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

[ApiController]
[Route("api/control-monitoring")]
[Authorize(Policy = "ControlMonitoringAccess")]
public class ControlMonitoringController : ControllerBase
{
    private readonly FaceStoneDbContext _db;
    private readonly ILogger<ControlMonitoringController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IHubContext<ChatHub> _hubContext;

    public ControlMonitoringController(
        FaceStoneDbContext db,
        ILogger<ControlMonitoringController> logger,
        IWebHostEnvironment env,
        IHubContext<ChatHub> hubContext)
    {
        _db = db;
        _logger = logger;
        _env = env;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Retrieve Dashboard summary cards for Control & Monitoring.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<ControlMonitoringDashboardStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        var todayUtcStart = DateTime.UtcNow.Date;

        var transPending = await _db.Transportations
            .AsNoTracking()
            .CountAsync(t => t.ApprovalStatus == "PENDING");

        var storagePending = await _db.StorageTransactions
            .AsNoTracking()
            .CountAsync(s => s.ApprovalStatus == "PENDING");

        var transApprovedToday = await _db.Transportations
            .AsNoTracking()
            .CountAsync(t => t.ApprovalStatus == "APPROVED" && t.ReviewedAt.HasValue && t.ReviewedAt.Value >= todayUtcStart);

        var storageApprovedToday = await _db.StorageTransactions
            .AsNoTracking()
            .CountAsync(s => s.ApprovalStatus == "APPROVED" && s.ReviewedAt.HasValue && s.ReviewedAt.Value >= todayUtcStart);

        var transRejectedToday = await _db.Transportations
            .AsNoTracking()
            .CountAsync(t => t.ApprovalStatus == "REJECTED" && t.ReviewedAt.HasValue && t.ReviewedAt.Value >= todayUtcStart);

        var storageRejectedToday = await _db.StorageTransactions
            .AsNoTracking()
            .CountAsync(s => s.ApprovalStatus == "REJECTED" && s.ReviewedAt.HasValue && s.ReviewedAt.Value >= todayUtcStart);

        var stats = new ControlMonitoringDashboardStatsDto
        {
            PendingReviewsCount = transPending + storagePending,
            TransportationPendingCount = transPending,
            StoragePendingCount = storagePending,
            ApprovedTodayCount = transApprovedToday + storageApprovedToday,
            RejectedTodayCount = transRejectedToday + storageRejectedToday
        };

        return Ok(ApiResponse<ControlMonitoringDashboardStatsDto>.Ok(stats));
    }

