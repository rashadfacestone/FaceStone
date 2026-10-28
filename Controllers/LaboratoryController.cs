using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
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
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class LaboratoryController : ControllerBase
{
    private readonly FaceStoneDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LaboratoryController> _logger;
    private readonly IHubContext<ChatHub> _hubContext;

    public LaboratoryController(
        FaceStoneDbContext db,
        IWebHostEnvironment env,
        ILogger<LaboratoryController> logger,
        IHubContext<ChatHub> hubContext)
    {
        _db = db;
        _env = env;
        _logger = logger;
        _hubContext = hubContext;
    }

    // =========================================================================
    // 1. DASHBOARD STATS & OVERVIEW
    // =========================================================================
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<LaboratoryDashboardStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        var rawReports = await _db.LaboratoryRawMaterialReports.AsNoTracking().ToListAsync();
        var inProc = await _db.LaboratoryInProcessReports.AsNoTracking().ToListAsync();
        var finalCerts = await _db.LaboratoryFinalConformityCertificates.AsNoTracking().ToListAsync();

        var stats = new LaboratoryDashboardStatsDto
        {
            PhysicalReportsCount = rawReports.Count(r => r.LaboratorySection == "PHYSICAL"),
            ChemicalReportsCount = rawReports.Count(r => r.LaboratorySection == "CHEMICAL"),
            InProcessTestsCount = inProc.Count,
            FinalCertificatesCount = finalCerts.Count,
            PendingApprovalCount = rawReports.Count(r => r.ApprovalStatus == "PENDING") + finalCerts.Count(c => c.ApprovalStatus == "PENDING"),
            ConformingBatchesCount = finalCerts.Count(c => c.ConformityDecision == "CONFORMS"),
            NonConformingBatchesCount = finalCerts.Count(c => c.ConformityDecision == "NON_CONFORMING"),
            TotalAcceptedRawMaterialTons = rawReports.Where(r => r.ApprovalStatus == "APPROVED").Sum(r => r.AcceptedQuantity),
            TotalFinalProductTons = finalCerts.Where(c => c.ConformityDecision == "CONFORMS" && c.ApprovalStatus == "APPROVED").Sum(c => c.ActualFinalProductQuantity),
            ActiveTraceableLifecyclesCount = rawReports.Select(r => r.ProductionReferenceNumber).Distinct().Count()
        };

        return Ok(ApiResponse<LaboratoryDashboardStatsDto>.Ok(stats));
    }

    // =========================================================================
    // 2. SHARED PRODUCTION REFERENCE LOOKUPS (Cross-department dropdowns)
    // =========================================================================
    [HttpGet("references")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryReferenceLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReferences(
        [FromQuery] string? section = null,
        [FromQuery] string? decision = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var query = _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Include(r => r.InProcessReports)
            .Include(r => r.FinalCertificates)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(section) && !section.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var secNorm = section.Trim().ToUpperInvariant();
            query = query.Where(r => r.LaboratorySection == secNorm);
        }

        if (!string.IsNullOrWhiteSpace(decision) && !decision.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var decNorm = decision.Trim().ToUpperInvariant();
            query = query.Where(r => r.InspectionDecision == decNorm);
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var stNorm = status.Trim().ToUpperInvariant();
            query = query.Where(r => r.ApprovalStatus == stNorm);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(r =>
                r.ProductionReferenceNumber.ToString().Contains(s) ||
                r.RawMaterialDescription.ToLower().Contains(s) ||
                (r.SupplierName != null && r.SupplierName.ToLower().Contains(s)) ||
                (r.PONumber != null && r.PONumber.ToLower().Contains(s)));
        }

        var reports = await query.OrderByDescending(r => r.ProductionReferenceNumber).Take(200).ToListAsync();

        var dtos = reports.Select(MapToReferenceLookupDto).ToList();
        return Ok(ApiResponse<List<LaboratoryReferenceLookupDto>>.Ok(dtos));
    }

    /// <summary>
    /// Returns the current authenticated user's next incremental sample number (read-only in form)
    /// </summary>
    [HttpGet("next-sample-number")]
    [ProducesResponseType(typeof(ApiResponse<NextSampleNumberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNextSampleNumber()
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth || !userId.HasValue)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail("المصادقة مطلوبة."));
        }

        var nextNo = await PeekNextUserSampleNumberAsync(userId.Value);
        var section = AppRoles.GetDefaultLaboratorySection(role);

        return Ok(ApiResponse<NextSampleNumberDto>.Ok(new NextSampleNumberDto
        {
            SampleNumber = nextNo,
            UserId = userId.Value,
            UserName = userName,
            LaboratorySection = section
        }));
    }

    /// <summary>
    /// Searchable selection control populated from existing backend references (Production, Storage, Lab).
    /// </summary>
    [HttpGet("batch-references")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryBatchLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatchReferences([FromQuery] string? search = null)
    {
        var results = new List<LaboratoryBatchLookupDto>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. From LaboratoryRawMaterialReports
        var labReports = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .OrderByDescending(r => r.Id)
            .Take(100)
            .ToListAsync();

        foreach (var r in labReports)
        {
            var key = !string.IsNullOrWhiteSpace(r.BatchNumber) ? r.BatchNumber : $"REF-{r.ProductionReferenceNumber:D4}";
            if (seenKeys.Add(key))
            {
                results.Add(new LaboratoryBatchLookupDto
                {
                    ProductionReferenceNumber = r.ProductionReferenceNumber,
                    BatchNumber = key,
                    SampleName = !string.IsNullOrWhiteSpace(r.SampleName) ? r.SampleName : r.RawMaterialDescription,
                    SampleDescription = !string.IsNullOrWhiteSpace(r.SampleDescription) ? r.SampleDescription : "بودرة رمادية اللون",
                    SupplierName = r.SupplierName,
                    PONumber = r.PONumber,
                    LaboratorySection = r.LaboratorySection,
                    CreatedAt = r.CreatedAt,
                    SourceType = "LABORATORY_REPORT"
                });
            }
        }

        // 2. From ProductionBatches
        var prodBatches = await _db.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProductionOrder)
            .OrderByDescending(b => b.Id)
            .Take(100)
            .ToListAsync();

        foreach (var pb in prodBatches)
        {
            var key = !string.IsNullOrWhiteSpace(pb.BatchNumber) ? pb.BatchNumber : $"PRD-BATCH-{pb.Id:D4}";
            if (seenKeys.Add(key))
            {
                results.Add(new LaboratoryBatchLookupDto
                {
                    ProductionReferenceNumber = pb.ProductionReferenceNumber ?? pb.Id,
                    BatchNumber = key,
                    SampleName = pb.ProductionOrder?.OrderNumber != null ? $"تشغيلة {pb.ProductionOrder.OrderNumber}" : "سيليكا باودر (Micro Silica Powder)",
                    SampleDescription = $"مرحلة: {pb.CurrentStage}",
                    SupplierName = "FaceStone Production Line",
                    PONumber = pb.ProductionOrder?.OrderNumber,
                    LaboratorySection = "PHYSICAL",
                    CreatedAt = DateTime.UtcNow,
                    SourceType = "PRODUCTION_BATCH"
                });
            }
        }

        // 3. From StorageOperations (Imports)
        var storageOps = await _db.StorageOperations
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.OperationType == "IMPORT")
            .OrderByDescending(o => o.Id)
            .Take(100)
            .ToListAsync();

        foreach (var op in storageOps)
        {
            var key = !string.IsNullOrWhiteSpace(op.WeighbridgeReference) ? op.WeighbridgeReference : $"STR-IMP-{op.TransactionNumber:D4}";
            if (seenKeys.Add(key))
            {
                var firstItem = op.Items.FirstOrDefault();
                results.Add(new LaboratoryBatchLookupDto
                {
                    ProductionReferenceNumber = op.ProductionReferenceNumber ?? op.TransactionNumber,
                    BatchNumber = key,
                    SampleName = firstItem?.MaterialName ?? "سيليكا باودر (Micro Silica Powder)",
                    SampleDescription = op.StorageLocationName ?? "بودرة رمادية اللون",
                    SupplierName = op.Source,
                    PONumber = null,
                    LaboratorySection = op.MaterialType == "CHEMICAL" ? "CHEMICAL" : "PHYSICAL",
                    CreatedAt = op.TransactionDate,
                    SourceType = "STORAGE_IMPORT"
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            results = results.Where(r =>
                r.BatchNumber.ToLower().Contains(s) ||
                r.SampleName.ToLower().Contains(s) ||
                (r.SupplierName != null && r.SupplierName.ToLower().Contains(s)) ||
                (r.PONumber != null && r.PONumber.ToLower().Contains(s)) ||
                r.ProductionReferenceNumber.ToString().Contains(s)
            ).ToList();
        }

        return Ok(ApiResponse<List<LaboratoryBatchLookupDto>>.Ok(results));
    }

    /// <summary>
    /// References with approved accepted raw material quantities or conforming finished products available for Storage Import.
    /// </summary>
    [HttpGet("for-storage-import")]
    [HttpGet("references/for-storage-import")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryReferenceLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReferencesForStorageImport()
    {
        var rawQuery = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Where(r => r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0 && !r.IsStorageReceived)
            .OrderByDescending(r => r.ProductionReferenceNumber)
            .ToListAsync();

        var dtos = rawQuery.Select(r =>
        {
            var d = MapToReferenceLookupDto(r);
            d.Stage = "RAW_MATERIAL_ACCEPTED";
            return d;
        }).ToList();

        // Also include approved conforming finished products ready for finished goods storage receipt
        var finalCerts = await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .Where(c => c.ApprovalStatus == "APPROVED" && c.ConformityDecision == "CONFORMS" && !c.IsStoragePosted)
            .OrderByDescending(c => c.ProductionReferenceNumber)
            .ToListAsync();

        foreach (var c in finalCerts)
        {
            dtos.Add(new LaboratoryReferenceLookupDto
            {
                ProductionReferenceNumber = c.ProductionReferenceNumber,
                LaboratorySection = c.LaboratorySection,
                RawMaterialDescription = c.FinalProductName,
                FinalProductName = c.FinalProductName,
                FinalProductQuantity = c.ActualFinalProductQuantity,
                SupplierName = "FaceStone Production Line",
                PONumber = c.ProductionBatchNumber,
                ReceivedQuantity = c.ActualFinalProductQuantity,
                AcceptedQuantity = c.ActualFinalProductQuantity,
                RejectedQuantity = 0,
                InspectionDecision = c.ConformityDecision,
                ConformityDecision = c.ConformityDecision,
                AnalysisDateTime = c.ProductionCompletionDateTime,
                ApprovalStatus = c.ApprovalStatus,
                IsStorageReceived = c.IsStoragePosted,
                HasFinalCertificate = true,
                Stage = "FINAL_PRODUCT_APPROVED"
            });
        }

        return Ok(ApiResponse<List<LaboratoryReferenceLookupDto>>.Ok(dtos));
    }

    /// <summary>
    /// References eligible for Production Work-In-Process transfer / batch tracking.
    /// </summary>
    [HttpGet("for-production")]
    [HttpGet("references/for-production")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryReferenceLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReferencesForProduction()
    {
        var query = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Where(r => r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0)
            .OrderByDescending(r => r.ProductionReferenceNumber)
            .ToListAsync();

        var dtos = query.Select(MapToReferenceLookupDto).ToList();
        return Ok(ApiResponse<List<LaboratoryReferenceLookupDto>>.Ok(dtos));
    }

    /// <summary>
    /// References eligible for In-Process testing.
    /// </summary>
    [HttpGet("for-in-process")]
    [HttpGet("references/for-in-process")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryReferenceLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReferencesForInProcess([FromQuery] string? section = null)
    {
        var query = _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Where(r => r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0);

        if (!string.IsNullOrWhiteSpace(section) && !section.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var secNorm = section.Trim().ToUpperInvariant();
            query = query.Where(r => r.LaboratorySection == secNorm);
        }

        var list = await query.OrderByDescending(r => r.ProductionReferenceNumber).ToListAsync();
        var dtos = list.Select(MapToReferenceLookupDto).ToList();
        return Ok(ApiResponse<List<LaboratoryReferenceLookupDto>>.Ok(dtos));
    }

    /// <summary>
    /// References eligible for Final Product Conformity inspection.
    /// </summary>
    [HttpGet("for-final-certificate")]
    [HttpGet("references/for-final-certificate")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryReferenceLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReferencesForFinalCertificate([FromQuery] string? section = null)
    {
        var query = _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Include(r => r.FinalCertificates)
            .Where(r => r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0);

        if (!string.IsNullOrWhiteSpace(section) && !section.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var secNorm = section.Trim().ToUpperInvariant();
            query = query.Where(r => r.LaboratorySection == secNorm);
        }

        var list = await query.OrderByDescending(r => r.ProductionReferenceNumber).ToListAsync();
        var dtos = list.Select(MapToReferenceLookupDto).ToList();
        return Ok(ApiResponse<List<LaboratoryReferenceLookupDto>>.Ok(dtos));
    }

    // =========================================================================
    // 3. FULL LIFECYCLE TRACEABILITY (Management, Finance & Quality Audit)
    // =========================================================================
    [HttpGet("traceability/{referenceNumber:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProductionReferenceTraceabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraceability(int referenceNumber)
    {
        var rawReport = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Include(r => r.SourceTransportation)
            .Include(r => r.PurchaseOrder)
            .FirstOrDefaultAsync(r => r.ProductionReferenceNumber == referenceNumber);

        if (rawReport == null)
        {
            return NotFound(ApiResponse.Fail($"الرقم المرجعي التتبعي #{referenceNumber} غير موجود بالنظام."));
        }

        // Linked Storage Operations (Imports or Transfers for this reference)
        var storageOps = await _db.StorageOperations
            .AsNoTracking()
            .Include(o => o.StorageLocation)
            .Include(o => o.Client)
            .Include(o => o.Items)
            .Where(o => o.ProductionReferenceNumber == referenceNumber || (rawReport.StorageOperationId.HasValue && o.Id == rawReport.StorageOperationId.Value))
            .ToListAsync();

        var storageOpDtos = storageOps.Select(op => StorageController.MapOperationToDto(op, op.Items.ToList())).ToList();

        // Linked Production WIP Batches
        var wipBatches = await _db.ProductionWorkInProcess
            .AsNoTracking()
            .Where(w => w.ProductionReferenceNumber == referenceNumber)
            .ToListAsync();

        var wipDtos = wipBatches.Select(w => new ProductionWipBatchDto
        {
            Id = w.Id,
            TrackingNumber = w.TrackingNumber,
            MaterialQuantityIssued = w.MaterialQuantityIssued,
            SourceStorageLocation = w.SourceStorageLocation,
            TransferDate = w.TransferDate,
            Status = w.Status,
            FinalProductName = w.FinalProductName,
            FinalProductQuantity = w.FinalProductQuantity,
            WasteQuantity = w.WasteQuantity,
            WasteClassification = w.WasteClassification,
            RecoverableProductName = w.RecoverableProductName,
            RecoverableQuantity = w.RecoverableQuantity
        }).ToList();

        // Linked In-Process Reports
        var inProcessList = await _db.LaboratoryInProcessReports
            .AsNoTracking()
            .Where(p => p.ProductionReferenceNumber == referenceNumber)
            .OrderBy(p => p.TestDateTime)
            .ToListAsync();

        var inProcDtos = inProcessList.Select(MapInProcessToDto).ToList();

        // Linked Final Conformity Certificate
        var finalCert = await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProductionReferenceNumber == referenceNumber);

        var finalCertDto = finalCert != null ? MapFinalCertToDto(finalCert) : null;

        // Audit Trail (FormApprovalHistory) for Laboratory raw report, storage, and certificate
        var auditHistories = await _db.FormApprovalHistories
            .AsNoTracking()
            .Where(h => (h.FormType == "LaboratoryRawMaterial" && h.FormId == rawReport.Id) ||
                        (h.FormType == "LaboratoryFinalCert" && finalCert != null && h.FormId == finalCert.Id) ||
                        (h.FormType == "Storage" && storageOps.Select(o => o.Id).Contains(h.FormId)))
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

        // Linked Transportation Summary
        TransportationSummaryDto? transDto = null;
        if (rawReport.SourceTransportation != null)
        {
            transDto = new TransportationSummaryDto
            {
                Id = rawReport.SourceTransportation.Id,
                TransactionNumber = rawReport.SourceTransportation.TransactionNumber,
                TransactionDate = rawReport.SourceTransportation.TransactionDate,
                CarNumber = rawReport.SourceTransportation.CarNumber,
                DriverName = rawReport.SourceTransportation.DriverName,
                Source = rawReport.SourceTransportation.Source,
                PONumber = rawReport.SourceTransportation.PONumber,
                MaterialType = rawReport.SourceTransportation.MaterialType,
                NetWeight = rawReport.SourceTransportation.NetWeight,
                ApprovalStatus = rawReport.SourceTransportation.ApprovalStatus
            };
        }

        // Linked Purchase Order Summary
        PurchaseOrderSummaryDto? poDto = null;
        if (rawReport.PurchaseOrder != null)
        {
            poDto = new PurchaseOrderSummaryDto
            {
                Id = rawReport.PurchaseOrder.Id,
                PONumber = rawReport.PurchaseOrder.PONumber,
                Supplier = rawReport.PurchaseOrder.Supplier,
                Quantity = rawReport.PurchaseOrder.Quantity,
                RemainingQuantity = rawReport.PurchaseOrder.RemainingQuantity,
                PricePerTon = rawReport.PurchaseOrder.PricePerTon > 0 ? rawReport.PurchaseOrder.PricePerTon : rawReport.PurchaseOrder.TonPrice,
                TotalValue = rawReport.PurchaseOrder.TotalValue,
                PaymentStatus = rawReport.PurchaseOrder.PaymentStatus
            };
        }

        var result = new ProductionReferenceTraceabilityDto
        {
            ProductionReferenceNumber = referenceNumber,
            LaboratorySection = rawReport.LaboratorySection,
            RawMaterialReport = MapRawMaterialToDto(rawReport),
            StorageImports = storageOpDtos,
            ProductionBatches = wipDtos,
            InProcessReports = inProcDtos,
            FinalCertificate = finalCertDto,
            AuditTrail = auditHistories,
            RelatedTransportation = transDto,
            RelatedPurchaseOrder = poDto
        };

        return Ok(ApiResponse<ProductionReferenceTraceabilityDto>.Ok(result));
    }

    // =========================================================================
    // 4. CARD ONE: RAW MATERIAL ANALYSIS REPORT (تقرير نتائج تحليل المواد الخام)
    // =========================================================================
    [HttpGet("raw-material-reports")]
    [ProducesResponseType(typeof(ApiResponse<List<RawMaterialReportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRawMaterialReports(
        [FromQuery] string? section = null,
        [FromQuery] string? decision = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var query = _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .AsQueryable();

        // Enforce employee laboratory section scoping if employee
        if (AppRoles.HasPhysicalLabAccess(role) && !AppRoles.HasChemicalLabAccess(role))
        {
            query = query.Where(r => r.LaboratorySection == "PHYSICAL");
        }
        else if (AppRoles.HasChemicalLabAccess(role) && !AppRoles.HasPhysicalLabAccess(role))
        {
            query = query.Where(r => r.LaboratorySection == "CHEMICAL");
        }
        else if (!string.IsNullOrWhiteSpace(section) && !section.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var secNorm = section.Trim().ToUpperInvariant();
            query = query.Where(r => r.LaboratorySection == secNorm);
        }

        if (!string.IsNullOrWhiteSpace(decision) && !decision.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => r.InspectionDecision == decision.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => r.ApprovalStatus == status.Trim().ToUpperInvariant());
        }

        if (fromDate.HasValue)
        {
            var f = fromDate.Value.Date;
            query = query.Where(r => r.AnalysisDateTime >= f);
        }

        if (toDate.HasValue)
        {
            var t = toDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(r => r.AnalysisDateTime <= t);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(r =>
                r.ProductionReferenceNumber.ToString().Contains(s) ||
                r.SampleNumber.ToString().Contains(s) ||
                (r.SampleName != null && r.SampleName.ToLower().Contains(s)) ||
                (r.BatchNumber != null && r.BatchNumber.ToLower().Contains(s)) ||
                r.RawMaterialDescription.ToLower().Contains(s) ||
                (r.SupplierName != null && r.SupplierName.ToLower().Contains(s)) ||
                (r.PONumber != null && r.PONumber.ToLower().Contains(s)) ||
                (r.ComplyStatus != null && r.ComplyStatus.ToLower().Contains(s)) ||
                r.ResponsibleEmployee.ToLower().Contains(s));
        }

        var reports = await query.OrderByDescending(r => r.Id).Take(300).ToListAsync();
        var dtos = reports.Select(MapRawMaterialToDto).ToList();
        return Ok(ApiResponse<List<RawMaterialReportDto>>.Ok(dtos));
    }

    [HttpGet("raw-material-reports/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<RawMaterialReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRawMaterialReportById(int id)
    {
        var report = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
        {
            return NotFound(ApiResponse.Fail($"تقرير فحص المواد الخام #{id} غير موجود."));
        }

        return Ok(ApiResponse<RawMaterialReportDto>.Ok(MapRawMaterialToDto(report)));
    }

    [HttpPost("raw-material-reports")]
    [ProducesResponseType(typeof(ApiResponse<RawMaterialReportDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateRawMaterialReport()
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail("المصادقة مطلوبة للوصول إلى المعمل."));
        }

        if (!AppRoles.HasLaboratoryAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: الوصول مقتصر على موظفي المعمل المعتمدين."));
        }

        var dto = await ParseRawMaterialReportRequestAsync();
        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات تقرير فحص المواد الخام غير صالحة."));
        }

        // 1. Determine & Enforce Laboratory Section
        string assignedSection = AppRoles.GetDefaultLaboratorySection(role);
        string section = "PHYSICAL";

        if (assignedSection == "PHYSICAL")
        {
            section = "PHYSICAL";
        }
        else if (assignedSection == "CHEMICAL")
        {
            section = "CHEMICAL";
        }
        else if (assignedSection == "ALL")
        {
            // CEO or IT can specify section in request
            var requestedSec = (dto.LaboratorySection ?? "PHYSICAL").Trim().ToUpperInvariant();
            section = requestedSec == "CHEMICAL" ? "CHEMICAL" : "PHYSICAL";
        }

        // Strict cross-section submission check
        if (assignedSection == "PHYSICAL" && !string.IsNullOrWhiteSpace(dto.LaboratorySection) && dto.LaboratorySection.Trim().ToUpperInvariant() == "CHEMICAL")
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: موظف المعمل الفيزيائي لا يمكنه تسجيل نتائج للمعمل الكيميائي."));
        }
        if (assignedSection == "CHEMICAL" && !string.IsNullOrWhiteSpace(dto.LaboratorySection) && dto.LaboratorySection.Trim().ToUpperInvariant() == "PHYSICAL")
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: موظف المعمل الكيميائي لا يمكنه تسجيل نتائج للمعمل الفيزيائي."));
        }

        // 2. Validate / normalize Sample Name & Description
        var sampleName = (dto.SampleName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sampleName))
        {
            sampleName = "سيليكا باودر (Micro Silica Powder)";
        }

        var sampleDesc = (dto.SampleDescription ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sampleDesc))
        {
            sampleDesc = "بودرة رمادية اللون";
        }

        var desc = (dto.RawMaterialDescription ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(desc))
        {
            desc = $"{sampleName} - {sampleDesc}";
        }

        // 3. Validate Comply Status & Inspection Decision
        var complyStatus = (dto.ComplyStatus ?? "COMPLY").Trim().ToUpperInvariant();
        if (complyStatus != "COMPLY" && complyStatus != "NOT_COMPLIED")
        {
            complyStatus = "COMPLY";
        }

        var decision = complyStatus == "COMPLY" ? "ACCEPTED" : "REJECTED";
        if (!string.IsNullOrWhiteSpace(dto.InspectionDecision) && dto.InspectionDecision.Equals("REJECTED", StringComparison.OrdinalIgnoreCase))
        {
            decision = "REJECTED";
            complyStatus = "NOT_COMPLIED";
        }

        decimal receivedQuantity = dto.ReceivedQuantity;
        decimal acceptedQuantity = dto.AcceptedQuantity;
        decimal rejectedQuantity = dto.RejectedQuantity ?? (receivedQuantity - acceptedQuantity);

        // 4. Validate Linked Transportation or PO if provided
        int? transId = null;
        int? transNum = null;
        if (dto.SourceTransportationId.HasValue && dto.SourceTransportationId.Value > 0)
        {
            var trans = await _db.Transportations.FirstOrDefaultAsync(t => t.Id == dto.SourceTransportationId.Value);
            if (trans != null)
            {
                transId = trans.Id;
                transNum = trans.TransactionNumber;
            }
        }

        int? poId = null;
        string? poNumber = dto.PONumber?.Trim();
        if (dto.PurchaseOrderId.HasValue && dto.PurchaseOrderId.Value > 0)
        {
            var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == dto.PurchaseOrderId.Value);
            if (po != null)
            {
                poId = po.Id;
                poNumber = po.PONumber;
            }
        }
        else if (!string.IsNullOrWhiteSpace(poNumber))
        {
            var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.PONumber.ToLower() == poNumber.ToLower());
            if (po != null)
            {
                poId = po.Id;
            }
        }

        // Validate Raw Material card mapping if provided
        int? rawMaterialId = null;
        if (dto.RawMaterialId.HasValue && dto.RawMaterialId.Value > 0)
        {
            var mat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Id == dto.RawMaterialId.Value);
            if (mat != null) rawMaterialId = mat.Id;
        }

        // 5. Handle Attachment
        var attachRes = await ValidateAndSaveAttachmentAsync(dto.Attachment, dto.AttachmentPath, "تقرير الفحص الفني");
        if (!attachRes.Success)
        {
            return BadRequest(ApiResponse.Fail(attachRes.ErrorMessage ?? "مرفق التقرير الفني غير صالح."));
        }

        var analysisDate = dto.AnalysisDateTime ?? DateTime.UtcNow;

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1. Shared Production Reference Number (Batch Reference)
            int refNumber;
            if (dto.ProductionReferenceNumber.HasValue && dto.ProductionReferenceNumber.Value > 0)
            {
                refNumber = dto.ProductionReferenceNumber.Value;
            }
            else
            {
                refNumber = await GetNextProductionReferenceNumberAsync();
            }

            // 2. Concurrency-Safe Independent per-user Sample Number (starts at 1 for each user)
            int sampleNumber = 1;
            if (userId.HasValue)
            {
                sampleNumber = await GetNextUserSampleNumberAsync(userId.Value);
            }

            var batchNumber = (dto.BatchNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(batchNumber))
            {
                batchNumber = $"REF-{refNumber:D4}";
            }

            var report = new LaboratoryRawMaterialReport
            {
                ProductionReferenceNumber = refNumber,
                SampleNumber = sampleNumber,
                SampleName = sampleName,
                SampleDescription = sampleDesc,
                BatchNumber = batchNumber,
                SampleReceivedDate = dto.SampleReceivedDate ?? analysisDate,
                ReportIssueDate = dto.ReportIssueDate ?? DateTime.UtcNow,
                LabManagerName = string.IsNullOrWhiteSpace(dto.LabManagerName) ? userName : dto.LabManagerName.Trim(),
                LabManagerSignature = string.IsNullOrWhiteSpace(dto.LabManagerSignature) ? "معتمد" : dto.LabManagerSignature.Trim(),
                LabManagerDate = dto.LabManagerDate ?? DateTime.UtcNow,
                ComplyStatus = complyStatus,
                LaboratorySection = section,
                AnalysisDateTime = analysisDate,
                RawMaterialDescription = desc,
                RawMaterialId = rawMaterialId,
                SupplierName = dto.SupplierName?.Trim(),
                SourceTransportationId = transId,
                TransportationTransactionNumber = transNum,
                PurchaseOrderId = poId,
                PONumber = poNumber,
                ReceivedQuantity = Math.Round(receivedQuantity, 3),
                AcceptedQuantity = Math.Round(acceptedQuantity, 3),
                RejectedQuantity = Math.Round(rejectedQuantity, 3),
                AnalysisResultsJson = dto.AnalysisResultsJson,
                InspectionDecision = decision,
                Notes = !string.IsNullOrWhiteSpace(dto.Notes) ? dto.Notes.Trim() : "مطابقة لمواصفة ASTM C1240",
                AttachmentPath = !string.IsNullOrWhiteSpace(attachRes.StoredFileName) ? $"/uploads/laboratory/{attachRes.StoredFileName}" : null,
                AttachmentFileName = attachRes.OriginalFileName,
                AttachmentFileType = attachRes.ContentType,
                AttachmentFileSize = attachRes.FileSize,
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                ApprovalStatus = "PENDING",
                IsStorageReceived = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            await _db.LaboratoryRawMaterialReports.AddAsync(report);
            await _db.SaveChangesAsync();

            // Link ProductionReferenceNumber on linked Transportation if not already set
            if (transId.HasValue)
            {
                var trans = await _db.Transportations.FirstOrDefaultAsync(t => t.Id == transId.Value);
                if (trans != null && !trans.ProductionReferenceNumber.HasValue)
                {
                    trans.ProductionReferenceNumber = refNumber;
                }
            }

            // Create FormApprovalHistory for Control & Monitoring review
            var history = new FormApprovalHistory
            {
                FormType = "LaboratoryRawMaterial",
                FormId = report.Id,
                PONumber = poNumber ?? string.Empty,
                TransactionOrMovementNumber = $"REF-#{refNumber}",
                PreviousStatus = "NONE",
                NewStatus = "PENDING",
                Action = "SUBMITTED",
                ActorName = userName,
                ActorUserId = userId,
                ActorRole = role,
                Comment = $"تم إنشاء تقرير فحص العينة #{sampleNumber} للتشغيلة {batchNumber} برقم تتبعي #{refNumber} ({section}) وقرار: {(complyStatus == "COMPLY" ? "مطابق لمواصفة ASTM C1240" : "غير مطابق")}. بانتظار اعتماد قسم التحكم والمراقبة.",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(history);

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Laboratory Sample #{Sample} created for Ref #{Ref} ({Batch}) by user {User}",
                sampleNumber, refNumber, batchNumber, userName);

            // SignalR broadcast
            var notifMsg = $"تم إصدار فحص عينة جديد (#{sampleNumber}) للتشغيلة {batchNumber} من قسم المعمل ({section}) قيد مراجعة واعتماد الرقابة.";
            await NotifyLaboratoryChangeAsync("RAW_REPORT", report.Id, refNumber, "PENDING", userName, notifMsg);

            var resultDto = MapRawMaterialToDto(report);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<RawMaterialReportDto>.Ok(resultDto, $"تم حفظ تقرير فحص العينة #{sampleNumber} برقم تشغيلة {batchNumber} بنجاح."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            CleanupSavedFiles(new[] { attachRes.StoredFileName });
            _logger.LogError(ex, "Failed to create Laboratory Raw Material Report.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ تقرير فحص العينة."));
        }
    }

    // =========================================================================
    // 5. CARD TWO: FINAL PRODUCT CONFORMITY CERTIFICATE (شهادة مطابقة المنتج النهائي)
    // =========================================================================
    [HttpGet("final-certificates")]
    [HttpGet("final-conformity-certificates")]
    [ProducesResponseType(typeof(ApiResponse<List<FinalConformityCertificateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFinalCertificates(
        [FromQuery] string? section = null,
        [FromQuery] string? decision = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var query = _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .Include(c => c.RawMaterialReport)
            .AsQueryable();

        if (AppRoles.HasPhysicalLabAccess(role) && !AppRoles.HasChemicalLabAccess(role))
        {
            query = query.Where(c => c.LaboratorySection == "PHYSICAL");
        }
        else if (AppRoles.HasChemicalLabAccess(role) && !AppRoles.HasPhysicalLabAccess(role))
        {
            query = query.Where(c => c.LaboratorySection == "CHEMICAL");
        }
        else if (!string.IsNullOrWhiteSpace(section) && !section.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.LaboratorySection == section.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(decision) && !decision.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.ConformityDecision == decision.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.ApprovalStatus == status.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.CertificateNumber.ToLower().Contains(s) ||
                c.ProductionReferenceNumber.ToString().Contains(s) ||
                c.FinalProductName.ToLower().Contains(s) ||
                c.ProductionBatchNumber.ToLower().Contains(s) ||
                c.ResponsibleEmployee.ToLower().Contains(s));
        }

        var certs = await query.OrderByDescending(c => c.Id).Take(200).ToListAsync();
        var dtos = certs.Select(MapFinalCertToDto).ToList();
        return Ok(ApiResponse<List<FinalConformityCertificateDto>>.Ok(dtos));
    }

    [HttpGet("final-certificates/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<FinalConformityCertificateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFinalCertificateById(int id)
    {
        var cert = await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cert == null)
        {
            return NotFound(ApiResponse.Fail($"شهادة مطابقة المنتج النهائي #{id} غير موجودة."));
        }

        return Ok(ApiResponse<FinalConformityCertificateDto>.Ok(MapFinalCertToDto(cert)));
    }

    [HttpPost("final-certificates")]
    [HttpPost("final-conformity-certificates")]
    [ProducesResponseType(typeof(ApiResponse<FinalConformityCertificateDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateFinalCertificate()
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail("المصادقة مطلوبة للوصول إلى المعمل."));
        }

        if (!AppRoles.HasLaboratoryAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: الوصول مقتصر على موظفي المعمل المعتمدين."));
        }

        var dto = await ParseFinalCertificateRequestAsync();
        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات شهادة مطابقة المنتج النهائي غير صالحة."));
        }

        // 1. Verify Production Reference Number exists
        if (dto.ProductionReferenceNumber <= 0)
        {
            return BadRequest(ApiResponse.Fail("الرقم المرجعي التتبعي للإنتاج مطلوب."));
        }

        var rawReport = await _db.LaboratoryRawMaterialReports
            .FirstOrDefaultAsync(r => r.ProductionReferenceNumber == dto.ProductionReferenceNumber);

        if (rawReport == null)
        {
            return BadRequest(ApiResponse.Fail($"الرقم المرجعي التتبعي #{dto.ProductionReferenceNumber} غير مسجل مسبقاً بفحص المواد الخام."));
        }

        // 2. Validate Section
        string assignedSection = AppRoles.GetDefaultLaboratorySection(role);
        string section = rawReport.LaboratorySection;
        if (assignedSection == "PHYSICAL" && section != "PHYSICAL")
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: موظف المعمل الفيزيائي لا يمكنه فحص منتجات المعمل الكيميائي."));
        }
        if (assignedSection == "CHEMICAL" && section != "CHEMICAL")
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: موظف المعمل الكيميائي لا يمكنه فحص منتجات المعمل الفيزيائي."));
        }

        // 3. Validate Output Fields
        var prodName = (dto.FinalProductName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(prodName))
        {
            return BadRequest(ApiResponse.Fail("اسم المنتج النهائي مطلوب."));
        }

        // Difference calculation: Input - Output (calculations preserved without weight restrictions blocking submission)
        var calculatedDiff = dto.ActualRawMaterialInputQuantity - dto.ActualFinalProductQuantity;

        var decision = (dto.ConformityDecision ?? "CONFORMS").Trim().ToUpperInvariant();
        if (decision != "CONFORMS" && decision != "NON_CONFORMING" && decision != "PENDING_TESTING")
        {
            return BadRequest(ApiResponse.Fail("قرار المطابقة غير صالح. الخيارات المتاحة: مطابق (CONFORMS) | غير مطابق (NON_CONFORMING) | قيد الفحص (PENDING_TESTING)."));
        }

        string? nonConfAction = null;
        if (decision == "NON_CONFORMING")
        {
            nonConfAction = (dto.NonConformityAction ?? "PENDING_DECISION").Trim().ToUpperInvariant();
            if (nonConfAction != "CLASSIFIED_PRODUCT" && nonConfAction != "REWORK_REMANUFACTURE" && nonConfAction != "PENDING_DECISION")
            {
                return BadRequest(ApiResponse.Fail("إجراء عدم المطابقة غير صالح. الخيارات: تخزين كمنتج مصنف (CLASSIFIED_PRODUCT) | إعادة تصنيع (REWORK_REMANUFACTURE) | معلق قيد القرار (PENDING_DECISION)."));
            }
        }

        var attachRes = await ValidateAndSaveAttachmentAsync(dto.Attachment, dto.AttachmentPath, "مرفق شهادة المطابقة");
        if (!attachRes.Success)
        {
            return BadRequest(ApiResponse.Fail(attachRes.ErrorMessage ?? "مرفق شهادة المطابقة غير صالح."));
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var certIndex = await _db.LaboratoryFinalConformityCertificates.CountAsync() + 1;
            var certNumber = $"CERT-{DateTime.UtcNow:yyyy}-{certIndex:D4}";

            var cert = new LaboratoryFinalConformityCertificate
            {
                CertificateNumber = certNumber,
                ProductionReferenceNumber = dto.ProductionReferenceNumber,
                RawMaterialReportId = rawReport.Id,
                LaboratorySection = section,
                FinalProductName = prodName,
                ActualFinalProductQuantity = Math.Round(dto.ActualFinalProductQuantity, 3),
                QuantityUnit = string.IsNullOrWhiteSpace(dto.QuantityUnit) ? "طن" : dto.QuantityUnit.Trim(),
                ProductionBatchNumber = (dto.ProductionBatchNumber ?? $"BATCH-{dto.ProductionReferenceNumber}").Trim(),
                ProductionWorkInProcessId = dto.ProductionWorkInProcessId,
                ProductionCompletionDateTime = dto.ProductionCompletionDateTime ?? DateTime.UtcNow,
                ActualRawMaterialInputQuantity = Math.Round(dto.ActualRawMaterialInputQuantity, 3),
                CalculatedDifference = calculatedDiff,
                ActualMeasuredWaste = dto.ActualMeasuredWaste.HasValue ? Math.Round(dto.ActualMeasuredWaste.Value, 3) : null,
                WasteClassification = string.IsNullOrWhiteSpace(dto.WasteClassification) ? "ORDINARY_WASTE" : dto.WasteClassification.Trim(),
                ConformityDecision = decision,
                NonConformityAction = nonConfAction,
                ClassifiedProductType = dto.ClassifiedProductType?.Trim(),
                ReworkInstructions = dto.ReworkInstructions?.Trim(),
                SpecificationResultsJson = dto.SpecificationResultsJson,
                Observations = dto.Observations?.Trim(),
                AttachmentPath = !string.IsNullOrWhiteSpace(attachRes.StoredFileName) ? $"/uploads/laboratory/{attachRes.StoredFileName}" : null,
                AttachmentFileName = attachRes.OriginalFileName,
                AttachmentFileType = attachRes.ContentType,
                AttachmentFileSize = attachRes.FileSize,
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                ApprovalStatus = "PENDING",
                IsStoragePosted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            await _db.LaboratoryFinalConformityCertificates.AddAsync(cert);
            await _db.SaveChangesAsync();

            // Link ProductionWorkInProcess if batch provided
            if (dto.ProductionWorkInProcessId.HasValue)
            {
                var wip = await _db.ProductionWorkInProcess.FirstOrDefaultAsync(w => w.Id == dto.ProductionWorkInProcessId.Value);
                if (wip != null)
                {
                    wip.FinalProductName = prodName;
                    wip.FinalProductQuantity = cert.ActualFinalProductQuantity;
                    wip.WasteQuantity = cert.ActualMeasuredWaste ?? calculatedDiff;
                    wip.WasteClassification = cert.WasteClassification;
                    wip.Status = decision == "CONFORMS" ? "مكتمل" : (nonConfAction == "REWORK_REMANUFACTURE" ? "إعادة تشغيل" : "قيد المعاينة");
                }
            }

            // Update connected ProductionOutputs of classification FINISHED
            var finishedOutputs = await _db.ProductionOutputs
                .Where(o => o.ProductionReferenceNumber == dto.ProductionReferenceNumber && o.Classification == "FINISHED")
                .ToListAsync();
            foreach (var outp in finishedOutputs)
            {
                outp.LaboratoryCertificateId = cert.Id;
                if (decision == "CONFORMS")
                {
                    outp.LaboratoryConformityStatus = "CONFORMS";
                }
                else
                {
                    outp.LaboratoryConformityStatus = "NON_CONFORMING";
                }
            }

            // Create FormApprovalHistory for Control & Monitoring review
            var history = new FormApprovalHistory
            {
                FormType = "LaboratoryFinalCert",
                FormId = cert.Id,
                PONumber = rawReport.PONumber ?? string.Empty,
                TransactionOrMovementNumber = cert.CertificateNumber,
                PreviousStatus = "NONE",
                NewStatus = "PENDING",
                Action = "SUBMITTED",
                ActorName = userName,
                ActorUserId = userId,
                ActorRole = role,
                Comment = $"تم إصدار شهادة مطابقة منتج نهائي ({cert.CertificateNumber}) للرقم المرجعي #{dto.ProductionReferenceNumber} بقرار: {decision}. بانتظار اعتماد قسم التحكم والمراقبة.",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(history);

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Laboratory Final Certificate {Cert} created for Ref #{Ref} by {User}",
                cert.CertificateNumber, dto.ProductionReferenceNumber, userName);

            var notifMsg = $"تم إصدار شهادة مطابقة منتج نهائي ({cert.CertificateNumber}) للرقم المرجعي #{dto.ProductionReferenceNumber} قيد اعتماد الرقابة.";
            await NotifyLaboratoryChangeAsync("FINAL_CERT", cert.Id, dto.ProductionReferenceNumber, "PENDING", userName, notifMsg);

            var resultDto = MapFinalCertToDto(cert);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<FinalConformityCertificateDto>.Ok(resultDto, $"تم إصدار شهادة مطابقة المنتج النهائي رقم {cert.CertificateNumber} بنجاح."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            CleanupSavedFiles(new[] { attachRes.StoredFileName });
            _logger.LogError(ex, "Failed to create Laboratory Final Conformity Certificate.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ شهادة المطابقة."));
        }
    }

    // =========================================================================
    // 6. CARD THREE: IN-PROCESS PRODUCT ANALYSIS REPORT (تقرير نتائج تحليل المنتج أثناء التشغيل)
    // =========================================================================
    [HttpGet("in-process-reports")]
    [ProducesResponseType(typeof(ApiResponse<List<InProcessReportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInProcessReports(
        [FromQuery] string? section = null,
        [FromQuery] int? referenceNumber = null,
        [FromQuery] string? stage = null,
        [FromQuery] string? decision = null,
        [FromQuery] string? search = null)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var query = _db.LaboratoryInProcessReports
            .AsNoTracking()
            .AsQueryable();

        if (AppRoles.HasPhysicalLabAccess(role) && !AppRoles.HasChemicalLabAccess(role))
        {
            query = query.Where(p => p.LaboratorySection == "PHYSICAL");
        }
        else if (AppRoles.HasChemicalLabAccess(role) && !AppRoles.HasPhysicalLabAccess(role))
        {
            query = query.Where(p => p.LaboratorySection == "CHEMICAL");
        }
        else if (!string.IsNullOrWhiteSpace(section) && !section.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.LaboratorySection == section.Trim().ToUpperInvariant());
        }

        if (referenceNumber.HasValue && referenceNumber.Value > 0)
        {
            query = query.Where(p => p.ProductionReferenceNumber == referenceNumber.Value);
        }

        if (!string.IsNullOrWhiteSpace(stage) && !stage.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.ProductionStage.ToLower().Contains(stage.Trim().ToLowerInvariant()));
        }

        if (!string.IsNullOrWhiteSpace(decision) && !decision.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.InspectionDecision == decision.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.ProductionReferenceNumber.ToString().Contains(s) ||
                p.ProductName.ToLower().Contains(s) ||
                p.ProductionBatchNumber.ToLower().Contains(s) ||
                p.ProductionStage.ToLower().Contains(s) ||
                p.ResponsibleEmployee.ToLower().Contains(s));
        }

        var list = await query.OrderByDescending(p => p.TestDateTime).Take(300).ToListAsync();
        var dtos = list.Select(MapInProcessToDto).ToList();
        return Ok(ApiResponse<List<InProcessReportDto>>.Ok(dtos));
    }

    [HttpGet("in-process-reports/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<InProcessReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInProcessReportById(int id)
    {
        var report = await _db.LaboratoryInProcessReports
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (report == null)
        {
            return NotFound(ApiResponse.Fail($"تقرير فحص التشغيل #{id} غير موجود."));
        }

        return Ok(ApiResponse<InProcessReportDto>.Ok(MapInProcessToDto(report)));
    }

    [HttpPost("in-process-reports")]
    [ProducesResponseType(typeof(ApiResponse<InProcessReportDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateInProcessReport()
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail("المصادقة مطلوبة للوصول إلى المعمل."));
        }

        if (!AppRoles.HasLaboratoryAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: الوصول مقتصر على موظفي المعمل المعتمدين."));
        }

        var dto = await ParseInProcessReportRequestAsync();
        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات فحص التشغيل غير صالحة."));
        }

        if (dto.ProductionReferenceNumber <= 0)
        {
            return BadRequest(ApiResponse.Fail("الرقم المرجعي التتبعي للإنتاج مطلوب."));
        }

        var rawReport = await _db.LaboratoryRawMaterialReports
            .FirstOrDefaultAsync(r => r.ProductionReferenceNumber == dto.ProductionReferenceNumber);

        if (rawReport == null)
        {
            return BadRequest(ApiResponse.Fail($"الرقم المرجعي التتبعي #{dto.ProductionReferenceNumber} غير مسجل مسبقاً."));
        }

        string assignedSection = AppRoles.GetDefaultLaboratorySection(role);
        string section = rawReport.LaboratorySection;
        if (assignedSection == "PHYSICAL" && section != "PHYSICAL")
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: موظف المعمل الفيزيائي لا يمكنه فحص تشغيل المعمل الكيميائي."));
        }
        if (assignedSection == "CHEMICAL" && section != "CHEMICAL")
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: موظف المعمل الكيميائي لا يمكنه فحص تشغيل المعمل الفيزيائي."));
        }

        var prodName = (dto.ProductName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(prodName))
        {
            return BadRequest(ApiResponse.Fail("اسم المنتج أو الخامة قيد التشغيل مطلوب."));
        }

        var stage = (dto.ProductionStage ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(stage))
        {
            return BadRequest(ApiResponse.Fail("مرحلة الإنتاج مطلوبة."));
        }

        var decision = (dto.InspectionDecision ?? "MEETS_REQUIREMENTS").Trim().ToUpperInvariant();

        var attachRes = await ValidateAndSaveAttachmentAsync(dto.Attachment, dto.AttachmentPath, "مرفق فحص التشغيل");
        if (!attachRes.Success)
        {
            return BadRequest(ApiResponse.Fail(attachRes.ErrorMessage ?? "مرفق فحص التشغيل غير صالح."));
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var report = new LaboratoryInProcessReport
            {
                ProductionReferenceNumber = dto.ProductionReferenceNumber,
                RawMaterialReportId = rawReport.Id,
                ProductionBatchNumber = (dto.ProductionBatchNumber ?? $"BATCH-{dto.ProductionReferenceNumber}").Trim(),
                ProductionWorkInProcessId = dto.ProductionWorkInProcessId,
                LaboratorySection = section,
                ProductName = prodName,
                TestDateTime = dto.TestDateTime ?? DateTime.UtcNow,
                ProductionStage = stage,
                AnalysisResultsJson = dto.AnalysisResultsJson,
                SpecificationCriteria = dto.SpecificationCriteria?.Trim(),
                InspectionDecision = decision,
                Notes = dto.Notes?.Trim(),
                AttachmentPath = !string.IsNullOrWhiteSpace(attachRes.StoredFileName) ? $"/uploads/laboratory/{attachRes.StoredFileName}" : null,
                AttachmentFileName = attachRes.OriginalFileName,
                AttachmentFileType = attachRes.ContentType,
                AttachmentFileSize = attachRes.FileSize,
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            await _db.LaboratoryInProcessReports.AddAsync(report);
            await _db.SaveChangesAsync();

            // Link WIP batch if provided
            if (dto.ProductionWorkInProcessId.HasValue)
            {
                var wip = await _db.ProductionWorkInProcess.FirstOrDefaultAsync(w => w.Id == dto.ProductionWorkInProcessId.Value);
                if (wip != null)
                {
                    if (decision == "DOES_NOT_MEET")
                    {
                        wip.Status = "معلق فحص معملي (تنبيه عدم مطابقة)";
                    }
                    else if (wip.Status == "قيد التشغيل" && decision == "MEETS_REQUIREMENTS")
                    {
                        wip.Status = "قيد التشغيل (فحص مطابق)";
                    }
                }
            }

            // Quality Gate: Check and update matching ProductionBatch
            if (!string.IsNullOrWhiteSpace(dto.ProductionBatchNumber))
            {
                var cleanBatchNum = dto.ProductionBatchNumber.Trim();
                var prodBatch = await _db.ProductionBatches
                    .FirstOrDefaultAsync(b => b.BatchNumber == cleanBatchNum);
                if (prodBatch != null)
                {
                    if (decision == "DOES_NOT_MEET")
                    {
                        prodBatch.QualityHold = true;
                        prodBatch.QualityHoldReason = $"فحص معملي غير مطابق للمواصفات: {dto.InspectionDecision} | {dto.Notes}";
                        prodBatch.QualityHoldReportId = report.Id;
                        prodBatch.QualityHoldAt = DateTime.UtcNow;
                        prodBatch.Status = "ON_HOLD_LABORATORY";
                    }
                    else if (prodBatch.QualityHold && (decision == "MEETS_REQUIREMENTS" || decision == "MEETS"))
                    {
                        prodBatch.QualityHold = false;
                        prodBatch.QualityHoldReleasedAt = DateTime.UtcNow;
                        prodBatch.QualityHoldReleasedBy = userName;
                        prodBatch.Status = "IN_PROGRESS";
                    }
                }
            }

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("In-process report #{Id} recorded for Ref #{Ref} at stage '{Stage}' by {User}",
                report.Id, dto.ProductionReferenceNumber, stage, userName);

            var resultDto = MapInProcessToDto(report);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<InProcessReportDto>.Ok(resultDto, $"تم تسجيل نتائج فحص التشغيل للمرحلة ({stage}) للرقم المرجعي #{dto.ProductionReferenceNumber} بنجاح."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            CleanupSavedFiles(new[] { attachRes.StoredFileName });
            _logger.LogError(ex, "Failed to create Laboratory In-Process Report.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ فحص التشغيل."));
        }
    }

    // =========================================================================
    // 7. SECURE ATTACHMENT STREAMING & DOWNLOAD
    // =========================================================================
    [AllowAnonymous]
    [HttpGet("attachments/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttachmentFile(string fileName, [FromQuery] bool download = false)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            return BadRequest(ApiResponse.Fail("اسم الملف غير صالح."));
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var filePath = Path.Combine(webRoot, "uploads", "laboratory", fileName);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(ApiResponse.Fail("الملف المطلوب غير موجود على الخادم."));
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        if (download)
        {
            return File(bytes, contentType, fileName);
        }

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
        return File(bytes, contentType);
    }

    // =========================================================================
    // 8. ATOMIC SEQUENCE GENERATOR (Concurrency-Safe UPDLOCK, ROWLOCK)
    // =========================================================================
    private async Task<int> GetNextProductionReferenceNumberAsync()
    {
        var seqParam = new Microsoft.Data.SqlClient.SqlParameter("@seqName", "PRODUCTION_REFERENCE");
        var nextValParam = new Microsoft.Data.SqlClient.SqlParameter("@nextVal", System.Data.SqlDbType.Int)
        {
            Direction = System.Data.ParameterDirection.Output
        };

        await _db.Database.ExecuteSqlRawAsync(@"
            UPDATE LaboratoryProductionSequences WITH (UPDLOCK, ROWLOCK)
            SET @nextVal = NextValue, NextValue = NextValue + 1
            WHERE SequenceName = @seqName;
            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO LaboratoryProductionSequences (SequenceName, NextValue) VALUES (@seqName, 2);
                SET @nextVal = 1;
            END", seqParam, nextValParam);

        return (int)nextValParam.Value;
    }

    private async Task<int> GetNextUserSampleNumberAsync(Guid userId)
    {
        var userParam = new Microsoft.Data.SqlClient.SqlParameter("@userId", userId);
        var nextValParam = new Microsoft.Data.SqlClient.SqlParameter("@nextVal", System.Data.SqlDbType.Int)
        {
            Direction = System.Data.ParameterDirection.Output
        };

        await _db.Database.ExecuteSqlRawAsync(@"
            UPDATE LaboratoryUserSampleSequences WITH (UPDLOCK, ROWLOCK)
            SET @nextVal = NextSampleNumber, NextSampleNumber = NextSampleNumber + 1, UpdatedAt = GETUTCDATE()
            WHERE UserId = @userId;
            IF @@ROWCOUNT = 0
            BEGIN
                DECLARE @maxExisting INT;
                SELECT @maxExisting = ISNULL(MAX(SampleNumber), 0) FROM LaboratoryRawMaterialReports WHERE ResponsibleUserId = @userId;
                SET @nextVal = @maxExisting + 1;
                INSERT INTO LaboratoryUserSampleSequences (UserId, NextSampleNumber, UpdatedAt) VALUES (@userId, @nextVal + 1, GETUTCDATE());
            END", userParam, nextValParam);

        return (int)nextValParam.Value;
    }

    private async Task<int> PeekNextUserSampleNumberAsync(Guid userId)
    {
        var seq = await _db.LaboratoryUserSampleSequences
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (seq != null)
        {
            return seq.NextSampleNumber;
        }

        var maxExisting = await _db.LaboratoryRawMaterialReports
            .Where(r => r.ResponsibleUserId == userId)
            .MaxAsync(r => (int?)r.SampleNumber) ?? 0;

        return maxExisting + 1;
    }

    // =========================================================================
    // 9. HELPER METHODS & REQUEST PARSERS
    // =========================================================================
    private (bool IsAuth, string Role, string UserName, Guid? UserId) GetAuthenticatedUser()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var userName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "موظف المعمل";
        Guid? userId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            userId = parsedGuid;
        }
        var isAuth = User.Identity?.IsAuthenticated ?? false;
        return (isAuth, role, userName, userId);
    }

    private async Task<CreateRawMaterialReportDto?> ParseRawMaterialReportRequestAsync()
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            var dto = new CreateRawMaterialReportDto
            {
                LaboratorySection = form["laboratorySection"].FirstOrDefault(),
                SampleName = form["sampleName"].FirstOrDefault() ?? "سيليكا باودر (Micro Silica Powder)",
                SampleDescription = form["sampleDescription"].FirstOrDefault() ?? "بودرة رمادية اللون",
                BatchNumber = form["batchNumber"].FirstOrDefault(),
                RawMaterialDescription = form["rawMaterialDescription"].FirstOrDefault(),
                SupplierName = form["supplierName"].FirstOrDefault(),
                PONumber = form["poNumber"].FirstOrDefault(),
                InspectionDecision = form["inspectionDecision"].FirstOrDefault() ?? "ACCEPTED",
                ComplyStatus = form["complyStatus"].FirstOrDefault() ?? "COMPLY",
                LabManagerName = form["labManagerName"].FirstOrDefault(),
                LabManagerSignature = form["labManagerSignature"].FirstOrDefault(),
                Notes = form["notes"].FirstOrDefault(),
                AnalysisResultsJson = form["analysisResultsJson"].FirstOrDefault(),
                Attachment = form.Files.GetFile("attachment"),
                AttachmentPath = form["attachmentPath"].FirstOrDefault()
            };

            if (int.TryParse(form["productionReferenceNumber"].FirstOrDefault(), out var pRef))
                dto.ProductionReferenceNumber = pRef;

            if (DateTime.TryParse(form["sampleReceivedDate"].FirstOrDefault(), out var srDate))
                dto.SampleReceivedDate = srDate;

            if (DateTime.TryParse(form["reportIssueDate"].FirstOrDefault(), out var riDate))
                dto.ReportIssueDate = riDate;

            if (DateTime.TryParse(form["labManagerDate"].FirstOrDefault(), out var lmDate))
                dto.LabManagerDate = lmDate;

            if (DateTime.TryParse(form["analysisDateTime"].FirstOrDefault(), out var parsedDate))
                dto.AnalysisDateTime = parsedDate;

            if (decimal.TryParse(form["receivedQuantity"].FirstOrDefault(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rQty))
                dto.ReceivedQuantity = rQty;

            if (decimal.TryParse(form["acceptedQuantity"].FirstOrDefault(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var aQty))
                dto.AcceptedQuantity = aQty;

            if (decimal.TryParse(form["rejectedQuantity"].FirstOrDefault(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rejQty))
                dto.RejectedQuantity = rejQty;

            if (int.TryParse(form["rawMaterialId"].FirstOrDefault(), out var mId))
                dto.RawMaterialId = mId;

            if (int.TryParse(form["sourceTransportationId"].FirstOrDefault(), out var tId))
                dto.SourceTransportationId = tId;

            if (int.TryParse(form["purchaseOrderId"].FirstOrDefault(), out var pId))
                dto.PurchaseOrderId = pId;

            return dto;
        }

        try
        {
            return await JsonSerializer.DeserializeAsync<CreateRawMaterialReportDto>(
                Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private async Task<CreateFinalConformityCertificateDto?> ParseFinalCertificateRequestAsync()
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            var dto = new CreateFinalConformityCertificateDto
            {
                FinalProductName = form["finalProductName"].FirstOrDefault() ?? string.Empty,
                QuantityUnit = form["quantityUnit"].FirstOrDefault() ?? "طن",
                ProductionBatchNumber = form["productionBatchNumber"].FirstOrDefault(),
                WasteClassification = form["wasteClassification"].FirstOrDefault() ?? "ORDINARY_WASTE",
                ConformityDecision = form["conformityDecision"].FirstOrDefault() ?? "CONFORMS",
                NonConformityAction = form["nonConformityAction"].FirstOrDefault(),
                ClassifiedProductType = form["classifiedProductType"].FirstOrDefault(),
                ReworkInstructions = form["reworkInstructions"].FirstOrDefault(),
                SpecificationResultsJson = form["specificationResultsJson"].FirstOrDefault(),
                Observations = form["observations"].FirstOrDefault(),
                Attachment = form.Files.GetFile("attachment"),
                AttachmentPath = form["attachmentPath"].FirstOrDefault()
            };

            if (int.TryParse(form["productionReferenceNumber"].FirstOrDefault(), out var refNo))
                dto.ProductionReferenceNumber = refNo;

            if (int.TryParse(form["productionWorkInProcessId"].FirstOrDefault(), out var wipId))
                dto.ProductionWorkInProcessId = wipId;

            if (DateTime.TryParse(form["productionCompletionDateTime"].FirstOrDefault(), out var compDate))
                dto.ProductionCompletionDateTime = compDate;

            if (decimal.TryParse(form["actualFinalProductQuantity"].FirstOrDefault(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var prodQty))
                dto.ActualFinalProductQuantity = prodQty;

            if (decimal.TryParse(form["actualRawMaterialInputQuantity"].FirstOrDefault(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var inputQty))
                dto.ActualRawMaterialInputQuantity = inputQty;

            if (decimal.TryParse(form["actualMeasuredWaste"].FirstOrDefault(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var wasteQty))
                dto.ActualMeasuredWaste = wasteQty;

            return dto;
        }

        try
        {
            return await JsonSerializer.DeserializeAsync<CreateFinalConformityCertificateDto>(
                Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private async Task<CreateInProcessReportDto?> ParseInProcessReportRequestAsync()
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            var dto = new CreateInProcessReportDto
            {
                ProductionBatchNumber = form["productionBatchNumber"].FirstOrDefault(),
                ProductName = form["productName"].FirstOrDefault() ?? string.Empty,
                ProductionStage = form["productionStage"].FirstOrDefault() ?? string.Empty,
                SpecificationCriteria = form["specificationCriteria"].FirstOrDefault(),
                AnalysisResultsJson = form["analysisResultsJson"].FirstOrDefault(),
                InspectionDecision = form["inspectionDecision"].FirstOrDefault() ?? "MEETS_REQUIREMENTS",
                Notes = form["notes"].FirstOrDefault(),
                Attachment = form.Files.GetFile("attachment"),
                AttachmentPath = form["attachmentPath"].FirstOrDefault()
            };

            if (int.TryParse(form["productionReferenceNumber"].FirstOrDefault(), out var refNo))
                dto.ProductionReferenceNumber = refNo;

            if (int.TryParse(form["productionWorkInProcessId"].FirstOrDefault(), out var wipId))
                dto.ProductionWorkInProcessId = wipId;

            if (DateTime.TryParse(form["testDateTime"].FirstOrDefault(), out var tDate))
                dto.TestDateTime = tDate;

            return dto;
        }

        try
        {
            return await JsonSerializer.DeserializeAsync<CreateInProcessReportDto>(
                Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private async Task<(bool Success, string? ErrorMessage, string? StoredFileName, string? OriginalFileName, string? ContentType, long FileSize)>
        ValidateAndSaveAttachmentAsync(IFormFile? file, string? existingPath, string fieldLabel, bool isRequired = false)
    {
        if (file == null || file.Length == 0)
        {
            if (!string.IsNullOrWhiteSpace(existingPath))
            {
                var clean = Path.GetFileName(existingPath.Trim());
                return (true, null, clean, clean, "application/octet-stream", 0);
            }

            if (isRequired)
            {
                return (false, $"{fieldLabel} مطلوب بصيغة PDF أو صورة (JPG/PNG).", null, null, null, 0);
            }

            return (true, null, null, null, null, 0);
        }

        // Validate max size 15 MB
        if (file.Length > 15 * 1024 * 1024)
        {
            return (false, $"{fieldLabel} يتجاوز الحد الأقصى المسموح (15 ميجابايت).", null, null, null, 0);
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExts.Contains(ext))
        {
            return (false, $"{fieldLabel} يجب أن يكون بصيغة PDF أو صورة (JPG / PNG / WEBP).", null, null, null, 0);
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadDir = Path.Combine(webRoot, "uploads", "laboratory");
        if (!Directory.Exists(uploadDir))
        {
            Directory.CreateDirectory(uploadDir);
        }

        var uniqueName = $"lab_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadDir, uniqueName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return (true, null, uniqueName, file.FileName, file.ContentType, file.Length);
    }

    private void CleanupSavedFiles(IEnumerable<string?> fileNames)
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        foreach (var fn in fileNames)
        {
            if (string.IsNullOrWhiteSpace(fn)) continue;
            try
            {
                var path = Path.Combine(webRoot, "uploads", "laboratory", fn);
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup uploaded laboratory file {File}", fn);
            }
        }
    }

    private async Task NotifyLaboratoryChangeAsync(string entityType, int entityId, int referenceNumber, string status, string actorName, string message)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("OnLaboratoryStatusChanged", new
            {
                entityType = entityType,
                entityId = entityId,
                productionReferenceNumber = referenceNumber,
                status = status,
                actorName = actorName,
                message = message,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SignalR broadcast for Laboratory {Entity} #{Id}", entityType, entityId);
        }
    }

    // =========================================================================
    // 10. MAPPERS
    // =========================================================================
    public static RawMaterialReportDto MapRawMaterialToDto(LaboratoryRawMaterialReport r) => new()
    {
        Id = r.Id,
        ProductionReferenceNumber = r.ProductionReferenceNumber,
        SampleNumber = r.SampleNumber,
        SampleName = !string.IsNullOrWhiteSpace(r.SampleName) ? r.SampleName : r.RawMaterialDescription,
        SampleDescription = r.SampleDescription,
        BatchNumber = r.BatchNumber,
        SampleReceivedDate = r.SampleReceivedDate,
        ReportIssueDate = r.ReportIssueDate,
        LabManagerName = r.LabManagerName,
        LabManagerSignature = r.LabManagerSignature,
        LabManagerDate = r.LabManagerDate,
        ComplyStatus = !string.IsNullOrWhiteSpace(r.ComplyStatus) ? r.ComplyStatus : (r.InspectionDecision == "ACCEPTED" ? "COMPLY" : "NOT_COMPLIED"),
        LaboratorySection = r.LaboratorySection,
        AnalysisDateTime = r.AnalysisDateTime,
        RawMaterialDescription = r.RawMaterialDescription,
        RawMaterialId = r.RawMaterialId,
        RawMaterialName = r.RawMaterial?.Name,
        SupplierName = r.SupplierName,
        SourceTransportationId = r.SourceTransportationId,
        TransportationTransactionNumber = r.TransportationTransactionNumber,
        PurchaseOrderId = r.PurchaseOrderId,
        PONumber = r.PONumber,
        ReceivedQuantity = r.ReceivedQuantity,
        AcceptedQuantity = r.AcceptedQuantity,
        RejectedQuantity = r.RejectedQuantity,
        AnalysisResultsJson = r.AnalysisResultsJson,
        InspectionDecision = r.InspectionDecision,
        Notes = r.Notes,
        AttachmentPath = r.AttachmentPath,
        AttachmentFileName = r.AttachmentFileName,
        AttachmentFileType = r.AttachmentFileType,
        AttachmentFileSize = r.AttachmentFileSize,
        ResponsibleEmployee = r.ResponsibleEmployee,
        ApprovalStatus = r.ApprovalStatus,
        ReviewedBy = r.ReviewedBy,
        ReviewedAt = r.ReviewedAt,
        RejectionReason = r.RejectionReason,
        IsStorageReceived = r.IsStorageReceived,
        StorageReceivedAt = r.StorageReceivedAt,
        StorageOperationId = r.StorageOperationId,
        CreatedAt = r.CreatedAt,
        CreatedBy = r.CreatedBy
    };

    public static InProcessReportDto MapInProcessToDto(LaboratoryInProcessReport p) => new()
    {
        Id = p.Id,
        ProductionReferenceNumber = p.ProductionReferenceNumber,
        RawMaterialReportId = p.RawMaterialReportId,
        ProductionBatchNumber = p.ProductionBatchNumber,
        ProductionWorkInProcessId = p.ProductionWorkInProcessId,
        LaboratorySection = p.LaboratorySection,
        ProductName = p.ProductName,
        TestDateTime = p.TestDateTime,
        ProductionStage = p.ProductionStage,
        AnalysisResultsJson = p.AnalysisResultsJson,
        SpecificationCriteria = p.SpecificationCriteria,
        InspectionDecision = p.InspectionDecision,
        Notes = p.Notes,
        AttachmentPath = p.AttachmentPath,
        AttachmentFileName = p.AttachmentFileName,
        AttachmentFileType = p.AttachmentFileType,
        AttachmentFileSize = p.AttachmentFileSize,
        ResponsibleEmployee = p.ResponsibleEmployee,
        CreatedAt = p.CreatedAt,
        CreatedBy = p.CreatedBy
    };

    public static FinalConformityCertificateDto MapFinalCertToDto(LaboratoryFinalConformityCertificate c) => new()
    {
        Id = c.Id,
        CertificateNumber = c.CertificateNumber,
        ProductionReferenceNumber = c.ProductionReferenceNumber,
        RawMaterialReportId = c.RawMaterialReportId,
        LaboratorySection = c.LaboratorySection,
        FinalProductName = c.FinalProductName,
        ActualFinalProductQuantity = c.ActualFinalProductQuantity,
        QuantityUnit = c.QuantityUnit,
        ProductionBatchNumber = c.ProductionBatchNumber,
        ProductionWorkInProcessId = c.ProductionWorkInProcessId,
        ProductionCompletionDateTime = c.ProductionCompletionDateTime,
        ActualRawMaterialInputQuantity = c.ActualRawMaterialInputQuantity,
        CalculatedDifference = c.CalculatedDifference,
        ActualMeasuredWaste = c.ActualMeasuredWaste,
        WasteClassification = c.WasteClassification,
        ConformityDecision = c.ConformityDecision,
        NonConformityAction = c.NonConformityAction,
        ClassifiedProductType = c.ClassifiedProductType,
        ReworkInstructions = c.ReworkInstructions,
        SpecificationResultsJson = c.SpecificationResultsJson,
        Observations = c.Observations,
        AttachmentPath = c.AttachmentPath,
        AttachmentFileName = c.AttachmentFileName,
        AttachmentFileType = c.AttachmentFileType,
        AttachmentFileSize = c.AttachmentFileSize,
        ResponsibleEmployee = c.ResponsibleEmployee,
        ApprovalStatus = c.ApprovalStatus,
        ReviewedBy = c.ReviewedBy,
        ReviewedAt = c.ReviewedAt,
        RejectionReason = c.RejectionReason,
        IsStoragePosted = c.IsStoragePosted,
        StoragePostedAt = c.StoragePostedAt,
        CreatedAt = c.CreatedAt,
        CreatedBy = c.CreatedBy
    };

    public static LaboratoryReferenceLookupDto MapToReferenceLookupDto(LaboratoryRawMaterialReport r)
    {
        var finalCert = r.FinalCertificates?.FirstOrDefault();
        return new LaboratoryReferenceLookupDto
        {
            ProductionReferenceNumber = r.ProductionReferenceNumber,
            LaboratorySection = r.LaboratorySection,
            RawMaterialDescription = r.RawMaterialDescription,
            RawMaterialId = r.RawMaterialId,
            RawMaterialName = r.RawMaterial?.Name,
            SupplierName = r.SupplierName,
            SourceTransportationId = r.SourceTransportationId,
            TransportationTransactionNumber = r.TransportationTransactionNumber,
            PONumber = r.PONumber,
            ReceivedQuantity = r.ReceivedQuantity,
            AcceptedQuantity = r.AcceptedQuantity,
            RejectedQuantity = r.RejectedQuantity,
            InspectionDecision = r.InspectionDecision,
            AnalysisDateTime = r.AnalysisDateTime,
            ApprovalStatus = r.ApprovalStatus,
            IsStorageReceived = r.IsStorageReceived,
            InProcessTestsCount = r.InProcessReports?.Count ?? 0,
            HasFinalCertificate = finalCert != null,
            FinalProductName = finalCert?.FinalProductName,
            FinalProductQuantity = finalCert?.ActualFinalProductQuantity,
            ConformityDecision = finalCert?.ConformityDecision
        };
    }
}
