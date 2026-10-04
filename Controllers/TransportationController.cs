using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FaceStone.Web.Data;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;

namespace FaceStone.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "TransportationAccess")]
[Produces("application/json")]
public class TransportationController : ControllerBase
{
    private readonly FaceStoneDbContext _db;
    private readonly ILogger<TransportationController> _logger;
    private readonly IWebHostEnvironment _env;

    public const string FixedSource = "Face stone (Full Concrete Solutions)";
    public const string DefaultShippingSupervisor = "محمد ناجح";
    public const string DefaultDepartmentSupervisor = "ازهر محمد";

    public TransportationController(
        FaceStoneDbContext db, 
        ILogger<TransportationController> logger,
        IWebHostEnvironment env)
    {
        _db = db;
        _logger = logger;
        _env = env;
    }

    /// <summary>
    /// Material Types: ONLY powder and slurry allowed.
    /// </summary>
    public static readonly List<string> MaterialTypes = new()
    {
        "powder",
        "slurry"
    };

    public static readonly List<string> Destinations = new()
    {
        "Cairo Factory",
        "Factory Sector A - Batching Plant 1",
        "Factory Sector B - Precast Yard",
        "Factory Sector C - Silos & Aggregate Storage",
        "New Cairo Construction Site - Zone 4",
        "New Administrative Capital - Tower Site A",
        "6th of October Industrial Complex",
        "Ain Sokhna Port Logistics Hub"
    };

    public static readonly List<string> TransactionStatuses = new()
    {
        "Completed",
        "In Transit",
        "Entered Factory",
        "Under Inspection",
        "Pending",
        "Cancelled"
    };

    /// <summary>
    /// Receiving Statuses: ONLY safe and intact.
    /// </summary>
    public static readonly List<string> ReceivingStatuses = new()
    {
        "safe",
        "intact"
    };

    /// <summary>
    /// Controlled selection of Recipient Titles.
    /// </summary>
    public static readonly List<string> RecipientTitles = new()
    {
        "مهندس الموقع",
        "مشرف استلام",
        "فني مختبر",
        "مسؤول الجودة",
        "مدير المشروع"
    };

