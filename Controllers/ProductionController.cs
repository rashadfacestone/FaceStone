using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FaceStone.Web.Data;
using FaceStone.Web.Hubs;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;

namespace FaceStone.Web.Controllers;

/// <summary>
/// Production Department Controller (قسم الإنتاج)
/// Connects Storage inventory, Laboratory quality gates, Batch tracking, Output classification, and Rework workflows.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class ProductionController : ControllerBase
{
    private readonly FaceStoneDbContext _db;
    private readonly ILogger<ProductionController> _logger;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IWebHostEnvironment _env;
    private readonly Services.IProductionShiftService _shiftService;

    public ProductionController(
        FaceStoneDbContext db,
        ILogger<ProductionController> logger,
        IHubContext<ChatHub> hubContext,
        IWebHostEnvironment env,
        Services.IProductionShiftService shiftService)
    {
        _db = db;
        _logger = logger;
        _hubContext = hubContext;
        _env = env;
        _shiftService = shiftService;
    }

    private (bool IsAuth, string Role, string UserName, Guid? UserId) GetAuthenticatedUser()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var userName = User.FindFirst(ClaimTypes.Name)?.Value ??
                       User.FindFirst("FullName")?.Value ??
                       User.FindFirst("username")?.Value ??
                       User.Identity?.Name ??
                       "المستخدم الحالي";

        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? userId = Guid.TryParse(idClaim, out var g) ? g : null;

        var hasAccess = AppRoles.HasProductionAccess(role) ||
                        AppRoles.HasStorageAccess(role) ||
                        AppRoles.HasManagementAccess(role) ||
                        AppRoles.HasLaboratoryAccess(role) ||
                        AppRoles.HasControlMonitoringAccess(role);

        return (hasAccess, role, userName, userId);
    }

    /// <summary>
    /// Resolves the authenticated employee's verified database profile, role, and server-side shift mapping.
    /// Never trusts shift or employee identity from the client.
    /// </summary>
    private async Task<(bool IsAuth, string Role, string FullName, Guid? UserId, string Email, string? Shift, string? Error)> GetResolvedEmployeeAsync()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var userName = User.FindFirst(ClaimTypes.Name)?.Value ??
                       User.FindFirst("FullName")?.Value ??
                       User.FindFirst("username")?.Value ??
                       User.Identity?.Name ??
                       string.Empty;

        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? userId = Guid.TryParse(idClaim, out var g) ? g : null;

        var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value ??
                         User.FindFirst("email")?.Value ??
                         string.Empty;

        // Fetch DB user record for absolute reliability
        UserAccount? dbUser = null;
        if (userId.HasValue)
        {
            dbUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        }
        if (dbUser == null && !string.IsNullOrEmpty(emailClaim))
        {
            dbUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == emailClaim);
        }
        if (dbUser == null && !string.IsNullOrEmpty(userName))
        {
            dbUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == userName);
        }

        var effectiveEmail = dbUser?.Email ?? emailClaim;
        var effectiveFullName = dbUser?.FullName ?? (string.IsNullOrEmpty(userName) ? "مسؤول الإنتاج" : userName);
        var effectiveRole = dbUser?.Role ?? role;
        var effectiveUserId = dbUser?.Id ?? userId;

        var isProductionManager = AppRoles.IsProductionManager(effectiveRole) ||
                                  effectiveRole == AppRoles.CEO ||
                                  effectiveRole == AppRoles.IT;

        var shift = _shiftService.GetShiftForEmail(effectiveEmail);

        // If CEO or IT without an explicit production shift mapping, default to First Shift for testing/administration
        if (string.IsNullOrEmpty(shift) && (effectiveRole == AppRoles.CEO || effectiveRole == AppRoles.IT))
        {
            shift = Services.ProductionShiftService.FirstShiftMorning;
        }

        string? error = null;
        if (!isProductionManager)
        {
            error = "هذا الإجراء مخصص لمدير إدارة الإنتاج والمصرح لهم فقط.";
        }
        else if (string.IsNullOrEmpty(shift))
        {
            error = $"حساب المستخدم ({effectiveEmail}) غير مرتبط بوردية إنتاج معتمدة في النظام.";
        }

        return (isProductionManager, effectiveRole, effectiveFullName, effectiveUserId, effectiveEmail, shift, error);
    }

    private void EnsureProductionManagerPermission(string role)
    {
        if (!AppRoles.IsProductionManager(role))
        {
            throw new UnauthorizedAccessException("هذا الإجراء مخصص لمدير إدارة الإنتاج والمصرح لهم فقط.");
        }
    }

    /// <summary>
    /// Concurrency-safe atomic sequence generator using SQL Server UPDLOCK and ROWLOCK
    /// </summary>
    private async Task<int> GetNextSequenceNumberAsync(string sequenceType)
    {
        var typeParam = sequenceType.Trim().ToUpperInvariant();

        if (_db.Database.IsSqlServer())
        {
            var result = await _db.Database
                .SqlQueryRaw<int>(
                    "SELECT NextValue FROM ProductionSequences WITH (UPDLOCK, ROWLOCK) WHERE SequenceType = {0}",
                    typeParam)
                .ToListAsync();

            if (result.Count > 0)
            {
                var currentVal = result[0];
                await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE ProductionSequences SET NextValue = NextValue + 1 WHERE SequenceType = {0}",
                    typeParam);
                return currentVal;
            }
        }

        var seq = await _db.ProductionSequences.FirstOrDefaultAsync(s => s.SequenceType == typeParam);
        if (seq == null)
        {
            seq = new ProductionSequence { SequenceType = typeParam, NextValue = 1 };
            await _db.ProductionSequences.AddAsync(seq);
            await _db.SaveChangesAsync();
        }

        var assigned = seq.NextValue;
        seq.NextValue += 1;
        await _db.SaveChangesAsync();
        return assigned;
    }

    // =========================================================================
    // 1. DASHBOARD & KPIS
    // =========================================================================
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<ProductionDashboardStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardStats()
    {
        var activeBatches = await _db.ProductionBatches.CountAsync(b => b.Status == "IN_PROGRESS" || b.Status == "ON_HOLD_LABORATORY");
        var pendingRequests = await _db.ProductionOrders.CountAsync(o => o.Status == "PENDING_STORAGE_APPROVAL" || o.Status == "DRAFT");
        var onHoldBatches = await _db.ProductionBatches.CountAsync(b => b.QualityHold || b.Status == "ON_HOLD_LABORATORY");
        var inProcessTests = await _db.LaboratoryInProcessReports.CountAsync();
        var semiFinished = await _db.ProductionOutputs.CountAsync(o => o.Classification == "SEMI_FINISHED");
        var finished = await _db.ProductionOutputs.CountAsync(o => o.Classification == "FINISHED");
        var rework = await _db.ProductionReworks.CountAsync(r => r.Status != "COMPLETED" && r.Status != "CANCELLED");

        var totalIssued = await _db.ProductionOrderItems.SumAsync(i => (decimal?)i.IssuedQuantity) ?? 0m;
        var totalOutput = await _db.ProductionOutputs.SumAsync(o => (decimal?)o.Quantity) ?? 0m;
        var totalWaste = await _db.ProductionBatches.SumAsync(b => (decimal?)b.TotalWasteQuantity) ?? 0m;

        var stats = new ProductionDashboardStatsDto
        {
            ActiveBatches = activeBatches,
            PendingRequests = pendingRequests,
            OnHoldBatches = onHoldBatches,
            InProcessTestsCount = inProcessTests,
            SemiFinishedCount = semiFinished,
            FinishedCount = finished,
            ReworkCount = rework,
            TotalIssuedRawMaterial = totalIssued,
            TotalProducedOutput = totalOutput,
            TotalMeasuredWaste = totalWaste
        };

        return Ok(ApiResponse<ProductionDashboardStatsDto>.Ok(stats));
    }

    // =========================================================================
    // DAILY PRODUCTION TRACKING SYSTEM (تسجيل الإنتاج اليومي)
    // =========================================================================

    /// <summary>
    /// Gets the authenticated employee's verified server-side shift mapping and status.
    /// </summary>
    [HttpGet("current-shift-info")]
    [ProducesResponseType(typeof(ApiResponse<DailyProductionShiftInfoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentShiftInfo()
    {
        var resolved = await GetResolvedEmployeeAsync();

        var info = new DailyProductionShiftInfoDto
        {
            EmployeeName = resolved.FullName,
            EmployeeEmail = resolved.Email,
            Shift = resolved.Shift ?? string.Empty,
            IsProductionManager = resolved.IsAuth,
            CanSubmit = resolved.IsAuth && !string.IsNullOrEmpty(resolved.Shift),
            ErrorMessage = resolved.Error,
            Today = DateTime.Today.ToString("yyyy-MM-dd")
        };

        return Ok(ApiResponse<DailyProductionShiftInfoDto>.Ok(info));
    }

    /// <summary>
    /// Returns the controlled list of products from the centralized product catalogue.
    /// </summary>
    [HttpGet("products")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductionProductCatalog.ProductItem>>), StatusCodes.Status200OK)]
    public IActionResult GetProductCatalog()
    {
        return Ok(ApiResponse<IReadOnlyList<ProductionProductCatalog.ProductItem>>.Ok(ProductionProductCatalog.StandardProducts));
    }

    /// <summary>
    /// Submits a new Daily Production Record (تسجيل الإنتاج اليومي).
    /// Enforces server-side employee and shift mapping, prevents duplicate products,
    /// supports multiple products and decimal arithmetic, and calculates duration, rates, net output, and waste %.
    /// </summary>
    [HttpPost("daily-submissions")]
    [ProducesResponseType(typeof(ApiResponse<DailyProductionSubmissionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateDailySubmission([FromBody] DailyProductionSubmitRequestDto dto)
    {
        var resolved = await GetResolvedEmployeeAsync();
        if (!resolved.IsAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(resolved.Error ?? "غير مصرح لك بتسجيل الإنتاج اليومي."));
        }

        if (string.IsNullOrEmpty(resolved.Shift))
        {
            return BadRequest(ApiResponse.Fail(resolved.Error ?? "حساب المستخدم غير مرتبط بوردية إنتاج معتمدة في النظام. يرجى مراجعة إدارة النظام."));
        }

        // Validate Shift Times
        if (dto.ShiftEnd <= dto.ShiftStart)
        {
            return BadRequest(ApiResponse.Fail("تاريخ ونهاية الوردية يجب أن يكون لاحقاً لتاريخ وبداية الوردية. مدة الإنتاج لا يمكن أن تكون صفراً أو سالبة."));
        }

        var durationHours = (decimal)(dto.ShiftEnd - dto.ShiftStart).TotalHours;
        if (durationHours <= 0)
        {
            return BadRequest(ApiResponse.Fail("مدة الوردية غير صالحة."));
        }

        // Validate Products Selection
        if (dto.Products == null || dto.Products.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("يجب اختيار منتج واحد على الأقل وإدخال كميته المنتجة."));
        }

        // Prevent duplicate product entries
        var productNames = dto.Products.Select(p => p.ProductName.Trim()).ToList();
        if (productNames.Count != productNames.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return BadRequest(ApiResponse.Fail("لا يمكن تكرار نفس المنتج في نفس تسجيل الوردية. يرجى دمج الكميات في حقل واحد."));
        }

        foreach (var p in dto.Products)
        {
            if (p.Quantity <= 0)
            {
                return BadRequest(ApiResponse.Fail($"الكمية المدخلة للمنتج '{p.ProductName}' يجب أن تكون أكبر من الصفر."));
            }
        }

        if (dto.WasteQuantity < 0)
        {
            return BadRequest(ApiResponse.Fail("كمية الهالك لا يمكن أن تكون سالبة."));
        }

        var totalGrossQuantity = dto.Products.Sum(p => p.Quantity);
        var netProductionQuantity = Math.Max(0m, totalGrossQuantity - dto.WasteQuantity);
        var overallProductionRate = durationHours > 0 ? Math.Round(totalGrossQuantity / durationHours, 3) : 0m;
        var wastePercentage = totalGrossQuantity > 0 ? Math.Round((dto.WasteQuantity / totalGrossQuantity) * 100m, 3) : 0m;

        // Concurrency-safe atomic sequence number
        var nextSeq = await GetNextSequenceNumberAsync("DAILY_SUBMISSION");
        var submissionNumber = $"DPS-{dto.ProductionDate:yyyyMMdd}-{nextSeq:D4}";

        var submission = new DailyProductionSubmission
        {
            SubmissionNumber = submissionNumber,
            ProductionDate = dto.ProductionDate.Date,
            EmployeeId = resolved.UserId ?? Guid.Empty,
            EmployeeName = resolved.FullName,
            EmployeeEmail = resolved.Email,
            Shift = resolved.Shift,
            ShiftStart = dto.ShiftStart,
            ShiftEnd = dto.ShiftEnd,
            DurationHours = Math.Round(durationHours, 3),
            TotalGrossQuantity = Math.Round(totalGrossQuantity, 3),
            WasteQuantity = Math.Round(dto.WasteQuantity, 3),
            NetProductionQuantity = Math.Round(netProductionQuantity, 3),
            ProductionRatePerHour = overallProductionRate,
            WastePercentage = wastePercentage,
            Notes = dto.Notes?.Trim(),
            ProductionReferenceNumber = dto.ProductionReferenceNumber,
            Status = "SUBMITTED",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = resolved.Email
        };

        foreach (var p in dto.Products)
        {
            var pRate = durationHours > 0 ? Math.Round(p.Quantity / durationHours, 3) : 0m;
            var pWaste = p.ProductWaste;
            var pNet = pWaste.HasValue ? Math.Max(0m, p.Quantity - pWaste.Value) : (decimal?)null;

            submission.Details.Add(new DailyProductionDetail
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName.Trim(),
                Quantity = Math.Round(p.Quantity, 3),
                Unit = string.IsNullOrWhiteSpace(p.Unit) ? "طن" : p.Unit.Trim(),
                ProductionRatePerHour = pRate,
                ProductWaste = pWaste,
                NetQuantity = pNet,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.DailyProductionSubmissions.AddAsync(submission);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Daily production record {SubmissionNumber} created by {Employee} for shift {Shift}",
            submission.SubmissionNumber, submission.EmployeeName, submission.Shift);

        var resultDto = MapToSubmissionDto(submission);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<DailyProductionSubmissionDto>.Ok(resultDto, "تم حفظ تسجيل الإنتاج اليومي بنجاح."));
    }

    /// <summary>
    /// Retrieves daily production submissions with multi-criteria filtering.
    /// Real records directly from SQL Server.
    /// </summary>
    [HttpGet("daily-submissions")]
    [ProducesResponseType(typeof(ApiResponse<List<DailyProductionSubmissionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDailySubmissions(
        [FromQuery] DateTime? date,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? shift,
        [FromQuery] string? employee,
        [FromQuery] string? product)
    {
        var query = _db.DailyProductionSubmissions
            .Include(s => s.Details)
            .AsNoTracking();

        if (date.HasValue)
        {
            var d = date.Value.Date;
            query = query.Where(s => s.ProductionDate == d);
        }
        else
        {
            if (startDate.HasValue)
            {
                var sd = startDate.Value.Date;
                query = query.Where(s => s.ProductionDate >= sd);
            }
            if (endDate.HasValue)
            {
                var ed = endDate.Value.Date;
                query = query.Where(s => s.ProductionDate <= ed);
            }
        }

        if (!string.IsNullOrWhiteSpace(shift))
        {
            var shiftTrimmed = shift.Trim();
            query = query.Where(s => s.Shift.Contains(shiftTrimmed));
        }

        if (!string.IsNullOrWhiteSpace(employee))
        {
            var empTrimmed = employee.Trim();
            query = query.Where(s => s.EmployeeName.Contains(empTrimmed) || s.EmployeeEmail.Contains(empTrimmed));
        }

        if (!string.IsNullOrWhiteSpace(product))
        {
            var prodTrimmed = product.Trim();
            query = query.Where(s => s.Details.Any(d => d.ProductName.Contains(prodTrimmed)));
        }

        var list = await query
            .OrderByDescending(s => s.ProductionDate)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync();

        var dtoList = list.Select(MapToSubmissionDto).ToList();
        return Ok(ApiResponse<List<DailyProductionSubmissionDto>>.Ok(dtoList));
    }

    /// <summary>
    /// Retrieves a single daily production submission by ID.
    /// </summary>
    [HttpGet("daily-submissions/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<DailyProductionSubmissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDailySubmissionById(int id)
    {
        var submission = await _db.DailyProductionSubmissions
            .Include(s => s.Details)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (submission == null)
        {
            return NotFound(ApiResponse.Fail($"سجل الإنتاج اليومي #{id} غير موجود."));
        }

        return Ok(ApiResponse<DailyProductionSubmissionDto>.Ok(MapToSubmissionDto(submission)));
    }

    /// <summary>
    /// Updates an existing daily production submission. Preserves original ID and records audit timestamp.
    /// </summary>
    [HttpPut("daily-submissions/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<DailyProductionSubmissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDailySubmission(int id, [FromBody] DailyProductionSubmitRequestDto dto)
    {
        var resolved = await GetResolvedEmployeeAsync();
        if (!resolved.IsAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بتعديل سجل الإنتاج."));
        }

        var submission = await _db.DailyProductionSubmissions
            .Include(s => s.Details)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (submission == null)
        {
            return NotFound(ApiResponse.Fail($"سجل الإنتاج اليومي #{id} غير موجود."));
        }

        if (dto.ShiftEnd <= dto.ShiftStart)
        {
            return BadRequest(ApiResponse.Fail("تاريخ ونهاية الوردية يجب أن يكون لاحقاً لبداية الوردية."));
        }

        var durationHours = (decimal)(dto.ShiftEnd - dto.ShiftStart).TotalHours;
        if (durationHours <= 0)
        {
            return BadRequest(ApiResponse.Fail("مدة الوردية غير صالحة."));
        }

        if (dto.Products == null || dto.Products.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("يجب اختيار منتج واحد على الأقل وإدخال كميته المنتجة."));
        }

        var productNames = dto.Products.Select(p => p.ProductName.Trim()).ToList();
        if (productNames.Count != productNames.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return BadRequest(ApiResponse.Fail("لا يمكن تكرار نفس المنتج في نفس تسجيل الوردية."));
        }

        foreach (var p in dto.Products)
        {
            if (p.Quantity <= 0)
            {
                return BadRequest(ApiResponse.Fail($"الكمية المدخلة للمنتج '{p.ProductName}' يجب أن تكون أكبر من الصفر."));
            }
        }

        if (dto.WasteQuantity < 0)
        {
            return BadRequest(ApiResponse.Fail("كمية الهالك لا يمكن أن تكون سالبة."));
        }

        var totalGrossQuantity = dto.Products.Sum(p => p.Quantity);
        var netProductionQuantity = Math.Max(0m, totalGrossQuantity - dto.WasteQuantity);
        var overallProductionRate = durationHours > 0 ? Math.Round(totalGrossQuantity / durationHours, 3) : 0m;
        var wastePercentage = totalGrossQuantity > 0 ? Math.Round((dto.WasteQuantity / totalGrossQuantity) * 100m, 3) : 0m;

        // Preserve original submission number, employee, and shift; update production values
        submission.ProductionDate = dto.ProductionDate.Date;
        submission.ShiftStart = dto.ShiftStart;
        submission.ShiftEnd = dto.ShiftEnd;
        submission.DurationHours = Math.Round(durationHours, 3);
        submission.TotalGrossQuantity = Math.Round(totalGrossQuantity, 3);
        submission.WasteQuantity = Math.Round(dto.WasteQuantity, 3);
        submission.NetProductionQuantity = Math.Round(netProductionQuantity, 3);
        submission.ProductionRatePerHour = overallProductionRate;
        submission.WastePercentage = wastePercentage;
        submission.Notes = dto.Notes?.Trim();
        submission.ProductionReferenceNumber = dto.ProductionReferenceNumber;
        submission.UpdatedAt = DateTime.UtcNow;
        submission.UpdatedBy = resolved.Email;

        _db.DailyProductionDetails.RemoveRange(submission.Details);
        submission.Details.Clear();

        foreach (var p in dto.Products)
        {
            var pRate = durationHours > 0 ? Math.Round(p.Quantity / durationHours, 3) : 0m;
            var pWaste = p.ProductWaste;
            var pNet = pWaste.HasValue ? Math.Max(0m, p.Quantity - pWaste.Value) : (decimal?)null;

            submission.Details.Add(new DailyProductionDetail
            {
                DailyProductionSubmissionId = submission.Id,
                ProductId = p.ProductId,
                ProductName = p.ProductName.Trim(),
                Quantity = Math.Round(p.Quantity, 3),
                Unit = string.IsNullOrWhiteSpace(p.Unit) ? "طن" : p.Unit.Trim(),
                ProductionRatePerHour = pRate,
                ProductWaste = pWaste,
                NetQuantity = pNet,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Daily production record {SubmissionNumber} updated by {User}",
            submission.SubmissionNumber, resolved.Email);

        return Ok(ApiResponse<DailyProductionSubmissionDto>.Ok(MapToSubmissionDto(submission), "تم تحديث تسجيل الإنتاج بنجاح."));
    }

    /// <summary>
    /// Aggregates daily production summary statistics for management without double counting.
    /// </summary>
    [HttpGet("daily-submissions/summary-stats")]
    [ProducesResponseType(typeof(ApiResponse<DailyProductionSummaryStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDailySummaryStats([FromQuery] DateTime? date, [FromQuery] string? shift)
    {
        var targetDate = (date ?? DateTime.Today).Date;

        var query = _db.DailyProductionSubmissions
            .Include(s => s.Details)
            .Where(s => s.ProductionDate == targetDate);

        if (!string.IsNullOrWhiteSpace(shift))
        {
            var shiftTrimmed = shift.Trim();
            query = query.Where(s => s.Shift.Contains(shiftTrimmed));
        }

        var submissions = await query.ToListAsync();

        var totalGross = submissions.Sum(s => s.TotalGrossQuantity);
        var totalWaste = submissions.Sum(s => s.WasteQuantity);
        var netProduction = submissions.Sum(s => s.NetProductionQuantity);
        var totalDuration = submissions.Sum(s => s.DurationHours);
        var overallRate = totalDuration > 0 ? Math.Round(totalGross / totalDuration, 3) : 0m;
        var wastePct = totalGross > 0 ? Math.Round((totalWaste / totalGross) * 100m, 3) : 0m;

        // Breakdown by product aggregated across submissions without double counting
        var breakdown = submissions
            .SelectMany(s => s.Details)
            .GroupBy(d => d.ProductName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DailyProductAggregateDto
            {
                ProductName = g.Key,
                TotalQuantity = Math.Round(g.Sum(d => d.Quantity), 3),
                Unit = g.FirstOrDefault()?.Unit ?? "طن",
                SubmissionsCount = g.Count()
            })
            .OrderByDescending(p => p.TotalQuantity)
            .ToList();

        var stats = new DailyProductionSummaryStatsDto
        {
            TotalGrossToday = Math.Round(totalGross, 3),
            TotalWasteToday = Math.Round(totalWaste, 3),
            NetProductionToday = Math.Round(netProduction, 3),
            OverallProductionRate = overallRate,
            AverageWastePercentage = wastePct,
            TotalDurationHours = Math.Round(totalDuration, 3),
            TotalSubmissionsCount = submissions.Count,
            ProductsBreakdown = breakdown
        };

        return Ok(ApiResponse<DailyProductionSummaryStatsDto>.Ok(stats));
    }

    private static DailyProductionSubmissionDto MapToSubmissionDto(DailyProductionSubmission s)
    {
        return new DailyProductionSubmissionDto
        {
            Id = s.Id,
            SubmissionNumber = s.SubmissionNumber,
            ProductionDate = s.ProductionDate,
            EmployeeId = s.EmployeeId,
            EmployeeName = s.EmployeeName,
            EmployeeEmail = s.EmployeeEmail,
            Shift = s.Shift,
            ShiftStart = s.ShiftStart,
            ShiftEnd = s.ShiftEnd,
            DurationHours = s.DurationHours,
            TotalGrossQuantity = s.TotalGrossQuantity,
            WasteQuantity = s.WasteQuantity,
            NetProductionQuantity = s.NetProductionQuantity,
            ProductionRatePerHour = s.ProductionRatePerHour,
            WastePercentage = s.WastePercentage,
            Notes = s.Notes,
            ProductionReferenceNumber = s.ProductionReferenceNumber,
            Status = s.Status,
            CreatedAt = s.CreatedAt,
            CreatedBy = s.CreatedBy,
            UpdatedAt = s.UpdatedAt,
            UpdatedBy = s.UpdatedBy,
            Details = (s.Details ?? new List<DailyProductionDetail>()).Select(d => new DailyProductionDetailDto
            {
                Id = d.Id,
                DailyProductionSubmissionId = d.DailyProductionSubmissionId,
                ProductId = d.ProductId,
                ProductName = d.ProductName,
                Quantity = d.Quantity,
                Unit = d.Unit,
                ProductionRatePerHour = d.ProductionRatePerHour,
                ProductWaste = d.ProductWaste,
                NetQuantity = d.NetQuantity
            }).ToList()
        };
    }

    // =========================================================================
    // 2. CONNECT PRODUCTION TO STORAGE INVENTORY
    // =========================================================================
    /// <summary>
    /// View warehouse inventory across storage locations from Storage source of truth
    /// </summary>
    [HttpGet("inventory")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionInventoryItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWarehouseInventory(
        [FromQuery] int? locationId = null,
        [FromQuery] string? materialType = null,
        [FromQuery] string? search = null,
        [FromQuery] bool onlyAvailable = false)
    {
        // 1. Group ledger movements by Location & RawMaterial to compute real running balance
        var ledgerBalances = await _db.StorageInventoryLedger
            .AsNoTracking()
            .GroupBy(l => new { l.StorageLocationId, l.RawMaterialId })
            .Select(g => new
            {
                LocationId = g.Key.StorageLocationId,
                MaterialId = g.Key.RawMaterialId,
                AvailableQuantity = g.Sum(x => x.Quantity)
            })
            .ToListAsync();

        var locations = await _db.StorageLocations.AsNoTracking().Where(l => l.IsActive).ToDictionaryAsync(l => l.Id, l => l.Name);
        var materials = await _db.RawMaterials.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m);

        // Fetch pending reserved quantities in unfulfilled production orders
        var reservedQuantities = await _db.ProductionOrderItems
            .AsNoTracking()
            .Where(i => i.ProductionOrder != null && 
                       (i.ProductionOrder.Status == "PENDING_STORAGE_APPROVAL" || i.ProductionOrder.Status == "PARTIALLY_ISSUED"))
            .GroupBy(i => new { i.StorageLocationId, i.RawMaterialId })
            .Select(g => new
            {
                LocationId = g.Key.StorageLocationId,
                MaterialId = g.Key.RawMaterialId,
                Reserved = g.Sum(x => x.RequestedQuantity - x.IssuedQuantity)
            })
            .ToListAsync();

        var reservedMap = reservedQuantities
            .Where(r => r.LocationId.HasValue)
            .ToDictionary(r => $"{r.LocationId!.Value}_{r.MaterialId}", r => r.Reserved);

        // Fetch Laboratory approved reference mappings
        var labApprovedRefs = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Where(r => r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0 && r.RawMaterialId.HasValue)
            .GroupBy(r => r.RawMaterialId!.Value)
            .Select(g => new
            {
                MaterialId = g.Key,
                LatestRef = g.OrderByDescending(x => x.ProductionReferenceNumber).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.MaterialId, x => x.LatestRef);

        var results = new List<ProductionInventoryItemDto>();

        foreach (var b in ledgerBalances)
        {
            if (!locations.TryGetValue(b.LocationId, out var locName)) continue;
            if (!materials.TryGetValue(b.MaterialId, out var mat)) continue;

            if (locationId.HasValue && b.LocationId != locationId.Value) continue;
            if (!string.IsNullOrWhiteSpace(materialType) && !string.Equals(mat.MaterialType, materialType, StringComparison.OrdinalIgnoreCase)) continue;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                if (!mat.Name.ToLowerInvariant().Contains(s) && !locName.ToLowerInvariant().Contains(s))
                    continue;
            }

            var avail = Math.Max(0m, b.AvailableQuantity);
            if (onlyAvailable && avail <= 0) continue;

            var reservedKey = $"{b.LocationId}_{b.MaterialId}";
            var reserved = reservedMap.TryGetValue(reservedKey, out var rVal) ? Math.Max(0m, rVal) : 0m;
            var freeToOrder = Math.Max(0m, avail - reserved);

            labApprovedRefs.TryGetValue(mat.Id, out var labReport);

            results.Add(new ProductionInventoryItemDto
            {
                RawMaterialId = mat.Id,
                MaterialName = mat.Name,
                MaterialType = mat.MaterialType ?? "MECHANICAL",
                StorageLocationId = b.LocationId,
                StorageLocationName = locName,
                AvailableQuantity = avail,
                Unit = "طن",
                ReservedQuantity = reserved,
                AvailableForNewOrders = freeToOrder,
                ProductionReferenceNumber = labReport?.ProductionReferenceNumber,
                IsLabEligible = labReport != null && labReport.ApprovalStatus == "APPROVED",
                LabInspectionStatus = labReport != null ? labReport.InspectionDecision : "لم يتم الفحص بعد"
            });
        }

        return Ok(ApiResponse<List<ProductionInventoryItemDto>>.Ok(results.OrderBy(r => r.StorageLocationName).ThenBy(r => r.MaterialName).ToList()));
    }

    /// <summary>
    /// Retrieve eligible raw materials that have passed Laboratory inspection and have stock in Storage
    /// </summary>
    [HttpGet("eligible-raw-materials")]
    [ProducesResponseType(typeof(ApiResponse<List<EligibleRawMaterialDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleRawMaterials([FromQuery] int? locationId = null)
    {
        // Must be approved by Laboratory with AcceptedQuantity > 0
        var approvedLabReports = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Where(r => r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0 && r.RawMaterialId.HasValue)
            .OrderByDescending(r => r.ProductionReferenceNumber)
            .ToListAsync();

        var locations = await _db.StorageLocations.AsNoTracking().Where(l => l.IsActive).ToListAsync();

        // Calculate current location balances
        var ledgerBalances = await _db.StorageInventoryLedger
            .AsNoTracking()
            .GroupBy(l => new { l.StorageLocationId, l.RawMaterialId })
            .Select(g => new
            {
                g.Key.StorageLocationId,
                g.Key.RawMaterialId,
                Balance = g.Sum(x => x.Quantity)
            })
            .Where(x => x.Balance > 0)
            .ToListAsync();

        var results = new List<EligibleRawMaterialDto>();

        foreach (var lab in approvedLabReports)
        {
            var mat = lab.RawMaterial;
            if (mat == null) continue;

            var matchingStock = ledgerBalances.Where(b => b.RawMaterialId == mat.Id);
            if (locationId.HasValue)
            {
                matchingStock = matchingStock.Where(b => b.StorageLocationId == locationId.Value);
            }

            foreach (var stock in matchingStock)
            {
                var loc = locations.FirstOrDefault(l => l.Id == stock.StorageLocationId);
                results.Add(new EligibleRawMaterialDto
                {
                    RawMaterialId = mat.Id,
                    MaterialName = mat.Name,
                    MaterialType = mat.MaterialType ?? lab.LaboratorySection,
                    ProductionReferenceNumber = lab.ProductionReferenceNumber,
                    AcceptedQuantity = lab.AcceptedQuantity,
                    AvailableStock = stock.Balance,
                    StorageLocationId = stock.StorageLocationId,
                    StorageLocationName = loc?.Name ?? $"مستودع #{stock.StorageLocationId}",
                    LaboratorySection = lab.LaboratorySection,
                    SupplierName = lab.SupplierName ?? string.Empty
                });
            }
        }

        return Ok(ApiResponse<List<EligibleRawMaterialDto>>.Ok(results));
    }

    /// <summary>
    /// Retrieve eligible Laboratory Production Reference Numbers for selection
    /// </summary>
    [HttpGet("eligible-references")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryReferenceLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleReferences()
    {
        var query = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Where(r => r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0)
            .OrderByDescending(r => r.ProductionReferenceNumber)
            .ToListAsync();

        var dtos = query.Select(LaboratoryController.MapToReferenceLookupDto).ToList();
        return Ok(ApiResponse<List<LaboratoryReferenceLookupDto>>.Ok(dtos));
    }

    // =========================================================================
    // 3. PRODUCTION ORDERS & MATERIAL REQUESTS
    // =========================================================================
    [HttpGet("orders")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductionOrders(
        [FromQuery] string? status = null,
        [FromQuery] int? referenceNumber = null,
        [FromQuery] string? search = null)
    {
        var query = _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.StorageLocation)
            .Include(o => o.Items)
            .Include(o => o.Batches)
            .Include(o => o.Outputs)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => o.Status == status.Trim().ToUpperInvariant());
        }

        if (referenceNumber.HasValue && referenceNumber.Value > 0)
        {
            query = query.Where(o => o.ProductionReferenceNumber == referenceNumber.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(o => o.OrderNumber.ToLower().Contains(s) ||
                                     o.ProductName.ToLower().Contains(s) ||
                                     o.StorageLocationName.ToLower().Contains(s) ||
                                     o.ResponsibleEmployee.ToLower().Contains(s));
        }

        var list = await query.OrderByDescending(o => o.CreatedAt).Take(200).ToListAsync();
        var dtos = list.Select(MapOrderToDto).ToList();
        return Ok(ApiResponse<List<ProductionOrderDto>>.Ok(dtos));
    }

    [HttpGet("orders/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProductionOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductionOrderById(int id)
    {
        var order = await _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.StorageLocation)
            .Include(o => o.Items)
                .ThenInclude(i => i.RawMaterial)
            .Include(o => o.Batches)
            .Include(o => o.Outputs)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(ApiResponse.Fail($"أمر الإنتاج رقم #{id} غير موجود."));
        }

        return Ok(ApiResponse<ProductionOrderDto>.Ok(MapOrderToDto(order)));
    }

    /// <summary>
    /// Create a new Production Order & Material Request to Storage
    /// Strictly verifies that raw materials have passed Laboratory inspection.
    /// </summary>
    [HttpPost("orders")]
    [ProducesResponseType(typeof(ApiResponse<ProductionOrderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProductionOrder([FromBody] CreateProductionOrderRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم الإنتاج."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات طلب الإنتاج غير صالحة."));
        }

        if (string.IsNullOrWhiteSpace(dto.ProductName))
        {
            return BadRequest(ApiResponse.Fail("اسم المنتج المطلوب أو العملية الإنتاجية حقل إجباري."));
        }

        if (dto.RequestedQuantity <= 0)
        {
            return BadRequest(ApiResponse.Fail("الكمية المستهدفة للإنتاج يجب أن تكون أكبر من الصفر."));
        }

        var location = await _db.StorageLocations.FirstOrDefaultAsync(l => l.Id == dto.StorageLocationId && l.IsActive);
        if (location == null)
        {
            return BadRequest(ApiResponse.Fail("مكان التخزين المحدد غير موجود أو غير مفعل."));
        }

        if (dto.Items == null || dto.Items.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("يجب تحديد مادة خام واحدة على الأقل مطلوبة لأمر الإنتاج."));
        }

        // Quality Gate: Validate shared Laboratory reference if provided
        if (dto.ProductionReferenceNumber.HasValue && dto.ProductionReferenceNumber.Value > 0)
        {
            var labApproved = await _db.LaboratoryRawMaterialReports
                .AnyAsync(r => r.ProductionReferenceNumber == dto.ProductionReferenceNumber.Value &&
                               r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0);

            if (!labApproved)
            {
                return BadRequest(ApiResponse.Fail($"الرقم المرجعي التتبعي #{dto.ProductionReferenceNumber} لم يجتز فحص واعتماد المعمل بنجاح، ولا يمكن استخدامه في الإنتاج."));
            }
        }

        // Validate items & material eligibility
        var resolvedItems = new List<(RawMaterial Material, decimal Quantity, string? Notes)>();
        foreach (var item in dto.Items)
        {
            if (item.RequestedQuantity <= 0)
            {
                return BadRequest(ApiResponse.Fail("الكمية المطلوبة لكل مادة خام يجب أن تكون أكبر من الصفر."));
            }

            var mat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Id == item.RawMaterialId);
            if (mat == null)
            {
                return BadRequest(ApiResponse.Fail($"الصنف رقم #{item.RawMaterialId} غير موجود بالنظام."));
            }

            // Quality Gate: Ensure material has passed Laboratory inspection
            var hasPassedLab = await _db.LaboratoryRawMaterialReports
                .AnyAsync(r => r.RawMaterialId == mat.Id && r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0);

            if (!hasPassedLab)
            {
                return BadRequest(ApiResponse.Fail($"المادة الخام '{mat.Name}' لم تجتز فحص الجودة المعملي المعتمد، ومحظور صرفها لخطوط الإنتاج."));
            }

            resolvedItems.Add((mat, item.RequestedQuantity, item.Notes));
        }

        using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            var seq = await GetNextSequenceNumberAsync("ORDER");
            var orderNumber = $"ORD-{DateTime.UtcNow:yyyy}-{seq:D4}";

            var order = new ProductionOrder
            {
                OrderNumber = orderNumber,
                ProductionReferenceNumber = dto.ProductionReferenceNumber,
                ProductName = dto.ProductName.Trim(),
                RequestedQuantity = dto.RequestedQuantity,
                Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "طن" : dto.Unit.Trim(),
                StorageLocationId = location.Id,
                StorageLocationName = location.Name,
                Status = "PENDING_STORAGE_APPROVAL",
                CurrentStage = "الخلط الأولي",
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                PlannedStartDate = dto.PlannedStartDate ?? DateTime.UtcNow,
                Notes = dto.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            await _db.ProductionOrders.AddAsync(order);
            await _db.SaveChangesAsync();

            foreach (var (mat, qty, notes) in resolvedItems)
            {
                var orderItem = new ProductionOrderItem
                {
                    ProductionOrderId = order.Id,
                    RawMaterialId = mat.Id,
                    MaterialName = mat.Name,
                    RequestedQuantity = qty,
                    IssuedQuantity = 0m,
                    ConsumedQuantity = 0m,
                    Unit = order.Unit,
                    StorageLocationId = location.Id,
                    Notes = notes
                };
                await _db.ProductionOrderItems.AddAsync(orderItem);
            }

            await _db.SaveChangesAsync();
            await dbTx.CommitAsync();

            _logger.LogInformation("Production Order {OrderNumber} (ID: {Id}) submitted by {User}.", order.OrderNumber, order.Id, userName);

            // SignalR notification to Storage & Management
            await _hubContext.Clients.All.SendAsync("ProductionOrderCreated", new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                productName = order.ProductName,
                requestedQuantity = order.RequestedQuantity,
                responsibleEmployee = userName
            });

            var result = await _db.ProductionOrders
                .Include(o => o.Items)
                .FirstAsync(o => o.Id == order.Id);

            return StatusCode(StatusCodes.Status201Created, ApiResponse<ProductionOrderDto>.Ok(MapOrderToDto(result), $"تم إنشاء طلب الإنتاج بنجاح برقم {order.OrderNumber} وإرساله للمستودع للمراجعة والصرف."));
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Failed to create Production Order.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ طلب الإنتاج."));
        }
    }

    // =========================================================================
    // 4. ACTIVE BATCH TRACKING & IN-PROCESS QUALITY GATES
    // =========================================================================
    [HttpGet("batches")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionBatchDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatches(
        [FromQuery] string? status = null,
        [FromQuery] int? orderId = null,
        [FromQuery] int? referenceNumber = null)
    {
        var query = _db.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProductionOrder)
            .Include(b => b.Outputs)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(b => b.Status == status.Trim().ToUpperInvariant());
        }

        if (orderId.HasValue && orderId.Value > 0)
        {
            query = query.Where(b => b.ProductionOrderId == orderId.Value);
        }

        if (referenceNumber.HasValue && referenceNumber.Value > 0)
        {
            query = query.Where(b => b.ProductionReferenceNumber == referenceNumber.Value);
        }

        var list = await query.OrderByDescending(b => b.StartedAt).Take(200).ToListAsync();
        var dtos = list.Select(MapBatchToDto).ToList();
        return Ok(ApiResponse<List<ProductionBatchDto>>.Ok(dtos));
    }

    [HttpGet("batches/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProductionBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBatchById(int id)
    {
        var batch = await _db.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProductionOrder)
            .Include(b => b.Outputs)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (batch == null)
        {
            return NotFound(ApiResponse.Fail($"تشغيلة الإنتاج رقم #{id} غير موجودة."));
        }

        var dto = MapBatchToDto(batch);

        // Fetch linked in-process lab tests
        var labTests = await _db.LaboratoryInProcessReports
            .AsNoTracking()
            .Where(t => t.ProductionBatchNumber == batch.BatchNumber ||
                       (batch.ProductionReferenceNumber.HasValue && t.ProductionReferenceNumber == batch.ProductionReferenceNumber.Value))
            .OrderByDescending(t => t.TestDateTime)
            .ToListAsync();

        dto.InProcessTests = labTests.Select(LaboratoryController.MapInProcessToDto).ToList();

        return Ok(ApiResponse<ProductionBatchDto>.Ok(dto));
    }

    /// <summary>
    /// Start a new Batch for an eligible Production Order
    /// </summary>
    [HttpPost("batches")]
    [ProducesResponseType(typeof(ApiResponse<ProductionBatchDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartBatch([FromBody] StartProductionBatchRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم الإنتاج."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات بدء التشغيلة غير صالحة."));
        }

        var order = await _db.ProductionOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == dto.ProductionOrderId);

        if (order == null)
        {
            return BadRequest(ApiResponse.Fail($"أمر الإنتاج رقم #{dto.ProductionOrderId} غير موجود."));
        }

        if (order.Status == "COMPLETED" || order.Status == "CANCELLED")
        {
            return BadRequest(ApiResponse.Fail($"أمر الإنتاج في حالة '{order.Status}' ولا يمكن بدء تشغيلة جديدة له."));
        }

        // Check total issued raw material
        var totalIssued = order.Items.Sum(i => i.IssuedQuantity);
        if (totalIssued <= 0 && !order.IsStorageApproved)
        {
            _logger.LogWarning("Batch started prior to storage completion for order {OrderNumber}.", order.OrderNumber);
        }

        var seq = await GetNextSequenceNumberAsync("BATCH");
        var batchNumber = $"BATCH-{DateTime.UtcNow:yyyy}-{seq:D4}";

        var batch = new ProductionBatch
        {
            BatchNumber = batchNumber,
            ProductionOrderId = order.Id,
            ProductionReferenceNumber = order.ProductionReferenceNumber,
            CurrentStage = string.IsNullOrWhiteSpace(dto.InitialStage) ? "الخلط الأولي" : dto.InitialStage.Trim(),
            Status = "IN_PROGRESS",
            QualityHold = false,
            TotalInputQuantity = totalIssued > 0 ? totalIssued : order.RequestedQuantity,
            TotalOutputQuantity = 0m,
            TotalWasteQuantity = 0m,
            StartedAt = DateTime.UtcNow,
            ResponsibleEmployee = userName,
            ResponsibleUserId = userId,
            Notes = dto.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userName
        };

        // Link to associated ProductionWorkInProcess entity to ensure 100% compatibility with Laboratory and legacy queries
        if (order.StorageOperationId.HasValue && order.StorageOperationId.Value > 0)
        {
            var existingWip = await _db.ProductionWorkInProcess
                .FirstOrDefaultAsync(w => w.StorageOperationId == order.StorageOperationId.Value);
            if (existingWip != null)
            {
                batch.ProductionWorkInProcessId = existingWip.Id;
            }
            else
            {
                var firstItem = order.Items.FirstOrDefault();
                var op = await _db.StorageOperations.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == order.StorageOperationId.Value);
                var firstOpItem = op?.Items.FirstOrDefault();
                if (firstItem != null && firstOpItem != null)
                {
                    var wip = new ProductionWorkInProcess
                    {
                        StorageOperationId = op!.Id,
                        StorageOperationItemId = firstOpItem.Id,
                        RawMaterialId = firstItem.RawMaterialId,
                        ProductionReferenceNumber = order.ProductionReferenceNumber,
                        TrackingNumber = batchNumber,
                        MaterialQuantityIssued = batch.TotalInputQuantity,
                        SourceStorageLocation = order.StorageLocationName ?? "المستودع الرئيسي",
                        TransferDate = DateTime.UtcNow,
                        Status = "قيد التشغيل",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userName
                    };
                    await _db.ProductionWorkInProcess.AddAsync(wip);
                    await _db.SaveChangesAsync();
                    batch.ProductionWorkInProcessId = wip.Id;
                }
            }
        }

        await _db.ProductionBatches.AddAsync(batch);

        order.Status = "IN_PRODUCTION";
        order.ActualStartDate ??= DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Batch {BatchNumber} started for Order {OrderNumber} by {User}.", batch.BatchNumber, order.OrderNumber, userName);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<ProductionBatchDto>.Ok(MapBatchToDto(batch), $"تم بدء تشغيلة الإنتاج بنجاح برقم {batch.BatchNumber}."));
    }

    /// <summary>
    /// Advance Batch Stage.
    /// MANDATORY QUALITY GATE: If an in-process laboratory test has failed (DOES_NOT_MEET),
    /// the backend strictly prevents moving forward until authorized resolution.
    /// </summary>
    [HttpPost("batches/{id:int}/advance-stage")]
    [ProducesResponseType(typeof(ApiResponse<ProductionBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdvanceBatchStage(int id, [FromBody] AdvanceBatchStageRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم الإنتاج."));
        }

        var batch = await _db.ProductionBatches
            .Include(b => b.ProductionOrder)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (batch == null)
        {
            return NotFound(ApiResponse.Fail($"تشغيلة الإنتاج رقم #{id} غير موجودة."));
        }

        // =========================================================================
        // MANDATORY QUALITY GATE: Verify Laboratory Hold & In-Process Test Results
        // =========================================================================
        if (batch.QualityHold || batch.Status == "ON_HOLD_LABORATORY")
        {
            return BadRequest(ApiResponse.Fail($"ممنوع الانتقال للمرحلة التالية: التشغيلة معلقة حالياً بأمر فني من قسم المعمل بسبب: '{batch.QualityHoldReason ?? "عدم مطابقة النتائج للمواصفات"}'."));
        }

        var latestFailedTest = await _db.LaboratoryInProcessReports
            .Where(t => t.ProductionBatchNumber == batch.BatchNumber && t.InspectionDecision == "DOES_NOT_MEET")
            .OrderByDescending(t => t.TestDateTime)
            .FirstOrDefaultAsync();

        if (latestFailedTest != null)
        {
            // Check if there is a newer passing retest
            var passingRetest = await _db.LaboratoryInProcessReports
                .Where(t => t.ProductionBatchNumber == batch.BatchNumber && 
                           t.InspectionDecision == "MEETS_REQUIREMENTS" && 
                           t.TestDateTime > latestFailedTest.TestDateTime)
                .AnyAsync();

            if (!passingRetest)
            {
                batch.QualityHold = true;
                batch.Status = "ON_HOLD_LABORATORY";
                batch.QualityHoldReason = latestFailedTest.Notes ?? "فحص المعمل غير مطابق للاشتراطات الفنية";
                batch.QualityHoldReportId = latestFailedTest.Id;
                batch.QualityHoldAt = latestFailedTest.TestDateTime;
                await _db.SaveChangesAsync();

                return BadRequest(ApiResponse.Fail($"ممنوع الانتقال للمرحلة التالية: توجد عينة فحص معملي غير مطابقة للاشتراطات في مرحلة '{latestFailedTest.ProductionStage}'. تم تعليق التشغيلة لحين إجراء فحص جديد أو اعتماد الإجراء التصحيحي."));
            }
        }

        if (string.IsNullOrWhiteSpace(dto?.NextStage))
        {
            return BadRequest(ApiResponse.Fail("يرجى تحديد المرحلة الإنتاجية التالية."));
        }

        batch.CurrentStage = dto.NextStage.Trim();
        if (batch.ProductionOrder != null)
        {
            batch.ProductionOrder.CurrentStage = batch.CurrentStage;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Batch {BatchNumber} advanced to stage {Stage} by {User}.", batch.BatchNumber, batch.CurrentStage, userName);

        return Ok(ApiResponse<ProductionBatchDto>.Ok(MapBatchToDto(batch), $"تم نقل التشغيلة إلى مرحلة '{batch.CurrentStage}' بنجاح."));
    }

    /// <summary>
    /// Release Laboratory Quality Gate Hold if authorized
    /// </summary>
    [HttpPost("batches/{id:int}/release-hold")]
    [ProducesResponseType(typeof(ApiResponse<ProductionBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReleaseHold(int id, [FromBody] AdvanceBatchStageRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم الإنتاج."));
        }

        var batch = await _db.ProductionBatches.FirstOrDefaultAsync(b => b.Id == id);
        if (batch == null)
        {
            return NotFound(ApiResponse.Fail($"تشغيلة الإنتاج رقم #{id} غير موجودة."));
        }

        batch.QualityHold = false;
        batch.Status = "IN_PROGRESS";
        batch.QualityHoldReleasedAt = DateTime.UtcNow;
        batch.QualityHoldReleasedBy = userName;
        if (!string.IsNullOrWhiteSpace(dto?.Notes))
        {
            batch.Notes = string.IsNullOrWhiteSpace(batch.Notes) 
                ? $"[فك التعليق بواسطة {userName}: {dto.Notes}]" 
                : $"{batch.Notes} | [فك التعليق بواسطة {userName}: {dto.Notes}]";
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Quality Hold on Batch {BatchNumber} released by {User}.", batch.BatchNumber, userName);

        return Ok(ApiResponse<ProductionBatchDto>.Ok(MapBatchToDto(batch), "تم فك تعليق التشغيلة والسماح بمواصلة خط الإنتاج."));
    }

    // =========================================================================
    // 5. PRODUCTION OUTPUT & WASTE RECONCILIATION
    // =========================================================================
    /// <summary>
    /// Record actual production output.
    /// Classifies output as Semi-Finished, Finished, or Recoverable Waste.
    /// MANDATORY QUALITY GATE: Output recording is BLOCKED if batch is on Laboratory Hold!
    /// </summary>
    [HttpPost("batches/{id:int}/outputs")]
    [ProducesResponseType(typeof(ApiResponse<ProductionOutputDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordOutput(int id, [FromBody] CreateProductionOutputRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم الإنتاج."));
        }

        var batch = await _db.ProductionBatches
            .Include(b => b.ProductionOrder)
            .Include(b => b.Outputs)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (batch == null)
        {
            return NotFound(ApiResponse.Fail($"تشغيلة الإنتاج رقم #{id} غير موجودة."));
        }

        // =========================================================================
        // QUALITY GATE: Block output if batch is on Laboratory Hold
        // =========================================================================
        if (batch.QualityHold || batch.Status == "ON_HOLD_LABORATORY")
        {
            return BadRequest(ApiResponse.Fail($"ممنوع تسجيل مخرجات: التشغيلة معلقة بأمر فني من قسم المعمل بسبب: '{batch.QualityHoldReason ?? "عدم مطابقة المواصفات"}'."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات المخرجات غير صالحة."));
        }

        if (string.IsNullOrWhiteSpace(dto.ProductName))
        {
            return BadRequest(ApiResponse.Fail("اسم المنتج أو تصنيف المخرجات حقل إجباري."));
        }

        if (dto.Quantity <= 0)
        {
            return BadRequest(ApiResponse.Fail("الكمية الفعلية للمخرجات يجب أن تكون أكبر من الصفر."));
        }

        var classification = (dto.Classification ?? "FINISHED").Trim().ToUpperInvariant();
        if (classification != "SEMI_FINISHED" && classification != "FINISHED" && classification != "RECOVERABLE_WASTE")
        {
            return BadRequest(ApiResponse.Fail("تصنيف المخرجات غير صحيح. الخيارات المتاحة: SEMI_FINISHED أو FINISHED أو RECOVERABLE_WASTE."));
        }

        StorageLocation? destLocation = null;
        if (dto.DestinationStorageLocationId.HasValue && dto.DestinationStorageLocationId.Value > 0)
        {
            destLocation = await _db.StorageLocations.FirstOrDefaultAsync(l => l.Id == dto.DestinationStorageLocationId.Value);
        }

        using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            var seq = await GetNextSequenceNumberAsync("OUTPUT");
            var outputNumber = $"OUT-{DateTime.UtcNow:yyyy}-{seq:D4}";

            var output = new ProductionOutput
            {
                OutputNumber = outputNumber,
                ProductionOrderId = batch.ProductionOrderId,
                ProductionBatchId = batch.Id,
                ProductionReferenceNumber = batch.ProductionReferenceNumber,
                ProductName = dto.ProductName.Trim(),
                Classification = classification,
                Quantity = dto.Quantity,
                Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "طن" : dto.Unit.Trim(),
                ProductionStage = string.IsNullOrWhiteSpace(dto.ProductionStage) ? batch.CurrentStage : dto.ProductionStage.Trim(),
                OutputDate = DateTime.UtcNow,
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                NextStage = classification == "SEMI_FINISHED" ? dto.NextStage?.Trim() : null,
                DestinationStorageLocationId = destLocation?.Id,
                DestinationStorageLocationName = destLocation?.Name,
                LaboratoryConformityStatus = classification == "FINISHED" ? "PENDING_INSPECTION" : "NOT_APPLICABLE",
                IsStorageReceived = false,
                StorageReceivedQuantity = 0m,
                Notes = dto.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            await _db.ProductionOutputs.AddAsync(output);
            await _db.SaveChangesAsync();

            // Update batch total outputs and waste reconciliation
            var allOutputs = await _db.ProductionOutputs.Where(o => o.ProductionBatchId == batch.Id).ToListAsync();
            var totalOut = allOutputs.Sum(o => o.Quantity);
            batch.TotalOutputQuantity = totalOut;

            // Difference = Input - Outputs
            var diff = Math.Max(0m, batch.TotalInputQuantity - totalOut);
            batch.TotalWasteQuantity = diff;

            // If this is a finished output and batch has fulfilled order, update order status
            if (classification == "FINISHED" && batch.ProductionOrder != null)
            {
                batch.ProductionOrder.Status = "PENDING_FINAL_LABORATORY_INSPECTION";
            }

            await _db.SaveChangesAsync();
            await dbTx.CommitAsync();

            _logger.LogInformation("Output {OutputNumber} recorded for Batch {BatchNumber} ({Qty} {Unit}, {Class}) by {User}.",
                output.OutputNumber, batch.BatchNumber, output.Quantity, output.Unit, output.Classification, userName);

            return StatusCode(StatusCodes.Status201Created, ApiResponse<ProductionOutputDto>.Ok(MapOutputToDto(output), $"تم تسجيل المخرجات بنجاح برقم {output.OutputNumber}."));
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Failed to record Production Output.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ ناتج الإنتاج."));
        }
    }

    [HttpGet("outputs")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionOutputDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOutputs(
        [FromQuery] string? classification = null,
        [FromQuery] int? referenceNumber = null,
        [FromQuery] int? batchId = null)
    {
        var query = _db.ProductionOutputs
            .AsNoTracking()
            .Include(o => o.ProductionOrder)
            .Include(o => o.ProductionBatch)
            .Include(o => o.DestinationStorageLocation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(classification) && !string.Equals(classification, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => o.Classification == classification.Trim().ToUpperInvariant());
        }

        if (referenceNumber.HasValue && referenceNumber.Value > 0)
        {
            query = query.Where(o => o.ProductionReferenceNumber == referenceNumber.Value);
        }

        if (batchId.HasValue && batchId.Value > 0)
        {
            query = query.Where(o => o.ProductionBatchId == batchId.Value);
        }

        var list = await query.OrderByDescending(o => o.OutputDate).Take(200).ToListAsync();
        var dtos = list.Select(MapOutputToDto).ToList();
        return Ok(ApiResponse<List<ProductionOutputDto>>.Ok(dtos));
    }

    // =========================================================================
    // 6. FINAL PRODUCT LABORATORY APPROVAL & REWORK ORDERS
    // =========================================================================
    /// <summary>
    /// Create a traceable Rework Order when a finished product fails Laboratory inspection
    /// </summary>
    [HttpPost("rework-orders")]
    [ProducesResponseType(typeof(ApiResponse<ProductionReworkDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReworkOrder([FromBody] CreateReworkOrderRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم الإنتاج."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات أمر إعادة التشغيل غير صالحة."));
        }

        var output = await _db.ProductionOutputs
            .Include(o => o.ProductionBatch)
            .Include(o => o.ProductionOrder)
            .FirstOrDefaultAsync(o => o.Id == dto.ProductionOutputId);

        if (output == null)
        {
            return BadRequest(ApiResponse.Fail($"ناتج الإنتاج رقم #{dto.ProductionOutputId} غير موجود."));
        }

        if (output.IsReworkCreated)
        {
            return BadRequest(ApiResponse.Fail("تم بالفعل إنشاء أمر إعادة تشغيل لهذا الناتج مسبقاً."));
        }

        using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            var seq = await GetNextSequenceNumberAsync("REWORK");
            var reworkNumber = $"RWK-{DateTime.UtcNow:yyyy}-{seq:D4}";

            var rework = new ProductionRework
            {
                ReworkNumber = reworkNumber,
                OriginalProductionOrderId = output.ProductionOrderId,
                OriginalProductionBatchId = output.ProductionBatchId,
                OriginalProductionOutputId = output.Id,
                ProductionReferenceNumber = output.ProductionReferenceNumber,
                Reason = string.IsNullOrWhiteSpace(dto.Reason) ? "عدم مطابقة مواصفات المنتج النهائي في فحص المعمل" : dto.Reason.Trim(),
                Instructions = string.IsNullOrWhiteSpace(dto.Instructions) ? "إعادة الخلط والمعالجة الكيميائية لضبط المواصفات" : dto.Instructions.Trim(),
                Status = "IN_PROGRESS",
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            await _db.ProductionReworks.AddAsync(rework);

            output.IsReworkCreated = true;
            output.NonConformityAction = "REWORK_REMANUFACTURE";
            output.LaboratoryConformityStatus = "REWORK_REQUIRED";

            if (output.ProductionOrder != null)
            {
                output.ProductionOrder.Status = "REWORK_REQUIRED";
            }

            if (output.ProductionBatch != null)
            {
                output.ProductionBatch.Status = "REWORK";
            }

            await _db.SaveChangesAsync();
            rework.OriginalProductionOutputId = output.Id;
            output.ReworkOrderId = rework.Id;

            await _db.SaveChangesAsync();
            await dbTx.CommitAsync();

            _logger.LogInformation("Rework Order {ReworkNumber} created for Output {OutputNumber} by {User}.",
                rework.ReworkNumber, output.OutputNumber, userName);

            var resultDto = new ProductionReworkDto
            {
                Id = rework.Id,
                ReworkNumber = rework.ReworkNumber,
                OriginalProductionOrderId = rework.OriginalProductionOrderId,
                OriginalOrderNumber = output.ProductionOrder?.OrderNumber ?? string.Empty,
                OriginalProductionBatchId = rework.OriginalProductionBatchId,
                OriginalBatchNumber = output.ProductionBatch?.BatchNumber ?? string.Empty,
                OriginalProductionOutputId = rework.OriginalProductionOutputId,
                OriginalOutputNumber = output.OutputNumber,
                ProductionReferenceNumber = rework.ProductionReferenceNumber,
                Reason = rework.Reason,
                Instructions = rework.Instructions,
                Status = rework.Status,
                ResponsibleEmployee = rework.ResponsibleEmployee,
                CreatedAt = rework.CreatedAt,
                CreatedBy = rework.CreatedBy
            };

            return StatusCode(StatusCodes.Status201Created, ApiResponse<ProductionReworkDto>.Ok(resultDto, $"تم إنشاء أمر إعادة التشغيل بنجاح برقم {rework.ReworkNumber}."));
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Failed to create Rework Order.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء إنشاء أمر إعادة التشغيل."));
        }
    }

    [HttpGet("rework-orders")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionReworkDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReworkOrders([FromQuery] int? referenceNumber = null)
    {
        var query = _db.ProductionReworks.AsNoTracking().AsQueryable();

        if (referenceNumber.HasValue && referenceNumber.Value > 0)
        {
            query = query.Where(r => r.ProductionReferenceNumber == referenceNumber.Value);
        }

        var list = await query.OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync();

        var ordersMap = await _db.ProductionOrders.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.OrderNumber);
        var batchesMap = await _db.ProductionBatches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.BatchNumber);
        var outputsMap = await _db.ProductionOutputs.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.OutputNumber);

        var dtos = list.Select(r => new ProductionReworkDto
        {
            Id = r.Id,
            ReworkNumber = r.ReworkNumber,
            OriginalProductionOrderId = r.OriginalProductionOrderId,
            OriginalOrderNumber = ordersMap.TryGetValue(r.OriginalProductionOrderId, out var oNum) ? oNum : string.Empty,
            OriginalProductionBatchId = r.OriginalProductionBatchId,
            OriginalBatchNumber = batchesMap.TryGetValue(r.OriginalProductionBatchId, out var bNum) ? bNum : string.Empty,
            OriginalProductionOutputId = r.OriginalProductionOutputId,
            OriginalOutputNumber = outputsMap.TryGetValue(r.OriginalProductionOutputId, out var outNum) ? outNum : string.Empty,
            ProductionReferenceNumber = r.ProductionReferenceNumber,
            Reason = r.Reason,
            Instructions = r.Instructions,
            Status = r.Status,
            NewProductionOrderId = r.NewProductionOrderId,
            NewProductionBatchId = r.NewProductionBatchId,
            ResponsibleEmployee = r.ResponsibleEmployee,
            CreatedAt = r.CreatedAt,
            CreatedBy = r.CreatedBy
        }).ToList();

        return Ok(ApiResponse<List<ProductionReworkDto>>.Ok(dtos));
    }

    // =========================================================================
    // 7. STORAGE INTEGRATION — RECEIVING FINISHED / SEMI-FINISHED OUTPUT
    // =========================================================================
    /// <summary>
    /// View outputs eligible for receiving into Storage
    /// Only finished outputs that have passed Final Laboratory Conformity (CONFORMS) and approved are eligible for finished stock.
    /// </summary>
    [HttpGet("for-storage-receiving")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionOutputDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOutputsForStorageReceiving()
    {
        var outputs = await _db.ProductionOutputs
            .AsNoTracking()
            .Include(o => o.ProductionOrder)
            .Include(o => o.ProductionBatch)
            .Include(o => o.DestinationStorageLocation)
            .Where(o => !o.IsStorageReceived)
            .ToListAsync();

        // Filter eligible outputs:
        // Finished products MUST have passed Laboratory conformity (CONFORMS)
        // Semi-finished products and Recoverable waste can be received into their designated locations
        var eligible = new List<ProductionOutput>();

        foreach (var o in outputs)
        {
            if (o.Classification == "FINISHED")
            {
                // Verify Laboratory certificate
                var hasPassedFinal = false;
                if (o.ProductionReferenceNumber.HasValue)
                {
                    hasPassedFinal = await _db.LaboratoryFinalConformityCertificates
                        .AnyAsync(c => c.ProductionReferenceNumber == o.ProductionReferenceNumber.Value &&
                                       c.ConformityDecision == "CONFORMS" &&
                                       c.ApprovalStatus == "APPROVED");
                }

                if (hasPassedFinal || o.LaboratoryConformityStatus == "CONFORMS")
                {
                    eligible.Add(o);
                }
            }
            else
            {
                // SEMI_FINISHED or RECOVERABLE_WASTE
                eligible.Add(o);
            }
        }

        var dtos = eligible.Select(MapOutputToDto).ToList();
        return Ok(ApiResponse<List<ProductionOutputDto>>.Ok(dtos));
    }

    /// <summary>
    /// Storage Manager receives eligible output into warehouse inventory.
    /// Posts directly to StorageInventoryLedger and marks output as received.
    /// </summary>
    [HttpPost("receive-output-in-storage")]
    [ProducesResponseType(typeof(ApiResponse<ProductionOutputDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceiveOutputInStorage([FromBody] ReceiveOutputInStorageRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم المخازن."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات استلام المخرجات غير صالحة."));
        }

        var output = await _db.ProductionOutputs
            .Include(o => o.ProductionBatch)
            .Include(o => o.ProductionOrder)
            .FirstOrDefaultAsync(o => o.Id == dto.ProductionOutputId);

        if (output == null)
        {
            return NotFound(ApiResponse.Fail($"ناتج الإنتاج رقم #{dto.ProductionOutputId} غير موجود."));
        }

        if (output.IsStorageReceived)
        {
            return BadRequest(ApiResponse.Fail("تم استلام هذا الناتج بالمستودع مسبقاً ولا يمكن تكرار ترحيله."));
        }

        var location = await _db.StorageLocations.FirstOrDefaultAsync(l => l.Id == dto.StorageLocationId && l.IsActive);
        if (location == null)
        {
            return BadRequest(ApiResponse.Fail("مكان التخزين المحدد غير موجود أو غير مفعل."));
        }

        // =========================================================================
        // QUALITY GATE: Finished products MUST have passed Final Laboratory Conformity
        // =========================================================================
        if (output.Classification == "FINISHED")
        {
            var isConforming = output.LaboratoryConformityStatus == "CONFORMS";
            if (!isConforming && output.ProductionReferenceNumber.HasValue)
            {
                isConforming = await _db.LaboratoryFinalConformityCertificates
                    .AnyAsync(c => c.ProductionReferenceNumber == output.ProductionReferenceNumber.Value &&
                                   c.ConformityDecision == "CONFORMS" &&
                                   c.ApprovalStatus == "APPROVED");
            }

            if (!isConforming)
            {
                return BadRequest(ApiResponse.Fail("محظور إدخال المنتج لمستودع البضاعة الجاهزة قبل صدور واعتماد شهادة المطابقة النهائية (CONFORMS) من قسم المعمل."));
            }
        }

        var receiveQty = dto.ReceivedQuantity > 0 ? dto.ReceivedQuantity : output.Quantity;
        if (receiveQty > output.Quantity)
        {
            return BadRequest(ApiResponse.Fail($"الكمية المستلمة ({receiveQty:N3} طن) لا يمكن أن تتجاوز الكمية الفعلية المنتجة ({output.Quantity:N3} طن)."));
        }

        using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Find or create RawMaterial card for this output
            var matName = output.ProductName.Trim();
            var rawMat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Name == matName);
            if (rawMat == null)
            {
                rawMat = new RawMaterial
                {
                    Name = matName,
                    MaterialType = output.Classification == "FINISHED" ? "FINISHED_PRODUCT" : "SEMI_FINISHED",
                    InitialQuantity = 0m,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName
                };
                await _db.RawMaterials.AddAsync(rawMat);
                await _db.SaveChangesAsync();
            }

            // Post to StorageInventoryLedger
            var currentStock = await _db.StorageInventoryLedger
                .Where(l => l.StorageLocationId == location.Id && l.RawMaterialId == rawMat.Id)
                .SumAsync(l => (decimal?)l.Quantity) ?? 0m;

            var newStock = currentStock + receiveQty;

            var ledgerEntry = new StorageInventoryLedger
            {
                StorageLocationId = location.Id,
                RawMaterialId = rawMat.Id,
                MovementType = "IMPORT",
                Quantity = receiveQty, // Positive for inward receipt
                RunningBalance = newStock,
                MovementDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            await _db.StorageInventoryLedger.AddAsync(ledgerEntry);

            output.IsStorageReceived = true;
            output.StorageReceivedQuantity = receiveQty;
            output.StorageReceivedAt = DateTime.UtcNow;
            output.StorageReceivedBy = userName;
            output.DestinationStorageLocationId = location.Id;
            output.DestinationStorageLocationName = location.Name;

            // If order completed all outputs, mark order as COMPLETED
            if (output.ProductionOrder != null)
            {
                output.ProductionOrder.Status = "COMPLETED";
                output.ProductionOrder.CompletedDate = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            await dbTx.CommitAsync();

            _logger.LogInformation("Output {OutputNumber} received into Storage {Location} ({Qty} TON) by {User}.",
                output.OutputNumber, location.Name, receiveQty, userName);

            return Ok(ApiResponse<ProductionOutputDto>.Ok(MapOutputToDto(output), $"تم استلام المخرجات بنجاح في {location.Name} وترحيل رصيد المخزن ({receiveQty:N3} طن)."));
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Failed to receive Production Output into storage.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء استلام المخرجات بالمستودع."));
        }
    }

    // =========================================================================
    // 8. PRODUCTION HISTORY & TRACEABILITY
    // =========================================================================
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductionHistory(
        [FromQuery] int? referenceNumber = null,
        [FromQuery] string? search = null)
    {
        var query = _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.StorageLocation)
            .Include(o => o.Items)
            .Include(o => o.Batches)
            .Include(o => o.Outputs)
            .Where(o => o.Status == "COMPLETED" || o.Status == "IN_PRODUCTION" || o.Status == "APPROVED_FOR_STORAGE")
            .AsQueryable();

        if (referenceNumber.HasValue && referenceNumber.Value > 0)
        {
            query = query.Where(o => o.ProductionReferenceNumber == referenceNumber.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(o => o.OrderNumber.ToLower().Contains(s) ||
                                     o.ProductName.ToLower().Contains(s) ||
                                     o.ResponsibleEmployee.ToLower().Contains(s));
        }

        var list = await query.OrderByDescending(o => o.CreatedAt).Take(200).ToListAsync();
        var dtos = list.Select(MapOrderToDto).ToList();
        return Ok(ApiResponse<List<ProductionOrderDto>>.Ok(dtos));
    }

    [HttpGet("traceability/{referenceNumber:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductionTraceability(int referenceNumber)
    {
        var rawReport = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .FirstOrDefaultAsync(r => r.ProductionReferenceNumber == referenceNumber);

        if (rawReport == null)
        {
            return NotFound(ApiResponse.Fail($"الرقم المرجعي التتبعي #{referenceNumber} غير موجود."));
        }

        var orders = await _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Batches)
            .Include(o => o.Outputs)
            .Where(o => o.ProductionReferenceNumber == referenceNumber)
            .ToListAsync();

        var batches = await _db.ProductionBatches
            .AsNoTracking()
            .Include(b => b.Outputs)
            .Where(b => b.ProductionReferenceNumber == referenceNumber)
            .ToListAsync();

        var inProcessTests = await _db.LaboratoryInProcessReports
            .AsNoTracking()
            .Where(t => t.ProductionReferenceNumber == referenceNumber)
            .OrderBy(t => t.TestDateTime)
            .ToListAsync();

        var finalCert = await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProductionReferenceNumber == referenceNumber);

        var reworks = await _db.ProductionReworks
            .AsNoTracking()
            .Where(r => r.ProductionReferenceNumber == referenceNumber)
            .ToListAsync();

        var result = new
        {
            productionReferenceNumber = referenceNumber,
            rawMaterialReport = rawReport,
            orders = orders.Select(MapOrderToDto).ToList(),
            batches = batches.Select(MapBatchToDto).ToList(),
            inProcessTests = inProcessTests.Select(LaboratoryController.MapInProcessToDto).ToList(),
            finalCertificate = finalCert != null ? LaboratoryController.MapFinalCertToDto(finalCert) : null,
            reworks = reworks
        };

        return Ok(ApiResponse<object>.Ok(result));
    }

    // =========================================================================
    // MAPPERS
    // =========================================================================
    public static ProductionOrderDto MapOrderToDto(ProductionOrder o)
    {
        return new ProductionOrderDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            ProductionReferenceNumber = o.ProductionReferenceNumber,
            ProductName = o.ProductName,
            RequestedQuantity = o.RequestedQuantity,
            Unit = o.Unit,
            StorageLocationId = o.StorageLocationId,
            StorageLocationName = o.StorageLocationName,
            Status = o.Status,
            CurrentStage = o.CurrentStage,
            ResponsibleEmployee = o.ResponsibleEmployee,
            PlannedStartDate = o.PlannedStartDate,
            ActualStartDate = o.ActualStartDate,
            CompletedDate = o.CompletedDate,
            Notes = o.Notes,
            AttachmentPath = o.AttachmentPath,
            AttachmentFileName = o.AttachmentFileName,
            IsStorageApproved = o.IsStorageApproved,
            StorageApprovedAt = o.StorageApprovedAt,
            StorageApprovedBy = o.StorageApprovedBy,
            StorageOperationId = o.StorageOperationId,
            CreatedAt = o.CreatedAt,
            CreatedBy = o.CreatedBy,
            Items = o.Items.Select(i => new ProductionOrderItemDto
            {
                Id = i.Id,
                ProductionOrderId = i.ProductionOrderId,
                RawMaterialId = i.RawMaterialId,
                MaterialName = i.MaterialName,
                RequestedQuantity = i.RequestedQuantity,
                IssuedQuantity = i.IssuedQuantity,
                ConsumedQuantity = i.ConsumedQuantity,
                Unit = i.Unit,
                StorageLocationId = i.StorageLocationId,
                Notes = i.Notes
            }).ToList(),
            Batches = o.Batches.Select(b => new ProductionBatchSummaryDto
            {
                Id = b.Id,
                BatchNumber = b.BatchNumber,
                CurrentStage = b.CurrentStage,
                Status = b.Status,
                QualityHold = b.QualityHold,
                QualityHoldReason = b.QualityHoldReason,
                TotalInputQuantity = b.TotalInputQuantity,
                TotalOutputQuantity = b.TotalOutputQuantity,
                StartedAt = b.StartedAt
            }).ToList(),
            Outputs = o.Outputs.Select(outp => new ProductionOutputSummaryDto
            {
                Id = outp.Id,
                OutputNumber = outp.OutputNumber,
                ProductName = outp.ProductName,
                Classification = outp.Classification,
                Quantity = outp.Quantity,
                Unit = outp.Unit,
                ProductionStage = outp.ProductionStage,
                LaboratoryConformityStatus = outp.LaboratoryConformityStatus,
                IsStorageReceived = outp.IsStorageReceived,
                OutputDate = outp.OutputDate
            }).ToList()
        };
    }

    public static ProductionBatchDto MapBatchToDto(ProductionBatch b)
    {
        return new ProductionBatchDto
        {
            Id = b.Id,
            BatchNumber = b.BatchNumber,
            ProductionOrderId = b.ProductionOrderId,
            OrderNumber = b.ProductionOrder?.OrderNumber ?? string.Empty,
            ProductionReferenceNumber = b.ProductionReferenceNumber,
            CurrentStage = b.CurrentStage,
            Status = b.Status,
            QualityHold = b.QualityHold,
            QualityHoldReason = b.QualityHoldReason,
            QualityHoldReportId = b.QualityHoldReportId,
            QualityHoldAt = b.QualityHoldAt,
            QualityHoldReleasedAt = b.QualityHoldReleasedAt,
            QualityHoldReleasedBy = b.QualityHoldReleasedBy,
            TotalInputQuantity = b.TotalInputQuantity,
            TotalOutputQuantity = b.TotalOutputQuantity,
            TotalWasteQuantity = b.TotalWasteQuantity,
            StartedAt = b.StartedAt,
            CompletedAt = b.CompletedAt,
            ResponsibleEmployee = b.ResponsibleEmployee,
            Notes = b.Notes,
            CreatedAt = b.CreatedAt,
            Outputs = b.Outputs.Select(outp => new ProductionOutputSummaryDto
            {
                Id = outp.Id,
                OutputNumber = outp.OutputNumber,
                ProductName = outp.ProductName,
                Classification = outp.Classification,
                Quantity = outp.Quantity,
                Unit = outp.Unit,
                ProductionStage = outp.ProductionStage,
                LaboratoryConformityStatus = outp.LaboratoryConformityStatus,
                IsStorageReceived = outp.IsStorageReceived,
                OutputDate = outp.OutputDate
            }).ToList()
        };
    }

    public static ProductionOutputDto MapOutputToDto(ProductionOutput outp)
    {
        return new ProductionOutputDto
        {
            Id = outp.Id,
            OutputNumber = outp.OutputNumber,
            ProductionOrderId = outp.ProductionOrderId,
            OrderNumber = outp.ProductionOrder?.OrderNumber ?? string.Empty,
            ProductionBatchId = outp.ProductionBatchId,
            BatchNumber = outp.ProductionBatch?.BatchNumber ?? string.Empty,
            ProductionReferenceNumber = outp.ProductionReferenceNumber,
            ProductName = outp.ProductName,
            Classification = outp.Classification,
            Quantity = outp.Quantity,
            Unit = outp.Unit,
            ProductionStage = outp.ProductionStage,
            OutputDate = outp.OutputDate,
            ResponsibleEmployee = outp.ResponsibleEmployee,
            NextStage = outp.NextStage,
            DestinationStorageLocationId = outp.DestinationStorageLocationId,
            DestinationStorageLocationName = outp.DestinationStorageLocationName,
            LaboratoryConformityStatus = outp.LaboratoryConformityStatus,
            LaboratoryCertificateId = outp.LaboratoryCertificateId,
            IsStorageReceived = outp.IsStorageReceived,
            StorageReceivedQuantity = outp.StorageReceivedQuantity,
            StorageReceivedAt = outp.StorageReceivedAt,
            StorageReceivedBy = outp.StorageReceivedBy,
            StorageOperationId = outp.StorageOperationId,
            NonConformityAction = outp.NonConformityAction,
            IsReworkCreated = outp.IsReworkCreated,
            ReworkOrderId = outp.ReworkOrderId,
            Notes = outp.Notes,
            AttachmentPath = outp.AttachmentPath,
            AttachmentFileName = outp.AttachmentFileName,
            CreatedAt = outp.CreatedAt,
            CreatedBy = outp.CreatedBy
        };
    }
}
