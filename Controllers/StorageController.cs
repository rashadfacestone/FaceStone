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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FaceStone.Web.Data;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;

namespace FaceStone.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "StorageAccess")]
[Produces("application/json")]
public class StorageController : ControllerBase
{
    private readonly FaceStoneDbContext _db;
    private readonly ILogger<StorageController> _logger;
    private readonly IWebHostEnvironment _env;

    public StorageController(
        FaceStoneDbContext db, 
        ILogger<StorageController> logger,
        IWebHostEnvironment env)
    {
        _db = db;
        _logger = logger;
        _env = env;
    }

    /// <summary>
    /// Supported Materials matching FaceStone project standard: powder and slurry
    /// </summary>
    public static readonly List<string> MaterialTypes = new()
    {
        "powder",
        "slurry"
    };

    /// <summary>
    /// Supported Actions ONLY: import (إيداع) and export (صرف)
    /// </summary>
    public static readonly List<string> ActionTypes = new()
    {
        "import",
        "export"
    };

    /// <summary>
    /// Retrieve configuration options: Material list, Action list, eligible Employees, next movement number.
    /// </summary>
    [HttpGet("config")]
    [ProducesResponseType(typeof(ApiResponse<StorageConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig()
    {
        // 1. Fetch eligible employees from Users table
        var employees = await _db.Users
            .AsNoTracking()
            .Where(u => u.Role == AppRoles.StorageManager
                     || u.Role == AppRoles.StorageManagerCode
                     || u.Role == AppRoles.StorageManagerDisplay
                     || u.Role == AppRoles.FactoryManager
                     || u.Role == AppRoles.FactoryManagerDisplay
                     || u.Role == AppRoles.CEO
                     || u.Role == AppRoles.IT)
            .OrderBy(u => u.FullName)
            .Select(u => new EmployeeSummaryDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Username = u.Username,
                Role = u.Role
            })
            .ToListAsync();

        // Fallback: If no users with storage roles are found, provide active users
        if (employees.Count == 0)
        {
            employees = await _db.Users
                .AsNoTracking()
                .OrderBy(u => u.FullName)
                .Select(u => new EmployeeSummaryDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Username = u.Username,
                    Role = u.Role
                })
                .ToListAsync();
        }

        var maxMovement = await _db.StorageTransactions.MaxAsync(s => (int?)s.MovementNumber) ?? 5000;
        var nextMovement = maxMovement + 1;

        var config = new StorageConfigDto
        {
            Materials = MaterialTypes,
            Actions = ActionTypes,
            Employees = employees,
            NextMovementNumber = nextMovement
        };

        return Ok(ApiResponse<StorageConfigDto>.Ok(config));
    }

    /// <summary>
    /// Validates a Purchase Order for Storage Export workflow.
    /// </summary>
    [HttpGet("po/{poNumber}")]
    [HttpGet("po-lookup/{poNumber}")]
    [ProducesResponseType(typeof(ApiResponse<StoragePoLookupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LookupPurchaseOrder(string poNumber)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
        {
            return BadRequest(ApiResponse.Fail("رقم أمر الشراء مطلوب."));
        }

        var trimmed = poNumber.Trim();
        var po = await _db.PurchaseOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PONumber.ToLower() == trimmed.ToLower());

        if (po == null)
        {
            _logger.LogWarning("PO lookup failed for Storage: '{PONumber}' not found.", trimmed);
            return NotFound(ApiResponse.Fail("Purchase Order not found."));
        }

        var dto = new StoragePoLookupDto
        {
            Id = po.Id,
            PoNumber = po.PONumber,
            Supplier = po.Supplier,
            Quantity = po.Quantity,
            RemainingQuantity = po.RemainingQuantity,
            OrderDate = po.OrderDate,
            Notes = po.Notes
        };

        return Ok(ApiResponse<StoragePoLookupDto>.Ok(dto));
    }

    /// <summary>
    /// Check if internal weighbridge reference already exists.
    /// </summary>
    [HttpGet("check-reference/{referenceNumber}")]
    [ProducesResponseType(typeof(ApiResponse<CheckReferenceResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckReferenceNumber(string referenceNumber)
    {
        if (string.IsNullOrWhiteSpace(referenceNumber))
        {
            return Ok(ApiResponse<CheckReferenceResultDto>.Ok(new CheckReferenceResultDto { ReferenceNumber = "", Exists = false }));
        }

        var trimmed = referenceNumber.Trim();
        var exists = await _db.StorageTransactions
            .AsNoTracking()
            .AnyAsync(s => s.InternalWeighbridgeReference.ToLower() == trimmed.ToLower());

        // Also check legacy ReferenceNumber if integer
        if (!exists && int.TryParse(trimmed, out var intRef))
        {
            exists = await _db.StorageTransactions
                .AsNoTracking()
                .AnyAsync(s => s.ReferenceNumber == intRef);
        }

        var result = new CheckReferenceResultDto
        {
            ReferenceNumber = trimmed,
            Exists = exists,
            Message = exists ? "رقم المرجع مستخدم بالفعل" : null
        };

        return Ok(ApiResponse<CheckReferenceResultDto>.Ok(result));
    }

    /// <summary>
    /// Retrieve recent storage transactions.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(ApiResponse<List<StorageTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentTransactions([FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 100);

        var transactions = await _db.StorageTransactions
            .AsNoTracking()
            .OrderByDescending(s => s.TransactionDate)
            .ThenByDescending(s => s.Id)
            .Take(limit)
            .ToListAsync();

        var dtos = transactions.Select(MapToDto).ToList();
        return Ok(ApiResponse<List<StorageTransactionDto>>.Ok(dtos));
    }

    /// <summary>
    /// Retrieve single storage transaction by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<StorageTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var transaction = await _db.StorageTransactions.FindAsync(id);
        if (transaction == null)
        {
            return NotFound(ApiResponse.Fail($"حركة المخزن #{id} غير موجودة."));
        }

        return Ok(ApiResponse<StorageTransactionDto>.Ok(MapToDto(transaction)));
    }

    /// <summary>
    /// Stream / Download weighbridge receipt attachment.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("attachments/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetAttachmentFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return BadRequest(ApiResponse.Fail("اسم الملف غير صالح."));
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var filePath = Path.Combine(webRoot, "uploads", "storage", fileName);

        if (!System.IO.File.Exists(filePath))
        {
            // Fallback 1: check if a transaction exists with this original filename
            var matchTx = _db.StorageTransactions
                .AsNoTracking()
                .Where(s => s.WeighbridgeFileName == fileName && s.WeighbridgeAttachmentPath != null)
                .OrderByDescending(s => s.Id)
                .FirstOrDefault();

            if (matchTx != null && !string.IsNullOrWhiteSpace(matchTx.WeighbridgeAttachmentPath))
            {
                var storedName = Path.GetFileName(matchTx.WeighbridgeAttachmentPath);
                var matchedPath = Path.Combine(webRoot, "uploads", "storage", storedName);
                if (System.IO.File.Exists(matchedPath))
                {
                    var mExt = Path.GetExtension(storedName).ToLowerInvariant();
                    var mContentType = mExt == ".pdf" ? "application/pdf" : "image/jpeg";
                    return PhysicalFile(matchedPath, mContentType, enableRangeProcessing: true);
                }
            }

            // Fallback 2: check transportation uploads
            var transFilePath = Path.Combine(webRoot, "uploads", "transportation", fileName);
            if (System.IO.File.Exists(transFilePath))
            {
                var extT = Path.GetExtension(fileName).ToLowerInvariant();
                var cTypeT = extT == ".pdf" ? "application/pdf" : "image/jpeg";
                return PhysicalFile(transFilePath, cTypeT, enableRangeProcessing: true);
            }
            return NotFound(ApiResponse.Fail("الملف غير موجود."));
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = ext == ".pdf" ? "application/pdf" : "image/jpeg";
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// Submit a new Storage Transaction (JSON format).
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<StorageTransactionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateJson([FromBody] CreateStorageTransactionDto dto)
    {
        return await ProcessCreateAsync(dto);
    }

    /// <summary>
    /// Submit a new Storage Transaction (multipart/form-data format with optional/required attachment).
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<StorageTransactionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateForm([FromForm] CreateStorageTransactionDto dto)
    {
        return await ProcessCreateAsync(dto);
    }

    private async Task<(bool Success, string? ErrorMessage, string? StoredFileName, string? OriginalFileName, string? ContentType, long FileSize)> ValidateAndSaveAttachmentAsync(
        IFormFile? file, string? fallbackPath, bool isRequired)
    {
        if (file == null || file.Length == 0)
        {
            // If fallback existing path is provided (for API/integration tests)
            if (!string.IsNullOrWhiteSpace(fallbackPath))
            {
                var webRootF = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var existingF = Path.Combine(webRootF, "uploads", "storage", Path.GetFileName(fallbackPath));
                if (System.IO.File.Exists(existingF))
                {
                    var extF = Path.GetExtension(fallbackPath).ToLowerInvariant();
                    return (true, null, Path.GetFileName(fallbackPath), Path.GetFileName(fallbackPath), extF == ".pdf" ? "application/pdf" : "image/jpeg", new FileInfo(existingF).Length);
                }
            }

            if (isRequired)
            {
                return (false, "إرفاق إيصال الميزان مطلوب في حالة وجود ميزان خارجي.", null, null, null, 0);
            }
            return (true, null, null, null, null, 0);
        }

        const long maxFileSize = 25 * 1024 * 1024; // 25 MB
        if (file.Length > maxFileSize)
        {
            return (false, "حجم ملف إيصال الميزان يتجاوز الحد الأقصى المسموح به (25 ميجابايت).", null, null, null, 0);
        }

        var fileExt = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (fileExt != ".pdf" && fileExt != ".jpg" && fileExt != ".jpeg")
        {
            return (false, "نوع ملف إيصال الميزان غير مدعوم. الملفات المسموحة فقط: PDF, JPG, JPEG.", null, null, null, 0);
        }

        var mime = (file.ContentType ?? string.Empty).ToLowerInvariant();
        var validMimes = new[] { "application/pdf", "image/jpeg", "image/pjpeg", "image/jpg" };
        if (!validMimes.Contains(mime))
        {
            return (false, "نوع ملف إيصال الميزان غير مدعوم.", null, null, null, 0);
        }

        // Magic bytes inspection
        byte[] magicBytes = new byte[8];
        using (var stream = file.OpenReadStream())
        {
            var bytesRead = await stream.ReadAsync(magicBytes, 0, magicBytes.Length);
            if (bytesRead < 4)
            {
                return (false, "محتوى ملف إيصال الميزان تالف أو غير صالح.", null, null, null, 0);
            }

            if (fileExt == ".pdf")
            {
                if (magicBytes[0] != 0x25 || magicBytes[1] != 0x50 || magicBytes[2] != 0x44 || magicBytes[3] != 0x46)
                {
                    return (false, "محتوى ملف PDF لإيصال الميزان غير صالح.", null, null, null, 0);
                }
            }
            else
            {
                if (magicBytes[0] != 0xFF || magicBytes[1] != 0xD8 || magicBytes[2] != 0xFF)
                {
                    return (false, "محتوى صورة JPG لإيصال الميزان غير صالح.", null, null, null, 0);
                }
            }
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "storage");
        if (!Directory.Exists(uploadsDir))
        {
            Directory.CreateDirectory(uploadsDir);
        }

        var uniqueFileName = $"{Guid.NewGuid():N}{fileExt}";
        var destinationPath = Path.Combine(uploadsDir, uniqueFileName);

        using (var destStream = new FileStream(destinationPath, FileMode.Create))
        {
            await file.CopyToAsync(destStream);
        }

        return (true, null, uniqueFileName, Path.GetFileName(file.FileName), file.ContentType, file.Length);
    }

    private void CleanupSavedFile(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return;
        try
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var p = Path.Combine(webRoot, "uploads", "storage", fileName);
            if (System.IO.File.Exists(p))
            {
                System.IO.File.Delete(p);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up file {FileName}", fileName);
        }
    }

    private async Task<IActionResult> ProcessCreateAsync(CreateStorageTransactionDto dto)
    {
        // 1. Role Authorization
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasStorageAccess(role))
        {
            _logger.LogWarning("Unauthorized user with role '{Role}' attempted to submit storage transaction.", role);
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("Access Denied: Storage department is restricted to CEO, IT, and Storage Manager."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات الحركة غير صالحة."));
        }

        // =========================================================================
        // SECTION 1 VALIDATION — MATERIAL & ACTION (النوع)
        // =========================================================================

        // 1.1 Material validation
        var material = (dto.Material ?? string.Empty).Trim().ToLowerInvariant();
        if (material != "powder" && material != "slurry")
        {
            return BadRequest(ApiResponse.Fail("نوع الخامة غير صحيح. الخيارات المتاحة فقط: powder أو slurry."));
        }

        // 1.2 Action validation
        var rawAction = (dto.Action ?? string.Empty).Trim().ToLowerInvariant();
        string normalizedAction;
        if (rawAction == "import" || rawAction == "إيداع")
        {
            normalizedAction = "Import";
        }
        else if (rawAction == "export" || rawAction == "صرف")
        {
            normalizedAction = "Export";
        }
        else
        {
            return BadRequest(ApiResponse.Fail("نوع الحركة غير صحيح. الخيارات المتاحة فقط: إيداع (Import) أو صرف (Export)."));
        }

        // 1.3 Quantity validation according to Action
        decimal incomingQty = 0m;
        decimal outgoingQty = 0m;

        if (normalizedAction == "Import")
        {
            // Import: IncomingQuantity is required, OutgoingQuantity must NOT be entered
            if (!dto.IncomingQuantity.HasValue || dto.IncomingQuantity.Value <= 0)
            {
                return BadRequest(ApiResponse.Fail("الكمية الواردة مطلوبة ويجب أن تكون أكبر من الصفر في حالة الإيداع."));
            }

            if (dto.OutgoingQuantity.HasValue && dto.OutgoingQuantity.Value > 0)
            {
                return BadRequest(ApiResponse.Fail("لا يمكن إدخال كمية صادرة في حالة حركة الإيداع."));
            }

            incomingQty = dto.IncomingQuantity.Value;
            outgoingQty = 0m;
        }
        else // Export
        {
            // Export: OutgoingQuantity is required, IncomingQuantity must NOT be entered
            if (!dto.OutgoingQuantity.HasValue || dto.OutgoingQuantity.Value <= 0)
            {
                return BadRequest(ApiResponse.Fail("الكمية الصادرة مطلوبة ويجب أن تكون أكبر من الصفر في حالة الصرف."));
            }

            if (dto.IncomingQuantity.HasValue && dto.IncomingQuantity.Value > 0)
            {
                return BadRequest(ApiResponse.Fail("لا يمكن إدخال كمية واردة في حالة حركة الصرف."));
            }

            outgoingQty = dto.OutgoingQuantity.Value;
            incomingQty = 0m;
        }

        // 1.4 Purchase Order Validation for Export
        int? purchaseOrderId = null;
        string? poNumber = null;

        if (normalizedAction == "Export" && dto.IsExportRelatedToPO)
        {
            if (string.IsNullOrWhiteSpace(dto.PONumber))
            {
                return BadRequest(ApiResponse.Fail("رقم أمر الشراء مطلوب عند تحديد أن الصادر مربوط بأمر شراء."));
            }

            var trimmedPo = dto.PONumber.Trim();
            var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.PONumber.ToLower() == trimmedPo.ToLower());

            if (po == null)
            {
                _logger.LogWarning("Storage export transaction rejected: PO '{PONumber}' not found in database.", trimmedPo);
                return BadRequest(ApiResponse.Fail("Purchase Order not found."));
            }

            purchaseOrderId = po.Id;
            poNumber = po.PONumber;
        }

        // =========================================================================
        // SECTION 2 VALIDATION — DATE (التاريخ)
        // =========================================================================
        if (!dto.TransactionDate.HasValue)
        {
            return BadRequest(ApiResponse.Fail("تاريخ ووقت الحركة مطلوب."));
        }
        var transactionDateTime = dto.TransactionDate.Value;

        // =========================================================================
        // SECTION 3 VALIDATION — WEIGHBRIDGE (الميزان)
        // =========================================================================

        // 3.1 Internal Weighbridge
        if (string.IsNullOrWhiteSpace(dto.InternalWeighbridgeReference))
        {
            return BadRequest(ApiResponse.Fail("رقم مرجع الميزان الداخلي مطلوب ويجب إدخاله يدوياً."));
        }
        var internalRef = dto.InternalWeighbridgeReference.Trim();

        if (dto.InternalLoadedWeight <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الداخلي قائم يجب أن يكون أكبر من الصفر."));
        }
        if (dto.InternalEmptyWeight <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الداخلي فارغ يجب أن يكون أكبر من الصفر."));
        }
        if (dto.InternalLoadedWeight <= dto.InternalEmptyWeight)
        {
            return BadRequest(ApiResponse.Fail("الوزن الداخلي قائم يجب أن يكون أكبر من الوزن الداخلي فارغ."));
        }

        if (!dto.InternalLoadedWeightTime.HasValue)
        {
            return BadRequest(ApiResponse.Fail("تاريخ ووقت الوزن القائم الداخلي مطلوب."));
        }

        var internalNetWeight = Math.Round(dto.InternalLoadedWeight - dto.InternalEmptyWeight, 3);
        if (internalNetWeight <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الداخلي الصافي المحسوب يجب أن يكون أكبر من الصفر."));
        }

        // 3.2 External Weighbridge
        decimal? externalLoaded = null;
        decimal? externalEmpty = null;
        decimal? externalNet = null;
        DateTime? externalEmptyTime = null;
        DateTime? externalLoadedTime = null;
        string? externalRef = null;

        if (dto.HasExternalWeighbridge)
        {
            if (string.IsNullOrWhiteSpace(dto.ExternalWeighbridgeReference))
            {
                return BadRequest(ApiResponse.Fail("رقم مرجع الميزان الخارجي مطلوب عند تفعيل الميزان الخارجي."));
            }
            externalRef = dto.ExternalWeighbridgeReference.Trim();

            if (!dto.ExternalLoadedWeight.HasValue || dto.ExternalLoadedWeight.Value <= 0)
            {
                return BadRequest(ApiResponse.Fail("الوزن الخارجي قائم يجب أن يكون أكبر من الصفر عند تفعيل الميزان الخارجي."));
            }
            if (!dto.ExternalEmptyWeight.HasValue || dto.ExternalEmptyWeight.Value <= 0)
            {
                return BadRequest(ApiResponse.Fail("الوزن الخارجي فارغ يجب أن يكون أكبر من الصفر عند تفعيل الميزان الخارجي."));
            }
            if (dto.ExternalLoadedWeight.Value <= dto.ExternalEmptyWeight.Value)
            {
                return BadRequest(ApiResponse.Fail("الوزن الخارجي قائم يجب أن يكون أكبر من الوزن الخارجي فارغ."));
            }

            if (!dto.ExternalLoadedWeightTime.HasValue)
            {
                return BadRequest(ApiResponse.Fail("تاريخ ووقت الوزن القائم الخارجي مطلوب عند تفعيل الميزان الخارجي."));
            }

            externalLoaded = dto.ExternalLoadedWeight.Value;
            externalEmpty = dto.ExternalEmptyWeight.Value;
            externalNet = Math.Round(externalLoaded.Value - externalEmpty.Value, 3);
            externalEmptyTime = dto.ExternalEmptyWeightTime;
            externalLoadedTime = dto.ExternalLoadedWeightTime;

            if (externalNet.Value <= 0)
            {
                return BadRequest(ApiResponse.Fail("الوزن الخارجي الصافي المحسوب يجب أن يكون أكبر من الصفر."));
            }
        }

        // 3.3 Weighbridge Receipt Attachment Validation
        // Required if external weighbridge is enabled
        var isAttachmentRequired = dto.HasExternalWeighbridge;
        var attRes = await ValidateAndSaveAttachmentAsync(dto.WeighbridgeAttachment, dto.WeighbridgeAttachmentPath, isAttachmentRequired);
        if (!attRes.Success)
        {
            return BadRequest(ApiResponse.Fail(attRes.ErrorMessage ?? "خطأ في التحقق من مرفق إيصال الميزان."));
        }

        // =========================================================================
        // SECTION 4 VALIDATION — RESPONSIBLE EMPLOYEE (الموظف المسؤول)
        // =========================================================================
        if (string.IsNullOrWhiteSpace(dto.ResponsibleEmployee))
        {
            CleanupSavedFile(attRes.StoredFileName);
            return BadRequest(ApiResponse.Fail("الموظف المسؤول مطلوب."));
        }

        var responsibleEmp = dto.ResponsibleEmployee.Trim();
        var empExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.FullName.ToLower() == responsibleEmp.ToLower() || u.Username.ToLower() == responsibleEmp.ToLower());

        if (!empExists)
        {
            CleanupSavedFile(attRes.StoredFileName);
            return BadRequest(ApiResponse.Fail("الموظف المسؤول المحدد غير موجود في قاعدة المستخدمين المعتمدة."));
        }

        // Generate movement number and reference number for backwards compatibility
        var maxMovement = await _db.StorageTransactions.MaxAsync(s => (int?)s.MovementNumber) ?? 5000;
        int movementNumber = (dto.MovementNumber.HasValue && dto.MovementNumber.Value > 0)
            ? dto.MovementNumber.Value
            : maxMovement + 1;

        var maxRef = await _db.StorageTransactions.MaxAsync(s => (int?)s.ReferenceNumber) ?? 7000;
        int legacyReferenceNumber = maxRef + 1;

        // Caller identity
        var callerName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "موظف المخزن";

        // Build entity
        var transaction = new StorageTransaction
        {
            MovementNumber = movementNumber,
            TransactionDate = transactionDateTime,
            Material = material,
            Action = normalizedAction,
            ServiceType = normalizedAction == "Import" ? "إيداع" : "صرف",
            ReferenceNumber = legacyReferenceNumber,
            IsExportRelatedToPO = (normalizedAction == "Export" && dto.IsExportRelatedToPO),
            PONumber = poNumber,
            PurchaseOrderId = purchaseOrderId,
            IncomingQuantity = incomingQty,
            OutgoingQuantity = outgoingQty,
            OpeningBalance = 0m,
            ClosingBalance = incomingQty - outgoingQty,
            StorageLocation = "مستودع الخامات الرئيسي",

            // Weighbridge
            InternalWeighbridgeReference = internalRef,
            InternalLoadedWeight = dto.InternalLoadedWeight,
            InternalEmptyWeight = dto.InternalEmptyWeight,
            InternalNetWeight = internalNetWeight,
            InternalEmptyWeightTime = dto.InternalEmptyWeightTime,
            InternalLoadedWeightTime = dto.InternalLoadedWeightTime,

            HasExternalWeighbridge = dto.HasExternalWeighbridge,
            ExternalWeighbridgeReference = externalRef,
            ExternalLoadedWeight = externalLoaded,
            ExternalEmptyWeight = externalEmpty,
            ExternalNetWeight = externalNet,
            ExternalEmptyWeightTime = externalEmptyTime,
            ExternalLoadedWeightTime = externalLoadedTime,

            // Attachment
            WeighbridgeAttachmentPath = !string.IsNullOrWhiteSpace(attRes.StoredFileName) ? $"/api/storage/attachments/{attRes.StoredFileName}" : null,
            WeighbridgeFileName = attRes.OriginalFileName,
            WeighbridgeFileType = attRes.ContentType,
            WeighbridgeFileSize = attRes.FileSize,

            // Responsible Employee
            ReceivingEmployee = responsibleEmp,
            Notes = dto.Notes?.Trim(),
            CreatedBy = callerName,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _db.StorageTransactions.AddAsync(transaction);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Storage Transaction #{MovementNumber} created successfully. Action={Action}, Material={Material}",
                transaction.MovementNumber, transaction.Action, transaction.Material);

            var resultDto = MapToDto(transaction);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<StorageTransactionDto>.Ok(resultDto, "تم حفظ حركة المخزن بنجاح"));
        }
        catch (Exception ex)
        {
            CleanupSavedFile(attRes.StoredFileName);
            _logger.LogError(ex, "Error persisting storage transaction.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("حدث خطأ أثناء حفظ حركة المخزن في قاعدة البيانات."));
        }
    }

    private static StorageTransactionDto MapToDto(StorageTransaction s)
    {
        return new StorageTransactionDto
        {
            Id = s.Id,
            MovementNumber = s.MovementNumber,
            TransactionDate = s.TransactionDate,
            Material = s.Material,
            Action = s.Action,
            ServiceType = s.ServiceType,
            IncomingQuantity = (s.Action == "Export" || s.ServiceType == "صرف") ? null : s.IncomingQuantity,
            OutgoingQuantity = (s.Action == "Import" || s.ServiceType == "إيداع" || s.ServiceType == "إضافة") ? null : s.OutgoingQuantity,
            IsExportRelatedToPO = s.IsExportRelatedToPO,
            PONumber = s.PONumber,
            PurchaseOrderId = s.PurchaseOrderId,

            // Weighbridge
            InternalWeighbridgeReference = s.InternalWeighbridgeReference,
            InternalLoadedWeight = s.InternalLoadedWeight,
            InternalEmptyWeight = s.InternalEmptyWeight,
            InternalNetWeight = s.InternalNetWeight,
            InternalEmptyWeightTime = s.InternalEmptyWeightTime,
            InternalLoadedWeightTime = s.InternalLoadedWeightTime,

            HasExternalWeighbridge = s.HasExternalWeighbridge,
            ExternalWeighbridgeReference = s.ExternalWeighbridgeReference,
            ExternalLoadedWeight = s.ExternalLoadedWeight,
            ExternalEmptyWeight = s.ExternalEmptyWeight,
            ExternalNetWeight = s.ExternalNetWeight,
            ExternalEmptyWeightTime = s.ExternalEmptyWeightTime,
            ExternalLoadedWeightTime = s.ExternalLoadedWeightTime,

            WeighbridgeAttachmentPath = s.WeighbridgeAttachmentPath,
            WeighbridgeFileName = s.WeighbridgeFileName,
            WeighbridgeFileType = s.WeighbridgeFileType,
            WeighbridgeFileSize = s.WeighbridgeFileSize,

            ResponsibleEmployee = s.ReceivingEmployee,
            Notes = s.Notes,
            CreatedBy = s.CreatedBy,
            CreatedAt = s.CreatedAt
        };
    }
}