    /// <summary>
    /// Retrieve list of forms for review with filtering.
    /// Default status is PENDING.
    /// </summary>
    [HttpGet("forms")]
    [ProducesResponseType(typeof(ApiResponse<List<ControlMonitoringReviewItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForms(
        [FromQuery] string? status = "PENDING",
        [FromQuery] string? formType = "ALL",
        [FromQuery] string? po = null,
        [FromQuery] string? employee = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var normalizedStatus = (status ?? "PENDING").Trim().ToUpperInvariant();
        var normalizedType = (formType ?? "ALL").Trim().ToUpperInvariant();
        var trimmedPo = po?.Trim().ToLowerInvariant();
        var trimmedEmp = employee?.Trim().ToLowerInvariant();
        var trimmedSearch = search?.Trim().ToLowerInvariant();

        var results = new List<ControlMonitoringReviewItemDto>();

        // 1. Fetch Transportation Forms
        if (normalizedType == "ALL" || normalizedType == "TRANSPORTATION")
        {
            var transQuery = _db.Transportations.AsNoTracking().AsQueryable();

            if (normalizedStatus != "ALL")
            {
                transQuery = transQuery.Where(t => t.ApprovalStatus == normalizedStatus);
            }

            if (!string.IsNullOrEmpty(trimmedPo))
            {
                transQuery = transQuery.Where(t => t.PONumber.ToLower().Contains(trimmedPo));
            }

            if (!string.IsNullOrEmpty(trimmedEmp))
            {
                transQuery = transQuery.Where(t => t.SubmittedBy != null && t.SubmittedBy.ToLower().Contains(trimmedEmp));
            }

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                transQuery = transQuery.Where(t => t.TransactionDate >= fDate);
            }

            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                transQuery = transQuery.Where(t => t.TransactionDate <= tDate);
            }

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                transQuery = transQuery.Where(t =>
                    t.PONumber.ToLower().Contains(trimmedSearch) ||
                    t.TransactionNumber.ToString().Contains(trimmedSearch) ||
                    t.Id.ToString().Contains(trimmedSearch) ||
                    (t.SubmittedBy != null && t.SubmittedBy.ToLower().Contains(trimmedSearch)) ||
                    t.DriverName.ToLower().Contains(trimmedSearch) ||
                    t.CarNumber.ToLower().Contains(trimmedSearch) ||
                    (t.CompanyName != null && t.CompanyName.ToLower().Contains(trimmedSearch)) ||
                    t.MaterialType.ToLower().Contains(trimmedSearch));
            }

            var transList = await transQuery.OrderByDescending(t => t.DateOfSubmit).Take(200).ToListAsync();

            foreach (var t in transList)
            {
                results.Add(MapTransportationToReviewItem(t));
            }
        }

        // 2. Fetch Storage Forms
        if (normalizedType == "ALL" || normalizedType == "STORAGE")
        {
            var storageQuery = _db.StorageTransactions.AsNoTracking().AsQueryable();

            if (normalizedStatus != "ALL")
            {
                storageQuery = storageQuery.Where(s => s.ApprovalStatus == normalizedStatus);
            }

            if (!string.IsNullOrEmpty(trimmedPo))
            {
                storageQuery = storageQuery.Where(s => s.PONumber != null && s.PONumber.ToLower().Contains(trimmedPo));
            }

            if (!string.IsNullOrEmpty(trimmedEmp))
            {
                storageQuery = storageQuery.Where(s => 
                    (s.CreatedBy != null && s.CreatedBy.ToLower().Contains(trimmedEmp)) ||
                    s.ReceivingEmployee.ToLower().Contains(trimmedEmp));
            }

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                storageQuery = storageQuery.Where(s => s.TransactionDate >= fDate);
            }

            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                storageQuery = storageQuery.Where(s => s.TransactionDate <= tDate);
            }

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                storageQuery = storageQuery.Where(s =>
                    (s.PONumber != null && s.PONumber.ToLower().Contains(trimmedSearch)) ||
                    s.MovementNumber.ToString().Contains(trimmedSearch) ||
                    s.Id.ToString().Contains(trimmedSearch) ||
                    (s.CreatedBy != null && s.CreatedBy.ToLower().Contains(trimmedSearch)) ||
                    s.ReceivingEmployee.ToLower().Contains(trimmedSearch) ||
                    s.Material.ToLower().Contains(trimmedSearch) ||
                    s.Action.ToLower().Contains(trimmedSearch));
            }

            var storageList = await storageQuery.OrderByDescending(s => s.CreatedAt).Take(200).ToListAsync();

            foreach (var s in storageList)
            {
                results.Add(MapStorageToReviewItem(s));
            }
        }

        // Sort combined items by submission date descending
        var sorted = results.OrderByDescending(r => r.SubmissionDate).ToList();
        return Ok(ApiResponse<List<ControlMonitoringReviewItemDto>>.Ok(sorted));
    }

    /// <summary>
    /// Retrieve single form for review by ID and optional formType.
    /// Supports direct /control-monitoring/request/{id} navigation.
    /// </summary>
    [HttpGet("forms/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ControlMonitoringReviewItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFormDetailsById(int id, [FromQuery] string? formType = null)
    {
        if (!string.IsNullOrWhiteSpace(formType))
        {
            return await GetFormDetails(formType, id);
        }

        // Try transportation first
        var trans = await _db.Transportations.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (trans != null)
        {
            return await GetFormDetails("transportation", id);
        }

        // Try storage
        var storage = await _db.StorageTransactions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (storage != null)
        {
            return await GetFormDetails("storage", id);
        }

        return NotFound(ApiResponse.Fail($"Request #{id} not found in Transportation or Storage."));
    }

    /// <summary>
    /// Retrieve single form for review with all details and full audit history.
    /// </summary>
    [HttpGet("forms/{formType}/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ControlMonitoringReviewItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFormDetails(string formType, int id)
    {
        var normType = formType.Trim().ToLowerInvariant();

        ControlMonitoringReviewItemDto? item = null;

        if (normType == "transportation")
        {
            var trans = await _db.Transportations.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (trans == null) return NotFound(ApiResponse.Fail("Transportation form not found."));
            item = MapTransportationToReviewItem(trans);
        }
        else if (normType == "storage")
        {
            var storage = await _db.StorageTransactions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            if (storage == null) return NotFound(ApiResponse.Fail("Storage form not found."));
            item = MapStorageToReviewItem(storage);
        }
        else
        {
            return BadRequest(ApiResponse.Fail("Invalid form type. Must be 'Transportation' or 'Storage'."));
        }

        // Attach full audit history
        var history = await _db.FormApprovalHistories
            .AsNoTracking()
            .Where(h => h.FormType.ToLower() == normType && h.FormId == id)
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new FormApprovalHistoryDto
            {
                Id = h.Id,
                FormType = h.FormType,
                FormId = h.FormId,
                PONumber = h.PONumber,
                TransactionOrMovementNumber = h.TransactionOrMovementNumber,
                PreviousStatus = h.PreviousStatus,
                NewStatus = h.NewStatus,
                Action = h.Action,
                ActorName = h.ActorName,
                ActorRole = h.ActorRole,
                Comment = h.Comment,
                Timestamp = h.Timestamp
            })
            .ToListAsync();

        item.AuditTrail = history;

        return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(item));
    }

    /// <summary>
    /// Accept / Approve a submitted Transportation or Storage form.
    /// </summary>
    [HttpPost("forms/{formType}/{id:int}/accept")]
    [ProducesResponseType(typeof(ApiResponse<ControlMonitoringReviewItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptForm(string formType, int id)
    {
        var normType = formType.Trim().ToLowerInvariant();
        var reviewerName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "موظف التحكم والمراقبة";
        Guid? reviewerId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            reviewerId = parsedGuid;
        }
        var reviewerRole = User.FindFirst(ClaimTypes.Role)?.Value ?? AppRoles.ControlMonitoring;

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            string poNumber = string.Empty;
            string txNumber = string.Empty;
            string prevStatus = "PENDING";

            if (normType == "transportation")
            {
                var trans = await _db.Transportations.FirstOrDefaultAsync(t => t.Id == id);
                if (trans == null)
                {
                    await dbTransaction.RollbackAsync();
                    return NotFound(ApiResponse.Fail("Transportation form not found."));
                }

                if (trans.ApprovalStatus == "APPROVED")
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("تم اعتماد طلب النقل هذا بالفعل ولا يمكن إعادة معالجته."));
                }

                prevStatus = trans.ApprovalStatus;
                poNumber = trans.PONumber;
                txNumber = $"#{trans.TransactionNumber}";

                // Update approval status
                trans.ApprovalStatus = "APPROVED";
                trans.TransactionStatus = "Completed";
                trans.ReviewedBy = reviewerName;
                trans.ReviewedByUserId = reviewerId;
                trans.ReviewedAt = DateTime.UtcNow;
                trans.RejectionReason = null; // Clear any previous rejection reason on approval

                // Deduct PO remaining quantity upon approval (if not already deducted)
                if (!string.IsNullOrWhiteSpace(trans.PONumber))
                {
                    var po = await _db.PurchaseOrders
                        .FromSqlInterpolated($"SELECT * FROM PurchaseOrders WITH (UPDLOCK, ROWLOCK) WHERE PONumber = {trans.PONumber}")
                        .FirstOrDefaultAsync();

                    if (po != null)
                    {
                        po.RemainingQuantity = Math.Max(0m, po.RemainingQuantity - trans.NetWeight);
                        po.UpdatedAt = DateTime.UtcNow;
                        _logger.LogInformation("PO {PO} remaining quantity updated to {Rem} after Transportation #{Tx} approval.",
                            po.PONumber, po.RemainingQuantity, trans.TransactionNumber);
                    }
                }

                // Add audit history
                var history = new FormApprovalHistory
                {
                    FormType = "Transportation",
                    FormId = trans.Id,
                    PONumber = trans.PONumber,
                    TransactionOrMovementNumber = txNumber,
                    PreviousStatus = prevStatus,
                    NewStatus = "APPROVED",
                    Action = "APPROVED",
                    ActorName = reviewerName,
                    ActorUserId = reviewerId,
                    ActorRole = reviewerRole,
                    Comment = "تم قبول واعتماد الحركة رسمياً من قسم التحكم والمراقبة.",
                    Timestamp = DateTime.UtcNow
                };
                await _db.FormApprovalHistories.AddAsync(history);

                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                // SignalR Broadcast
                await NotifyStatusChangeAsync("Transportation", trans.Id, trans.PONumber, txNumber, "APPROVED", reviewerName, null);

                var dto = MapTransportationToReviewItem(trans);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تم اعتماد حركة النقل بنجاح."));
            }
            else if (normType == "storage")
            {
                var storage = await _db.StorageTransactions.FirstOrDefaultAsync(s => s.Id == id);
                if (storage == null)
                {
                    await dbTransaction.RollbackAsync();
                    return NotFound(ApiResponse.Fail("Storage form not found."));
                }

                if (storage.ApprovalStatus == "APPROVED")
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("تم اعتماد حركة المستودع هذه بالفعل ولا يمكن إعادة معالجتها."));
                }

                prevStatus = storage.ApprovalStatus;
                poNumber = storage.PONumber ?? string.Empty;
                txNumber = $"#{storage.MovementNumber}";

                storage.ApprovalStatus = "APPROVED";
                storage.ReviewedBy = reviewerName;
                storage.ReviewedByUserId = reviewerId;
                storage.ReviewedAt = DateTime.UtcNow;
                storage.RejectionReason = null;

                // Add audit history
                var history = new FormApprovalHistory
                {
                    FormType = "Storage",
                    FormId = storage.Id,
                    PONumber = storage.PONumber,
                    TransactionOrMovementNumber = txNumber,
                    PreviousStatus = prevStatus,
                    NewStatus = "APPROVED",
                    Action = "APPROVED",
                    ActorName = reviewerName,
                    ActorUserId = reviewerId,
                    ActorRole = reviewerRole,
                    Comment = "تم قبول واعتماد حركة المستودع رسمياً من قسم التحكم والمراقبة.",
                    Timestamp = DateTime.UtcNow
                };
                await _db.FormApprovalHistories.AddAsync(history);

                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                // SignalR Broadcast
                await NotifyStatusChangeAsync("Storage", storage.Id, storage.PONumber, txNumber, "APPROVED", reviewerName, null);

                var dto = MapStorageToReviewItem(storage);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تم اعتماد حركة المستودع بنجاح."));
            }
            else
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(ApiResponse.Fail("Invalid form type."));
            }
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Error accepting form {FormType} #{Id}", formType, id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء اعتماد الحركة."));
        }
    }

    /// <summary>
    /// Reject a submitted Transportation or Storage form with optional rejection reason.
    /// </summary>
    [HttpPost("forms/{formType}/{id:int}/reject")]
    [ProducesResponseType(typeof(ApiResponse<ControlMonitoringReviewItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectForm(string formType, int id, [FromBody] RejectDecisionDto? body)
    {
        var normType = formType.Trim().ToLowerInvariant();
        var reviewerName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "موظف التحكم والمراقبة";
        Guid? reviewerId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            reviewerId = parsedGuid;
        }
        var reviewerRole = User.FindFirst(ClaimTypes.Role)?.Value ?? AppRoles.ControlMonitoring;
        var reason = body?.GetReason()?.Trim();

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            string poNumber = string.Empty;
            string txNumber = string.Empty;
            string prevStatus = "PENDING";

            if (normType == "transportation")
            {
                var trans = await _db.Transportations.FirstOrDefaultAsync(t => t.Id == id);
                if (trans == null)
                {
                    await dbTransaction.RollbackAsync();
                    return NotFound(ApiResponse.Fail("Transportation form not found."));
                }

                if (trans.ApprovalStatus == "APPROVED")
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("لا يمكن رفض طلب معتمد بالفعل."));
                }
                if (trans.ApprovalStatus == "REJECTED")
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("تم رفض هذا الطلب بالفعل ولا يمكن إعادة رفضه."));
                }

                prevStatus = trans.ApprovalStatus;
                poNumber = trans.PONumber;
                txNumber = $"#{trans.TransactionNumber}";

                trans.ApprovalStatus = "REJECTED";
                trans.TransactionStatus = "Rejected";
                trans.ReviewedBy = reviewerName;
                trans.ReviewedByUserId = reviewerId;
                trans.ReviewedAt = DateTime.UtcNow;
                trans.RejectionReason = reason;

                // Add audit history
                var history = new FormApprovalHistory
                {
                    FormType = "Transportation",
                    FormId = trans.Id,
                    PONumber = trans.PONumber,
                    TransactionOrMovementNumber = txNumber,
                    PreviousStatus = prevStatus,
                    NewStatus = "REJECTED",
                    Action = "REJECTED",
                    ActorName = reviewerName,
                    ActorUserId = reviewerId,
                    ActorRole = reviewerRole,
                    Comment = reason,
                    Timestamp = DateTime.UtcNow
                };
                await _db.FormApprovalHistories.AddAsync(history);

                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                // SignalR Broadcast
                await NotifyStatusChangeAsync("Transportation", trans.Id, trans.PONumber, txNumber, "REJECTED", reviewerName, reason);

                var dto = MapTransportationToReviewItem(trans);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تم رفض الحركة وحفظ سبب الرفض بنجاح."));
            }
            else if (normType == "storage")
            {
                var storage = await _db.StorageTransactions.FirstOrDefaultAsync(s => s.Id == id);
                if (storage == null)
                {
                    await dbTransaction.RollbackAsync();
                    return NotFound(ApiResponse.Fail("Storage form not found."));
                }

                if (storage.ApprovalStatus == "APPROVED")
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("لا يمكن رفض طلب معتمد بالفعل."));
                }
                if (storage.ApprovalStatus == "REJECTED")
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("تم رفض هذا الطلب بالفعل ولا يمكن إعادة رفضه."));
                }

                prevStatus = storage.ApprovalStatus;
                poNumber = storage.PONumber ?? string.Empty;
                txNumber = $"#{storage.MovementNumber}";

                storage.ApprovalStatus = "REJECTED";
                storage.ReviewedBy = reviewerName;
                storage.ReviewedByUserId = reviewerId;
                storage.ReviewedAt = DateTime.UtcNow;
                storage.RejectionReason = reason;

                // Add audit history
                var history = new FormApprovalHistory
                {
                    FormType = "Storage",
                    FormId = storage.Id,
                    PONumber = storage.PONumber,
                    TransactionOrMovementNumber = txNumber,
                    PreviousStatus = prevStatus,
                    NewStatus = "REJECTED",
                    Action = "REJECTED",
                    ActorName = reviewerName,
                    ActorUserId = reviewerId,
                    ActorRole = reviewerRole,
                    Comment = reason,
                    Timestamp = DateTime.UtcNow
                };
                await _db.FormApprovalHistories.AddAsync(history);

                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                // SignalR Broadcast
                await NotifyStatusChangeAsync("Storage", storage.Id, storage.PONumber, txNumber, "REJECTED", reviewerName, reason);

                var dto = MapStorageToReviewItem(storage);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تم رفض الحركة وحفظ سبب الرفض بنجاح."));
            }
            else
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(ApiResponse.Fail("Invalid form type."));
            }
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Error rejecting form {FormType} #{Id}", formType, id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء رفض الحركة."));
        }
    }

    /// <summary>
    /// Resubmit a rejected Transportation or Storage form.
    /// Resets status to PENDING and preserves full audit history.
    /// </summary>
    [HttpPost("forms/{formType}/{id:int}/resubmit")]
    [AllowAnonymous] // Allow authenticated callers or originating department
    [ProducesResponseType(typeof(ApiResponse<ControlMonitoringReviewItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResubmitForm(string formType, int id, [FromBody] ResubmitDecisionDto? body)
    {
        var normType = formType.Trim().ToLowerInvariant();
        var submitterName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "الموظف المسؤول";
        var submitterRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Employee";

        if (normType == "transportation")
        {
            var trans = await _db.Transportations.FirstOrDefaultAsync(t => t.Id == id);
            if (trans == null) return NotFound(ApiResponse.Fail("Transportation form not found."));

            var prevStatus = trans.ApprovalStatus;
            trans.ApprovalStatus = "PENDING";
            var comment = body?.GetComment()?.Trim();
            if (!string.IsNullOrWhiteSpace(comment))
            {
                trans.Notes = comment;
            }

            var history = new FormApprovalHistory
            {
                FormType = "Transportation",
                FormId = trans.Id,
                PONumber = trans.PONumber,
                TransactionOrMovementNumber = $"#{trans.TransactionNumber}",
                PreviousStatus = prevStatus,
                NewStatus = "PENDING",
                Action = "RESUBMITTED",
                ActorName = submitterName,
                ActorRole = submitterRole,
                Comment = comment ?? "تمت إعادة إرسال الحركة للمراجعة والتدقيق بعد التصحيح.",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(history);
            await _db.SaveChangesAsync();

            await NotifyStatusChangeAsync("Transportation", trans.Id, trans.PONumber, $"#{trans.TransactionNumber}", "PENDING", submitterName, null);

            var dto = MapTransportationToReviewItem(trans);
            return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تمت إعادة إرسال حركة النقل لقسم التحكم والمراقبة بنجاح."));
        }
        else if (normType == "storage")
        {
            var storage = await _db.StorageTransactions.FirstOrDefaultAsync(s => s.Id == id);
            if (storage == null) return NotFound(ApiResponse.Fail("Storage form not found."));

            var prevStatus = storage.ApprovalStatus;
            storage.ApprovalStatus = "PENDING";
            var comment = body?.GetComment()?.Trim();
            if (!string.IsNullOrWhiteSpace(comment))
            {
                storage.Notes = comment;
            }

            var history = new FormApprovalHistory
            {
                FormType = "Storage",
                FormId = storage.Id,
                PONumber = storage.PONumber,
                TransactionOrMovementNumber = $"#{storage.MovementNumber}",
                PreviousStatus = prevStatus,
                NewStatus = "PENDING",
                Action = "RESUBMITTED",
                ActorName = submitterName,
                ActorRole = submitterRole,
                Comment = comment ?? "تمت إعادة إرسال حركة المستودع للمراجعة والتدقيق بعد التصحيح.",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(history);
            await _db.SaveChangesAsync();

            await NotifyStatusChangeAsync("Storage", storage.Id, storage.PONumber, $"#{storage.MovementNumber}", "PENDING", submitterName, null);

            var dto = MapStorageToReviewItem(storage);
            return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تمت إعادة إرسال حركة المستودع لقسم التحكم والمراقبة بنجاح."));
        }
        else
        {
            return BadRequest(ApiResponse.Fail("Invalid form type."));
        }
    }

    /// <summary>
    /// Retrieve full audit trail for a form.
    /// </summary>
    [HttpGet("forms/{formType}/{id:int}/history")]
    [ProducesResponseType(typeof(ApiResponse<List<FormApprovalHistoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditHistory(string formType, int id)
    {
        var normType = formType.Trim().ToLowerInvariant();
        var history = await _db.FormApprovalHistories
            .AsNoTracking()
            .Where(h => h.FormType.ToLower() == normType && h.FormId == id)
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new FormApprovalHistoryDto
            {
                Id = h.Id,
                FormType = h.FormType,
                FormId = h.FormId,
                PONumber = h.PONumber,
                TransactionOrMovementNumber = h.TransactionOrMovementNumber,
                PreviousStatus = h.PreviousStatus,
                NewStatus = h.NewStatus,
                Action = h.Action,
                ActorName = h.ActorName,
                ActorRole = h.ActorRole,
                Comment = h.Comment,
                Timestamp = h.Timestamp
            })
            .ToListAsync();

        return Ok(ApiResponse<List<FormApprovalHistoryDto>>.Ok(history));
    }

    /// <summary>
    /// Stream / Download an attachment file from Transportation or Storage.
    /// Authorized Control & Monitoring employees have full permission to view/download.
    /// </summary>
    [HttpGet("attachments/{source}/{fileName}")]
    [HttpGet("/api/attachments/{source}/{fileName}")]
    [HttpGet("attachments/{fileName}")]
    [HttpGet("/api/attachments/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttachment(string? source, string fileName, [FromQuery] bool download = false)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            return BadRequest(ApiResponse.Fail("Invalid file name."));
        }

        var normSource = string.IsNullOrWhiteSpace(source) ? "" : source.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(normSource) && normSource != "transportation" && normSource != "storage")
        {
            // If source wasn't transportation or storage, assume it might be fileName with empty source
            normSource = "";
        }

        var (filePath, origName) = await ResolveAttachmentFileAsync(normSource, fileName);
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            _logger.LogWarning("Attachment file not found for source {Source}, file {File}", normSource, fileName);
            return NotFound(ApiResponse.Fail("Attachment file not found on server."));
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        string contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".txt" => "text/plain; charset=utf-8",
            ".csv" => "text/csv; charset=utf-8",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };

        var displayFileName = !string.IsNullOrWhiteSpace(origName) ? origName : Path.GetFileName(filePath);
        if (!Path.HasExtension(displayFileName) || string.IsNullOrEmpty(Path.GetExtension(displayFileName)))
        {
            displayFileName += ext;
        }

        var encodedName = Uri.EscapeDataString(displayFileName);

        if (download)
        {
            return PhysicalFile(filePath, contentType, fileDownloadName: displayFileName, enableRangeProcessing: true);
        }

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{encodedName}\"; filename*=UTF-8''{encodedName}";
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }

    private async Task<(string? FilePath, string? OriginalName)> ResolveAttachmentFileAsync(string normSource, string fileName)
    {
        var cleanName = Path.GetFileName(fileName);
        var otherSource = normSource == "transportation" ? "storage" : "transportation";

        // Candidate root paths to check for compatibility with Localhost & MonsterASP / IIS production
        var rootPaths = new List<string>
        {
            _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            _env.ContentRootPath,
            Directory.GetCurrentDirectory(),
            Path.Combine(_env.ContentRootPath, "wwwroot"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
        }.Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();

        // 1. Database reverse-lookup (find matching Transportation record & get original filename)
        if (normSource == "transportation" || string.IsNullOrEmpty(normSource))
        {
            var t = await _db.Transportations.AsNoTracking().FirstOrDefaultAsync(x =>
                x.WeighbridgeFileName == cleanName ||
                x.DailySupplyFileName == cleanName ||
                x.BillOfLadingFileName == cleanName ||
                x.InvoiceFileName == cleanName ||
                (x.WeighbridgeAttachmentPath != null && (x.WeighbridgeAttachmentPath == cleanName || x.WeighbridgeAttachmentPath.Contains(cleanName))) ||
                (x.DailySupplyAttachmentPath != null && (x.DailySupplyAttachmentPath == cleanName || x.DailySupplyAttachmentPath.Contains(cleanName))) ||
                (x.BillOfLadingAttachmentPath != null && (x.BillOfLadingAttachmentPath == cleanName || x.BillOfLadingAttachmentPath.Contains(cleanName))) ||
                (x.InvoiceAttachmentPath != null && (x.InvoiceAttachmentPath == cleanName || x.InvoiceAttachmentPath.Contains(cleanName))));

            if (t != null)
            {
                var candidateStored = new[]
                {
                    (t.WeighbridgeAttachmentPath, t.WeighbridgeFileName),
                    (t.DailySupplyAttachmentPath, t.DailySupplyFileName),
                    (t.BillOfLadingAttachmentPath, t.BillOfLadingFileName),
                    (t.InvoiceAttachmentPath, t.InvoiceFileName)
                };

                foreach (var (path, orig) in candidateStored)
                {
                    if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(orig)) continue;
                    var storedFn = !string.IsNullOrWhiteSpace(path) ? Path.GetFileName(path) : orig!;
                    foreach (var root in rootPaths)
                    {
                        var diskP = Path.Combine(root, "uploads", "transportation", storedFn);
                        if (System.IO.File.Exists(diskP)) return (diskP, orig);
                    }
                }
            }
        }

        // 2. Database reverse-lookup for Storage
        if (normSource == "storage" || string.IsNullOrEmpty(normSource))
        {
            var s = await _db.StorageTransactions.AsNoTracking().FirstOrDefaultAsync(x =>
                x.WeighbridgeFileName == cleanName ||
                (x.WeighbridgeAttachmentPath != null && (x.WeighbridgeAttachmentPath == cleanName || x.WeighbridgeAttachmentPath.Contains(cleanName))));

            if (s != null && (!string.IsNullOrWhiteSpace(s.WeighbridgeAttachmentPath) || !string.IsNullOrWhiteSpace(s.WeighbridgeFileName)))
            {
                var storedFn = !string.IsNullOrWhiteSpace(s.WeighbridgeAttachmentPath)
                    ? Path.GetFileName(s.WeighbridgeAttachmentPath)
                    : s.WeighbridgeFileName!;
                foreach (var root in rootPaths)
                {
                    var diskP = Path.Combine(root, "uploads", "storage", storedFn);
                    if (System.IO.File.Exists(diskP)) return (diskP, s.WeighbridgeFileName);
                }
            }
        }

        // 3. Direct disk check in normSource if specified
        if (!string.IsNullOrEmpty(normSource))
        {
            foreach (var root in rootPaths)
            {
                var p = Path.Combine(root, "uploads", normSource, cleanName);
                if (System.IO.File.Exists(p)) return (p, null);
            }
        }

        // 4. Direct disk check in otherSource
        foreach (var root in rootPaths)
        {
            var p = Path.Combine(root, "uploads", otherSource, cleanName);
            if (System.IO.File.Exists(p)) return (p, null);
        }

        // 5. Direct disk check in uploads root
        foreach (var root in rootPaths)
        {
            var p = Path.Combine(root, "uploads", cleanName);
            if (System.IO.File.Exists(p)) return (p, null);
        }

        return (null, null);
    }

    private long? TryGetFileSize(string source, string? storedPath, long? existingSize)
    {
        if (existingSize.HasValue && existingSize.Value > 0) return existingSize.Value;
        if (string.IsNullOrWhiteSpace(storedPath)) return null;
        var fn = Path.GetFileName(storedPath);
        var candidateRoots = new[]
        {
            _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            _env.ContentRootPath,
            Directory.GetCurrentDirectory(),
            Path.Combine(_env.ContentRootPath, "wwwroot"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
        };
        foreach (var root in candidateRoots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            var p = Path.Combine(root, "uploads", source, fn);
            if (System.IO.File.Exists(p))
            {
                try { return new FileInfo(p).Length; } catch { }
            }
        }
        return null;
    }

    // =========================================================================
    // HELPER MAPPERS
    // =========================================================================
    private ControlMonitoringReviewItemDto MapTransportationToReviewItem(Transportation t)
    {
        var attachments = new List<FormAttachmentDto>();
        var submitDate = t.DateOfSubmit != default ? t.DateOfSubmit : t.TransactionDate;
        var submitter = t.SubmittedBy ?? "موظف النقل";

        if (!string.IsNullOrWhiteSpace(t.WeighbridgeAttachmentPath) || !string.IsNullOrWhiteSpace(t.WeighbridgeFileName))
        {
            var fn = !string.IsNullOrWhiteSpace(t.WeighbridgeAttachmentPath)
                ? Path.GetFileName(t.WeighbridgeAttachmentPath)
                : t.WeighbridgeFileName!;
            var size = TryGetFileSize("transportation", fn, t.WeighbridgeFileSize);
            attachments.Add(new FormAttachmentDto
            {
                Key = "Weighbridge",
                Title = "إيصال الميزان (Weighbridge Receipt)",
                FileName = t.WeighbridgeFileName ?? fn,
                FileType = t.WeighbridgeFileType ?? (fn.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Image"),
                FileSize = size ?? 0L,
                ViewUrl = $"/api/control-monitoring/attachments/transportation/{fn}",
                DownloadUrl = $"/api/control-monitoring/attachments/transportation/{fn}?download=true",
                UploadedBy = submitter,
                UploadDate = submitDate
            });
        }

        if (!string.IsNullOrWhiteSpace(t.DailySupplyAttachmentPath) || !string.IsNullOrWhiteSpace(t.DailySupplyFileName))
        {
            var fn = !string.IsNullOrWhiteSpace(t.DailySupplyAttachmentPath)
                ? Path.GetFileName(t.DailySupplyAttachmentPath)
                : t.DailySupplyFileName!;
            var size = TryGetFileSize("transportation", fn, t.DailySupplyFileSize);
            attachments.Add(new FormAttachmentDto
            {
                Key = "DailySupply",
                Title = "يومية التوريد (Daily Supply Record)",
                FileName = t.DailySupplyFileName ?? fn,
                FileType = t.DailySupplyFileType ?? (fn.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Image"),
                FileSize = size ?? 0L,
                ViewUrl = $"/api/control-monitoring/attachments/transportation/{fn}",
                DownloadUrl = $"/api/control-monitoring/attachments/transportation/{fn}?download=true",
                UploadedBy = submitter,
                UploadDate = submitDate
            });
        }

        if (!string.IsNullOrWhiteSpace(t.BillOfLadingAttachmentPath) || !string.IsNullOrWhiteSpace(t.BillOfLadingFileName))
        {
            var fn = !string.IsNullOrWhiteSpace(t.BillOfLadingAttachmentPath)
                ? Path.GetFileName(t.BillOfLadingAttachmentPath)
                : t.BillOfLadingFileName!;
            var size = TryGetFileSize("transportation", fn, t.BillOfLadingFileSize);
            attachments.Add(new FormAttachmentDto
            {
                Key = "BillOfLading",
                Title = "بوليصة الشحن (Bill of Lading)",
                FileName = t.BillOfLadingFileName ?? fn,
                FileType = t.BillOfLadingFileType ?? (fn.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Image"),
                FileSize = size ?? 0L,
                ViewUrl = $"/api/control-monitoring/attachments/transportation/{fn}",
                DownloadUrl = $"/api/control-monitoring/attachments/transportation/{fn}?download=true",
                UploadedBy = submitter,
                UploadDate = submitDate
            });
        }

        if (!string.IsNullOrWhiteSpace(t.InvoiceAttachmentPath) || !string.IsNullOrWhiteSpace(t.InvoiceFileName))
        {
            var fn = !string.IsNullOrWhiteSpace(t.InvoiceAttachmentPath)
                ? Path.GetFileName(t.InvoiceAttachmentPath)
                : t.InvoiceFileName!;
            var size = TryGetFileSize("transportation", fn, t.InvoiceFileSize);
            attachments.Add(new FormAttachmentDto
            {
                Key = "Invoice",
                Title = "مرفق الفاتورة (Invoice Attachment)",
                FileName = t.InvoiceFileName ?? fn,
                FileType = t.InvoiceFileType ?? (fn.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Image"),
                FileSize = size ?? 0L,
                ViewUrl = $"/api/control-monitoring/attachments/transportation/{fn}",
                DownloadUrl = $"/api/control-monitoring/attachments/transportation/{fn}?download=true",
                UploadedBy = submitter,
                UploadDate = submitDate
            });
        }

        return new ControlMonitoringReviewItemDto
        {
            Id = t.Id,
            FormType = "Transportation",
            FormId = t.Id,
            PONumber = t.PONumber,
            TransactionNumber = $"#{t.TransactionNumber}",
            SubmittedBy = submitter,
            SubmittedByUserId = t.SubmittedByUserId,
            SubmissionDate = submitDate,
            Status = string.IsNullOrWhiteSpace(t.ApprovalStatus) ? "APPROVED" : t.ApprovalStatus,
            ReviewedBy = t.ReviewedBy,
            ReviewedAt = t.ReviewedAt,
            RejectionReason = t.RejectionReason,
            AttachmentCount = attachments.Count,
            Attachments = attachments,

            // Transportation specifics
            CarNumber = t.CarNumber,
            DriverName = t.DriverName,
            DriverPhone = t.DriverPhone,
            Source = t.Source,
            Destination = t.Destination,
            CompanyName = t.CompanyName,
            MaterialType = t.MaterialType,
            ExpectedWeight = t.ExpectedWeight,
            WeightBeforeLoading = t.WeightBeforeLoading,
            WeightWhenEnteringFactory = t.WeightWhenEnteringFactory,
            WeightWhenExitingFactory = t.WeightWhenExitingFactory,
            InternalWeight = t.InternalWeight,
            ExternalLoadedWeight = t.ExternalLoadedWeight,
            ExternalEmptyWeight = t.ExternalEmptyWeight,
            ExternalNetWeight = t.ExternalNetWeight,
            NetWeight = t.NetWeight,
            WasteQuantity = t.WasteQuantity,
            DepartureTime = t.DepartureTime,
            ArrivalTime = t.ArrivalTime,
            ShippingDurationMinutes = t.ShippingDurationMinutes,
            ShippingSupervisor = t.ShippingSupervisor,
            DepartmentSupervisor = t.DepartmentSupervisor,
            RecipientName = t.RecipientName,
            RecipientTitle = t.RecipientTitle,
            ReceivedDate = t.ReceivedDate,
            InternalWeighbridgeCode = t.InternalWeighbridgeCode,
            ExternalWeighbridgeCode = t.ExternalWeighbridgeCode,
            InternalEmptyWeightTime = t.InternalEmptyWeightTime,
            InternalLoadedWeightTime = t.InternalLoadedWeightTime,
            ExternalEmptyWeightTime = t.ExternalEmptyWeightTime,
            ExternalLoadedWeightTime = t.ExternalLoadedWeightTime,
            Ikramiya = t.Ikramiya,
            CustodyAmount = t.CustodyAmount,
            ReceivingStatus = t.ReceivingStatus,
            TransactionStatus = t.TransactionStatus,
            Notes = t.Notes
        };
    }

    private ControlMonitoringReviewItemDto MapStorageToReviewItem(StorageTransaction s)
    {
        var attachments = new List<FormAttachmentDto>();
        var submitDate = s.CreatedAt != default ? s.CreatedAt : s.TransactionDate;
        var submitter = s.CreatedBy ?? s.ReceivingEmployee ?? "موظف المخزن";

        if (!string.IsNullOrWhiteSpace(s.WeighbridgeAttachmentPath) || !string.IsNullOrWhiteSpace(s.WeighbridgeFileName))
        {
            var fn = !string.IsNullOrWhiteSpace(s.WeighbridgeAttachmentPath)
                ? Path.GetFileName(s.WeighbridgeAttachmentPath)
                : s.WeighbridgeFileName!;
            var size = TryGetFileSize("storage", fn, s.WeighbridgeFileSize);
            attachments.Add(new FormAttachmentDto
            {
                Key = "Weighbridge",
                Title = "إيصال الميزان (Weighbridge Receipt)",
                FileName = s.WeighbridgeFileName ?? fn,
                FileType = s.WeighbridgeFileType ?? (fn.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Image"),
                FileSize = size ?? 0L,
                ViewUrl = $"/api/control-monitoring/attachments/storage/{fn}",
                DownloadUrl = $"/api/control-monitoring/attachments/storage/{fn}?download=true",
                UploadedBy = submitter,
                UploadDate = submitDate
            });
        }

        return new ControlMonitoringReviewItemDto
        {
            Id = s.Id,
            FormType = "Storage",
            FormId = s.Id,
            PONumber = s.PONumber,
            TransactionNumber = $"#{s.MovementNumber}",
            SubmittedBy = submitter,
            SubmissionDate = submitDate,
            Status = string.IsNullOrWhiteSpace(s.ApprovalStatus) ? "APPROVED" : s.ApprovalStatus,
            ReviewedBy = s.ReviewedBy,
            ReviewedAt = s.ReviewedAt,
            RejectionReason = s.RejectionReason,
            AttachmentCount = attachments.Count,
            Attachments = attachments,

            // Storage specifics
            ReferenceNumber = s.ReferenceNumber,
            IsExportRelatedToPO = s.IsExportRelatedToPO,
            Material = s.Material,
            Action = s.Action,
            ServiceType = s.ServiceType,
            IncomingQuantity = s.IncomingQuantity,
            OutgoingQuantity = s.OutgoingQuantity,
            OpeningBalance = s.OpeningBalance,
            ClosingBalance = s.ClosingBalance,
            StorageLocation = s.StorageLocation,
            DestinationStorageLocation = s.DestinationStorageLocation,
            InternalWeighbridgeReference = s.InternalWeighbridgeReference,
            StorageInternalLoadedWeight = s.InternalLoadedWeight,
            StorageInternalEmptyWeight = s.InternalEmptyWeight,
            StorageInternalNetWeight = s.InternalNetWeight,
            StorageInternalEmptyWeightTime = s.InternalEmptyWeightTime,
            StorageInternalLoadedWeightTime = s.InternalLoadedWeightTime,
            HasExternalWeighbridge = s.HasExternalWeighbridge,
            ExternalWeighbridgeReference = s.ExternalWeighbridgeReference,
            StorageExternalLoadedWeight = s.ExternalLoadedWeight,
            StorageExternalEmptyWeight = s.ExternalEmptyWeight,
            StorageExternalNetWeight = s.ExternalNetWeight,
            StorageExternalEmptyWeightTime = s.ExternalEmptyWeightTime,
            StorageExternalLoadedWeightTime = s.ExternalLoadedWeightTime,
            ResponsibleEmployee = s.ReceivingEmployee,
            CreatedBy = s.CreatedBy,
            CreatedAt = s.CreatedAt,
            Notes = s.Notes
        };
    }

    private async Task NotifyStatusChangeAsync(
        string formType, int formId, string? po, string txNumber, string status, string reviewer, string? reason)
    {
        try
        {
            var payload = new
            {
                department = "control-monitoring",
                action = status,
                formType,
                formId,
                po,
                txNumber,
                status,
                reviewer,
                reason,
                timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", payload);

            if (status == "APPROVED")
            {
                await _hubContext.Clients.All.SendAsync("OnFormApproved", payload);
            }
            else if (status == "REJECTED")
            {
                await _hubContext.Clients.All.SendAsync("OnFormRejected", payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast SignalR notification for {FormType} #{Id}", formType, formId);
        }
    }
}