    /// <summary>
    /// Retrieve configurable dropdown options for Transportation Transaction Form.
    /// </summary>
    [HttpGet("config")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TransportationConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig()
    {
        var existingDrivers = await _db.Transportations
            .AsNoTracking()
            .Where(t => !string.IsNullOrEmpty(t.DriverName))
            .Select(t => t.DriverName.Trim())
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();

        var maxTx = await _db.Transportations.MaxAsync(t => (int?)t.TransactionNumber) ?? 1000;
        var nextTxNum = maxTx + 1;

        var config = new TransportationConfigDto
        {
            MaterialTypes = MaterialTypes,
            Destinations = Destinations,
            TransactionStatuses = TransactionStatuses,
            ReceivingStatuses = ReceivingStatuses,
            ShippingSupervisors = new() { DefaultShippingSupervisor },
            DepartmentSupervisors = new() { DefaultDepartmentSupervisor },
            RecipientTitles = RecipientTitles,
            Drivers = existingDrivers,
            NextTransactionNumber = nextTxNum,
            NextInternalWeighbridgeCode = string.Empty, // Manual entry strictly required
            NextExternalWeighbridgeCode = string.Empty, // Manual entry strictly required
            FixedSource = FixedSource
        };

        return Ok(ApiResponse<TransportationConfigDto>.Ok(config));
    }

    /// <summary>
    /// Validates a Purchase Order for Transportation workflow.
    /// Returns destination (strictly read-only on client), company name, remaining quantity, and next transaction number for this PO.
    /// </summary>
    [HttpGet("po/{poNumber}")]
    [HttpGet("po-lookup/{poNumber}")]
    [ProducesResponseType(typeof(ApiResponse<PoLookupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LookupPurchaseOrder(string poNumber)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
        {
            return BadRequest(ApiResponse.Fail("Purchase Order Number is required."));
        }

        var trimmed = poNumber.Trim();
        var po = await _db.PurchaseOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PONumber.ToLower() == trimmed.ToLower());

        if (po == null)
        {
            _logger.LogWarning("PO lookup failed for Transportation: '{PONumber}' not found.", trimmed);
            return NotFound(ApiResponse.Fail("Purchase Order not found."));
        }

        if (po.RemainingQuantity <= 0)
        {
            return BadRequest(ApiResponse.Fail("This Purchase Order has no remaining quantity."));
        }

        var maxTxForPo = await _db.Transportations
            .Where(t => t.PONumber.ToLower() == trimmed.ToLower())
            .MaxAsync(t => (int?)t.TransactionNumber) ?? 0;
        var nextTxNumForPo = maxTxForPo + 1;

        var dto = new PoLookupDto
        {
            Id = po.Id,
            PoNumber = po.PONumber,
            Destination = string.IsNullOrWhiteSpace(po.Destination) ? "Cairo Factory" : po.Destination,
            Supplier = po.Supplier,
            CompanyName = po.Supplier,
            TotalQuantity = po.Quantity,
            RemainingQuantity = po.RemainingQuantity,
            PricePerTon = po.PricePerTon,
            TotalValue = po.TotalValue > 0 ? po.TotalValue : Math.Round(po.Quantity * po.PricePerTon, 2),
            OrderDate = po.OrderDate,
            InvoiceNumber = po.InvoiceNumber,
            Notes = po.Notes,
            NextTransactionNumber = nextTxNumForPo
        };

        return Ok(ApiResponse<PoLookupDto>.Ok(dto));
    }

    /// <summary>
    /// Check if a Transaction Number already exists for a PO or globally.
    /// </summary>
    [HttpGet("check-number/{transactionNumber:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckTransactionNumber(int transactionNumber, [FromQuery] string? po = null)
    {
        bool exists;
        if (!string.IsNullOrWhiteSpace(po))
        {
            var trimmedPo = po.Trim();
            exists = await _db.Transportations.AnyAsync(t => t.PONumber.ToLower() == trimmedPo.ToLower() && t.TransactionNumber == transactionNumber);
        }
        else
        {
            exists = await _db.Transportations.AnyAsync(t => t.TransactionNumber == transactionNumber);
        }
        return Ok(ApiResponse<object>.Ok(new { transactionNumber, exists }));
    }

    /// <summary>
    /// Retrieve recent transportation transactions.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(ApiResponse<List<TransportationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentTransactions([FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 100);
        var transactions = await _db.Transportations
            .AsNoTracking()
            .OrderByDescending(t => t.DateOfSubmit)
            .Take(limit)
            .Select(t => MapToDto(t))
            .ToListAsync();

        return Ok(ApiResponse<List<TransportationDto>>.Ok(transactions));
    }

    /// <summary>
    /// Retrieve single transportation transaction by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<TransportationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var transaction = await _db.Transportations.FindAsync(id);
        if (transaction == null)
        {
            return NotFound(ApiResponse.Fail($"Transportation transaction #{id} was not found."));
        }

        return Ok(ApiResponse<TransportationDto>.Ok(MapToDto(transaction)));
    }

    /// <summary>
    /// Stream / Download attachment or invoice document attached to a Transportation transaction.
    /// </summary>
    [HttpGet("invoices/{fileName}")]
    [HttpGet("attachments/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetAttachmentFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return BadRequest(ApiResponse.Fail("Invalid file name."));
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var filePath = Path.Combine(webRoot, "uploads", "transportation", fileName);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(ApiResponse.Fail("File not found."));
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = ext == ".pdf" ? "application/pdf" : "image/jpeg";
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// Submit a new Transportation record (JSON format).
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<TransportationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateJson([FromBody] CreateTransportationDto dto)
    {
        return await ProcessCreateAsync(dto);
    }

    /// <summary>
    /// Submit a new Transportation record (multipart/form-data format).
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<TransportationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateForm([FromForm] CreateTransportationDto dto)
    {
        return await ProcessCreateAsync(dto);
    }

    private async Task<(bool Success, string? ErrorMessage, string? StoredFileName, string? OriginalFileName, string? ContentType, long FileSize)> ValidateAndSaveAttachmentAsync(
        IFormFile? file, string? fallbackPath, string sectionNameAr)
    {
        if (file == null || file.Length == 0)
        {
            // If fallback existing path is provided (for API/integration test continuity)
            if (!string.IsNullOrWhiteSpace(fallbackPath))
            {
                var webRootF = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var existingF = Path.Combine(webRootF, "uploads", "transportation", Path.GetFileName(fallbackPath));
                if (System.IO.File.Exists(existingF))
                {
                    var extF = Path.GetExtension(fallbackPath).ToLowerInvariant();
                    return (true, null, Path.GetFileName(fallbackPath), Path.GetFileName(fallbackPath), extF == ".pdf" ? "application/pdf" : "image/jpeg", new FileInfo(existingF).Length);
                }
            }
            return (false, $"Attachment is required for this section. (مرفق قسم {sectionNameAr} مطلوب)", null, null, null, 0);
        }

        const long maxFileSize = 25 * 1024 * 1024; // 25 MB
        if (file.Length > maxFileSize)
        {
            return (false, $"حجم ملف مرفق {sectionNameAr} يتجاوز الحد الأقصى (25 ميجابايت).", null, null, null, 0);
        }

        var fileExt = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (fileExt != ".pdf" && fileExt != ".jpg" && fileExt != ".jpeg")
        {
            return (false, $"نوع ملف مرفق {sectionNameAr} غير مدعوم. الملفات المسموحة فقط: PDF, JPG, JPEG.", null, null, null, 0);
        }

        var mime = (file.ContentType ?? string.Empty).ToLowerInvariant();
        var validMimes = new[] { "application/pdf", "image/jpeg", "image/pjpeg", "image/jpg" };
        if (!validMimes.Contains(mime))
        {
            return (false, $"نوع ملف مرفق {sectionNameAr} غير مدعوم.", null, null, null, 0);
        }

        // Magic bytes inspection
        byte[] magicBytes = new byte[8];
        using (var stream = file.OpenReadStream())
        {
            var bytesRead = await stream.ReadAsync(magicBytes, 0, magicBytes.Length);
            if (bytesRead < 4)
            {
                return (false, $"محتوى ملف مرفق {sectionNameAr} تالف أو غير صالح.", null, null, null, 0);
            }

            if (fileExt == ".pdf")
            {
                if (magicBytes[0] != 0x25 || magicBytes[1] != 0x50 || magicBytes[2] != 0x44 || magicBytes[3] != 0x46)
                {
                    return (false, $"محتوى ملف PDF لمرفق {sectionNameAr} غير صالح.", null, null, null, 0);
                }
            }
            else
            {
                if (magicBytes[0] != 0xFF || magicBytes[1] != 0xD8 || magicBytes[2] != 0xFF)
                {
                    return (false, $"محتوى صورة JPG لمرفق {sectionNameAr} غير صالح.", null, null, null, 0);
                }
            }
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "transportation");
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

    private void CleanupSavedFiles(IEnumerable<string> fileNames)
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "transportation");
        foreach (var fn in fileNames)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(fn))
                {
                    var p = Path.Combine(uploadsDir, fn);
                    if (System.IO.File.Exists(p))
                    {
                        System.IO.File.Delete(p);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temporary file {FileName}", fn);
            }
        }
    }

    private async Task<IActionResult> ProcessCreateAsync(CreateTransportationDto dto)
    {
        // 1. Role Authorization
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasTransportationAccess(role))
        {
            _logger.LogWarning("Unauthorized user with role '{Role}' attempted to submit transportation transaction.", role);
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse.Fail("Access Denied: Transportation department is restricted to CEO, IT, and Transportation Manager."));
        }

        // 2. ModelState Validation
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse.Fail("Validation failed. Please verify the entered data.", errors));
        }

        // 3. Purchase Order Validation against database
        if (string.IsNullOrWhiteSpace(dto.PONumber))
        {
            return BadRequest(ApiResponse.Fail("Purchase Order Number is required."));
        }

        var trimmedPo = dto.PONumber.Trim();
        var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.PONumber.ToLower() == trimmedPo.ToLower());
        if (po == null)
        {
            _logger.LogWarning("Transportation transaction rejected: PO '{PONumber}' not found in database.", trimmedPo);
            return BadRequest(ApiResponse.Fail("Purchase Order not found."));
        }

        if (po.RemainingQuantity <= 0)
        {
            return BadRequest(ApiResponse.Fail("This Purchase Order has no remaining quantity."));
        }

        // 4. Server-enforced fields (Never trust client tampering)
        // Company Name and Destination strictly from PO
        var autoDestination = string.IsNullOrWhiteSpace(po.Destination) ? "Cairo Factory" : po.Destination.Trim();
        var autoCompanyName = string.IsNullOrWhiteSpace(po.Supplier) ? "FaceStone Partner" : po.Supplier.Trim();

        // Source strictly "Face stone (Full Concrete Solutions)"
        var autoSource = FixedSource;

        // Material Type strictly "powder" or "slurry"
        var matType = (dto.MaterialType ?? string.Empty).Trim().ToLowerInvariant();
        if (matType != "powder" && matType != "slurry")
        {
            return BadRequest(ApiResponse.Fail("نوع الخامة غير صحيح. الخيارات المتاحة فقط: powder أو slurry."));
        }

        // Shipping Supervisor strictly "محمد ناجح"
        var shippingSupervisor = DefaultShippingSupervisor;

        // Department Supervisor strictly "ازهر محمد"
        var deptSupervisor = DefaultDepartmentSupervisor;

        // Receiving Status strictly from Checklist ("safe", "intact")
        var rawStatus = dto.ReceivingStatus ?? string.Empty;
        var statusParts = rawStatus.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (statusParts.Count == 0 || statusParts.Any(s => s != "safe" && s != "intact"))
        {
            return BadRequest(ApiResponse.Fail("حالة الاستلام غير صحيحة. يجب اختيار safe أو intact من القائمة المرجعية (Checklist)."));
        }
        var recStatus = string.Join(", ", statusParts);

        // 5. Positive Numeric Weights & Negative Weight Validations (Entered ONLY in Section 1: الميزان)
        if (dto.WeightBeforeLoading <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الداخلي فارغ يجب أن يكون أكبر من الصفر."));
        }
        if (dto.WeightWhenExitingFactory <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الداخلي قائم يجب أن يكون أكبر من الصفر."));
        }
        if (dto.WeightWhenExitingFactory <= dto.WeightBeforeLoading)
        {
            return BadRequest(ApiResponse.Fail("الوزن الداخلي قائم يجب أن يكون أكبر من الوزن الداخلي فارغ."));
        }

        if (dto.ExternalLoadedWeight <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الخارجي قائم يجب أن يكون أكبر من الصفر."));
        }
        if (dto.ExternalEmptyWeight <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الخارجي فارغ يجب أن يكون أكبر من الصفر."));
        }
        if (dto.ExternalLoadedWeight <= dto.ExternalEmptyWeight)
        {
            return BadRequest(ApiResponse.Fail("الوزن الخارجي قائم يجب أن يكون أكبر من الوزن الخارجي فارغ."));
        }

        // 6. Weight Formulas & Calculations
        // Internal Weight = Internal Loaded - Internal Empty
        var internalWeight = Math.Round(dto.WeightWhenExitingFactory - dto.WeightBeforeLoading, 3);
        // External Weight = External Loaded - External Empty
        var externalNetWeight = Math.Round(dto.ExternalLoadedWeight - dto.ExternalEmptyWeight, 3);

        decimal wasteQuantity;
        decimal netWeight;

        if (internalWeight == externalNetWeight)
        {
            wasteQuantity = 0m;
            netWeight = internalWeight;
        }
        else
        {
            wasteQuantity = Math.Round(Math.Abs(internalWeight - externalNetWeight), 3);
            netWeight = Math.Round(Math.Min(internalWeight, externalNetWeight), 3);
        }

        if (netWeight <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الصافي المحسوب يجب أن يكون أكبر من الصفر."));
        }

        // PO Quantity limit check: Net weight <= PO remaining quantity
        if (netWeight > po.RemainingQuantity)
        {
            _logger.LogWarning("Net weight {Net} exceeds remaining quantity {Rem} for PO {PO}",
                netWeight, po.RemainingQuantity, po.PONumber);
            return BadRequest(ApiResponse.Fail("Net weight exceeds the remaining quantity of this PO."));
        }

        // 7. Shipping Time Calculation
        // Formula: Shipping Time = External Loaded Weighing Date/Time - Internal Loaded Weighing Date/Time
        int? shippingDurationMinutes = null;
        if (dto.InternalLoadedWeightTime.HasValue && dto.ExternalLoadedWeightTime.HasValue)
        {
            if (dto.ExternalLoadedWeightTime.Value < dto.InternalLoadedWeightTime.Value)
            {
                return BadRequest(ApiResponse.Fail("وقت الوزن الخارجي القائم لا يمكن أن يكون قبل وقت الوزن الداخلي القائم."));
            }
            shippingDurationMinutes = (int)Math.Max(0, (dto.ExternalLoadedWeightTime.Value - dto.InternalLoadedWeightTime.Value).TotalMinutes);
        }

        // 8. Validate and Save 3 Mandatory Attachments (One for each of the 3 main sections)
        var savedFileNames = new List<string>();

        // Section 1: الميزان Attachment
        var wbRes = await ValidateAndSaveAttachmentAsync(dto.WeighbridgeAttachment, dto.WeighbridgeAttachmentPath, "الميزان");
        if (!wbRes.Success)
        {
            CleanupSavedFiles(savedFileNames);
            return BadRequest(ApiResponse.Fail(wbRes.ErrorMessage ?? "Attachment is required for this section."));
        }
        if (!string.IsNullOrWhiteSpace(wbRes.StoredFileName)) savedFileNames.Add(wbRes.StoredFileName);

        // Section 2: يومية التوريد Attachment
        var dsRes = await ValidateAndSaveAttachmentAsync(dto.DailySupplyAttachment, dto.DailySupplyAttachmentPath, "يومية التوريد");
        if (!dsRes.Success)
        {
            CleanupSavedFiles(savedFileNames);
            return BadRequest(ApiResponse.Fail(dsRes.ErrorMessage ?? "Attachment is required for this section."));
        }
        if (!string.IsNullOrWhiteSpace(dsRes.StoredFileName)) savedFileNames.Add(dsRes.StoredFileName);

        // Section 3: بوليصة الشحن Attachment (allows InvoiceFile for backwards compatibility)
        var bolFile = dto.BillOfLadingAttachment ?? dto.InvoiceFile;
        var bolRes = await ValidateAndSaveAttachmentAsync(bolFile, dto.BillOfLadingAttachmentPath ?? dto.InvoiceAttachmentPath, "بوليصة الشحن");
        if (!bolRes.Success)
        {
            CleanupSavedFiles(savedFileNames);
            return BadRequest(ApiResponse.Fail(bolRes.ErrorMessage ?? "Attachment is required for this section."));
        }
        if (!string.IsNullOrWhiteSpace(bolRes.StoredFileName)) savedFileNames.Add(bolRes.StoredFileName);

        // 9. Atomic Concurrency-Safe Database Transaction with UPDLOCK
        var callerName = User.Identity?.Name ?? "موظف النقل";
        Guid? callerUserId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            callerUserId = parsedGuid;
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            // Concurrency Lock: Lock the PO row with UPDLOCK, ROWLOCK to serialize concurrent transactions for the same PO
            var lockedPo = await _db.PurchaseOrders
                .FromSqlInterpolated($"SELECT * FROM PurchaseOrders WITH (UPDLOCK, ROWLOCK) WHERE Id = {po.Id}")
                .FirstOrDefaultAsync();

            if (lockedPo == null)
            {
                await dbTransaction.RollbackAsync();
                CleanupSavedFiles(savedFileNames);
                return BadRequest(ApiResponse.Fail("أمر الشراء غير موجود."));
            }

            if (netWeight > lockedPo.RemainingQuantity)
            {
                await dbTransaction.RollbackAsync();
                CleanupSavedFiles(savedFileNames);
                _logger.LogWarning("Net weight {Net} exceeds remaining quantity {Rem} for PO {PO}",
                    netWeight, lockedPo.RemainingQuantity, lockedPo.PONumber);
                return BadRequest(ApiResponse.Fail("Net weight exceeds the remaining quantity of this PO."));
            }

            // Sequential Transaction Number per PO:
            // For a new PO: TransactionNumber = 1
            // For an existing PO: TransactionNumber = MAX(existing transaction number for this PO) + 1
            var maxTx = await _db.Transportations
                .Where(t => t.PONumber.ToLower() == trimmedPo.ToLower())
                .MaxAsync(t => (int?)t.TransactionNumber) ?? 0;
            int finalTxNumber = maxTx + 1;

            // Deduct net weight from PO remaining quantity
            lockedPo.RemainingQuantity -= netWeight;
            lockedPo.UpdatedAt = DateTime.UtcNow;

            // Manual Weighbridge Reference Codes (Strictly entered manually by employee)
            if (string.IsNullOrWhiteSpace(dto.InternalWeighbridgeCode))
            {
                await dbTransaction.RollbackAsync();
                CleanupSavedFiles(savedFileNames);
                return BadRequest(ApiResponse.Fail("رقم مرجع الميزان الداخلي مطلوب ويجب إدخاله يدوياً."));
            }
            var internalCode = dto.InternalWeighbridgeCode.Trim();

            if (string.IsNullOrWhiteSpace(dto.ExternalWeighbridgeCode))
            {
                await dbTransaction.RollbackAsync();
                CleanupSavedFiles(savedFileNames);
                return BadRequest(ApiResponse.Fail("رقم مرجع الميزان الخارجي مطلوب ويجب إدخاله يدوياً."));
            }
            var externalCode = dto.ExternalWeighbridgeCode.Trim();

            // Single Source of Truth for Departure and Arrival Dates
            if (!dto.InternalLoadedWeightTime.HasValue)
            {
                await dbTransaction.RollbackAsync();
                CleanupSavedFiles(savedFileNames);
                return BadRequest(ApiResponse.Fail("تاريخ ووقت الوزن القائم الداخلي مطلوب."));
            }
            if (!dto.ExternalLoadedWeightTime.HasValue)
            {
                await dbTransaction.RollbackAsync();
                CleanupSavedFiles(savedFileNames);
                return BadRequest(ApiResponse.Fail("تاريخ ووقت الوزن القائم الخارجي مطلوب."));
            }
            var departureTime = dto.InternalLoadedWeightTime.Value;
            var arrivalTime = dto.ExternalLoadedWeightTime.Value;

            if (dto.CustodyAmount < 0)
            {
                await dbTransaction.RollbackAsync();
                CleanupSavedFiles(savedFileNames);
                return BadRequest(ApiResponse.Fail("قيمة العهدة يجب أن تكون رقماً موجباً أو صفراً."));
            }
            var custodyAmount = Math.Round(dto.CustodyAmount, 3);

            // 10. Create Transportation Record
            var entity = new Transportation
            {
                TransactionNumber = finalTxNumber,
                TransactionDate = dto.TransactionDate ?? DateTime.UtcNow,
                CarNumber = dto.CarNumber.Trim(),
                DriverName = dto.DriverName.Trim(),
                Source = autoSource,
                PONumber = lockedPo.PONumber.Trim(),
                CompanyName = autoCompanyName,
                PurchaseOrderId = lockedPo.Id,
                MaterialType = matType,
                WeightBeforeLoading = Math.Round(dto.WeightBeforeLoading, 3),
                WeightWhenEnteringFactory = Math.Round(dto.WeightWhenEnteringFactory > 0 ? dto.WeightWhenEnteringFactory : dto.WeightBeforeLoading, 3),
                WeightWhenExitingFactory = Math.Round(dto.WeightWhenExitingFactory, 3),
                ExpectedWeight = internalWeight,
                InternalWeight = internalWeight,
                ExternalLoadedWeight = Math.Round(dto.ExternalLoadedWeight, 3),
                ExternalEmptyWeight = Math.Round(dto.ExternalEmptyWeight, 3),
                ExternalNetWeight = externalNetWeight,
                WasteQuantity = wasteQuantity,
                NetWeight = netWeight,
                DriverPhone = string.IsNullOrWhiteSpace(dto.DriverPhone) ? null : dto.DriverPhone.Trim(),
                DepartureTime = departureTime,
                ArrivalTime = arrivalTime,
                ShippingSupervisor = shippingSupervisor,
                DepartmentSupervisor = deptSupervisor,
                RecipientName = string.IsNullOrWhiteSpace(dto.RecipientName) ? null : dto.RecipientName.Trim(),
                RecipientTitle = string.IsNullOrWhiteSpace(dto.RecipientTitle) ? null : dto.RecipientTitle.Trim(),
                ReceivedDate = dto.ReceivedDate,
                InternalWeighbridgeCode = internalCode,
                InternalEmptyWeightTime = dto.InternalEmptyWeightTime,
                InternalLoadedWeightTime = dto.InternalLoadedWeightTime,
                ExternalWeighbridgeCode = externalCode,
                ExternalEmptyWeightTime = dto.ExternalEmptyWeightTime,
                ExternalLoadedWeightTime = dto.ExternalLoadedWeightTime,
                ShippingDurationMinutes = shippingDurationMinutes,
                Destination = autoDestination,
                TransactionStatus = string.IsNullOrWhiteSpace(dto.TransactionStatus) ? "Completed" : dto.TransactionStatus.Trim(),
                ReceivingStatus = recStatus,
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                DateOfSubmit = DateTime.UtcNow,
                Ikramiya = Math.Round(dto.Ikramiya, 3),
                CustodyAmount = custodyAmount,

                // Section 1: Weighbridge Attachment
                WeighbridgeAttachmentPath = wbRes.StoredFileName,
                WeighbridgeFileName = wbRes.OriginalFileName,
                WeighbridgeFileType = wbRes.ContentType,
                WeighbridgeFileSize = wbRes.FileSize,

                // Section 2: Daily Supply Attachment
                DailySupplyAttachmentPath = dsRes.StoredFileName,
                DailySupplyFileName = dsRes.OriginalFileName,
                DailySupplyFileType = dsRes.ContentType,
                DailySupplyFileSize = dsRes.FileSize,

                // Section 3: Bill of Lading Attachment
                BillOfLadingAttachmentPath = bolRes.StoredFileName,
                BillOfLadingFileName = bolRes.OriginalFileName,
                BillOfLadingFileType = bolRes.ContentType,
                BillOfLadingFileSize = bolRes.FileSize,

                // Backwards compatibility for Invoice fields
                InvoiceAttachmentPath = bolRes.StoredFileName ?? wbRes.StoredFileName,
                InvoiceFileName = bolRes.OriginalFileName ?? wbRes.OriginalFileName,
                InvoiceFileType = bolRes.ContentType ?? wbRes.ContentType,
                InvoiceFileSize = bolRes.FileSize > 0 ? bolRes.FileSize : wbRes.FileSize,

                SubmittedBy = callerName,
                SubmittedByUserId = callerUserId
            };

            await _db.Transportations.AddAsync(entity);
            await _db.SaveChangesAsync();

            await dbTransaction.CommitAsync();

            _logger.LogInformation("Transportation transaction PO {PO} / #{TxNum} submitted and saved. Net weight {NetWeight} deducted. Remaining PO: {Remaining}.",
                entity.PONumber, entity.TransactionNumber, netWeight, lockedPo.RemainingQuantity);

            var resultDto = MapToDto(entity);
            resultDto.PoRemainingQuantityAfter = lockedPo.RemainingQuantity;

            return Ok(ApiResponse<TransportationDto>.Ok(resultDto, "تم حفظ حركة النقل بنجاح."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            CleanupSavedFiles(savedFileNames);

            _logger.LogError(ex, "Unexpected error saving transportation transaction for PO {PO}", trimmedPo);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                ApiResponse.Fail("حدث خطأ غير متوقع أثناء حفظ حركة النقل."));
        }
    }

    private static TransportationDto MapToDto(Transportation entity)
    {
        return new TransportationDto
        {
            Id = entity.Id,
            TransactionNumber = entity.TransactionNumber,
            TransactionDate = entity.TransactionDate,
            CarNumber = entity.CarNumber,
            DriverName = entity.DriverName,
            Source = entity.Source,
            PONumber = entity.PONumber,
            CompanyName = entity.CompanyName,
            MaterialType = entity.MaterialType,
            WeightBeforeLoading = entity.WeightBeforeLoading,
            WeightWhenEnteringFactory = entity.WeightWhenEnteringFactory,
            WeightWhenExitingFactory = entity.WeightWhenExitingFactory,
            ExpectedWeight = entity.ExpectedWeight,
            InternalWeight = entity.InternalWeight > 0 ? entity.InternalWeight : (entity.WeightWhenExitingFactory - entity.WeightBeforeLoading),
            ExternalLoadedWeight = entity.ExternalLoadedWeight,
            ExternalEmptyWeight = entity.ExternalEmptyWeight,
            ExternalNetWeight = entity.ExternalNetWeight,
            WasteQuantity = entity.WasteQuantity,
            NetWeight = entity.NetWeight,
            DriverPhone = entity.DriverPhone,
            DepartureTime = entity.DepartureTime,
            ArrivalTime = entity.ArrivalTime,
            ShippingSupervisor = entity.ShippingSupervisor,
            DepartmentSupervisor = entity.DepartmentSupervisor,
            RecipientName = entity.RecipientName,
            RecipientTitle = entity.RecipientTitle,
            ReceivedDate = entity.ReceivedDate,
            InternalWeighbridgeCode = entity.InternalWeighbridgeCode,
            InternalEmptyWeightTime = entity.InternalEmptyWeightTime,
            InternalLoadedWeightTime = entity.InternalLoadedWeightTime,
            ExternalWeighbridgeCode = entity.ExternalWeighbridgeCode,
            ExternalEmptyWeightTime = entity.ExternalEmptyWeightTime,
            ExternalLoadedWeightTime = entity.ExternalLoadedWeightTime,
            ShippingDurationMinutes = entity.ShippingDurationMinutes,
            Destination = entity.Destination,
            TransactionStatus = entity.TransactionStatus,
            ReceivingStatus = entity.ReceivingStatus,
            Notes = entity.Notes,
            DateOfSubmit = entity.DateOfSubmit,
            Ikramiya = entity.Ikramiya,
            CustodyAmount = entity.CustodyAmount,
            
            WeighbridgeAttachmentPath = entity.WeighbridgeAttachmentPath,
            WeighbridgeFileName = entity.WeighbridgeFileName,
            WeighbridgeFileType = entity.WeighbridgeFileType,
            WeighbridgeFileSize = entity.WeighbridgeFileSize,

            DailySupplyAttachmentPath = entity.DailySupplyAttachmentPath,
            DailySupplyFileName = entity.DailySupplyFileName,
            DailySupplyFileType = entity.DailySupplyFileType,
            DailySupplyFileSize = entity.DailySupplyFileSize,

            BillOfLadingAttachmentPath = entity.BillOfLadingAttachmentPath,
            BillOfLadingFileName = entity.BillOfLadingFileName,
            BillOfLadingFileType = entity.BillOfLadingFileType,
            BillOfLadingFileSize = entity.BillOfLadingFileSize,

            InvoiceAttachmentPath = entity.InvoiceAttachmentPath,
            InvoiceFileName = entity.InvoiceFileName,
            InvoiceFileType = entity.InvoiceFileType,
            InvoiceFileSize = entity.InvoiceFileSize,

            SubmittedBy = entity.SubmittedBy
        };
    }
}

