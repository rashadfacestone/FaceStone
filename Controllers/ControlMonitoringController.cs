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

        var storagePending = (await _db.StorageTransactions
            .AsNoTracking()
            .CountAsync(s => s.ApprovalStatus == "PENDING"))
            + (await _db.StorageOperations
            .AsNoTracking()
            .CountAsync(s => s.ApprovalStatus == "PENDING"));

        var attendancePending = await _db.AttendanceSubmissions
            .AsNoTracking()
            .CountAsync(a => a.ApprovalStatus == "PENDING" || a.ApprovalStatus == "PARTIALLY_REVIEWED");

        var transApprovedToday = await _db.Transportations
            .AsNoTracking()
            .CountAsync(t => t.ApprovalStatus == "APPROVED" && t.ReviewedAt.HasValue && t.ReviewedAt.Value >= todayUtcStart);

        var storageApprovedToday = (await _db.StorageTransactions
            .AsNoTracking()
            .CountAsync(s => s.ApprovalStatus == "APPROVED" && s.ReviewedAt.HasValue && s.ReviewedAt.Value >= todayUtcStart))
            + (await _db.StorageOperations
            .AsNoTracking()
            .CountAsync(s => s.ApprovalStatus == "APPROVED" && s.ReviewedAt.HasValue && s.ReviewedAt.Value >= todayUtcStart));

        var attendanceApprovedToday = await _db.AttendanceSubmissions
            .AsNoTracking()
            .CountAsync(a => (a.ApprovalStatus == "APPROVED" || a.ApprovalStatus == "MIXED") && a.ReviewedAt.HasValue && a.ReviewedAt.Value >= todayUtcStart);

        var transRejectedToday = await _db.Transportations
            .AsNoTracking()
            .CountAsync(t => t.ApprovalStatus == "REJECTED" && t.ReviewedAt.HasValue && t.ReviewedAt.Value >= todayUtcStart);

        var storageRejectedToday = (await _db.StorageTransactions
            .AsNoTracking()
            .CountAsync(s => s.ApprovalStatus == "REJECTED" && s.ReviewedAt.HasValue && s.ReviewedAt.Value >= todayUtcStart))
            + (await _db.StorageOperations
            .AsNoTracking()
            .CountAsync(s => s.ApprovalStatus == "REJECTED" && s.ReviewedAt.HasValue && s.ReviewedAt.Value >= todayUtcStart));

        var attendanceRejectedToday = await _db.AttendanceSubmissions
            .AsNoTracking()
            .CountAsync(a => a.ApprovalStatus == "REJECTED" && a.ReviewedAt.HasValue && a.ReviewedAt.Value >= todayUtcStart);

        var labPending = (await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .CountAsync(l => l.ApprovalStatus == "PENDING"))
            + (await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .CountAsync(l => l.ApprovalStatus == "PENDING"));

        var labApprovedToday = (await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .CountAsync(l => l.ApprovalStatus == "APPROVED" && l.ReviewedAt.HasValue && l.ReviewedAt.Value >= todayUtcStart))
            + (await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .CountAsync(l => l.ApprovalStatus == "APPROVED" && l.ReviewedAt.HasValue && l.ReviewedAt.Value >= todayUtcStart));

        var labRejectedToday = (await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .CountAsync(l => l.ApprovalStatus == "REJECTED" && l.ReviewedAt.HasValue && l.ReviewedAt.Value >= todayUtcStart))
            + (await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .CountAsync(l => l.ApprovalStatus == "REJECTED" && l.ReviewedAt.HasValue && l.ReviewedAt.Value >= todayUtcStart));

        var stats = new ControlMonitoringDashboardStatsDto
        {
            PendingReviewsCount = transPending + storagePending + attendancePending + labPending,
            TransportationPendingCount = transPending,
            StoragePendingCount = storagePending,
            AttendancePendingCount = attendancePending,
            LaboratoryPendingCount = labPending,
            ApprovedTodayCount = transApprovedToday + storageApprovedToday + attendanceApprovedToday + labApprovedToday,
            RejectedTodayCount = transRejectedToday + storageRejectedToday + attendanceRejectedToday + labRejectedToday
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
            // 2.a New Storage Operations (Redesign: Import, Export, Transfer)
            var opQuery = _db.StorageOperations
                .AsNoTracking()
                .Include(o => o.StorageLocation)
                .Include(o => o.Client)
                .Include(o => o.Items)
                .AsQueryable();

            if (normalizedStatus != "ALL")
            {
                opQuery = opQuery.Where(o => o.ApprovalStatus == normalizedStatus);
            }

            if (!string.IsNullOrEmpty(trimmedEmp))
            {
                opQuery = opQuery.Where(o =>
                    (o.ResponsibleEmployee != null && o.ResponsibleEmployee.ToLower().Contains(trimmedEmp)) ||
                    (o.CreatedBy != null && o.CreatedBy.ToLower().Contains(trimmedEmp)));
            }

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                opQuery = opQuery.Where(o => o.TransactionDate >= fDate);
            }

            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                opQuery = opQuery.Where(o => o.TransactionDate <= tDate);
            }

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                opQuery = opQuery.Where(o =>
                    o.TransactionNumber.ToString().Contains(trimmedSearch) ||
                    o.Id.ToString().Contains(trimmedSearch) ||
                    (o.ResponsibleEmployee != null && o.ResponsibleEmployee.ToLower().Contains(trimmedSearch)) ||
                    (o.Source != null && o.Source.ToLower().Contains(trimmedSearch)) ||
                    (o.Client != null && o.Client.Name.ToLower().Contains(trimmedSearch)) ||
                    (o.DestinationName != null && o.DestinationName.ToLower().Contains(trimmedSearch)) ||
                    o.OperationType.ToLower().Contains(trimmedSearch) ||
                    o.Items.Any(i => i.MaterialName.ToLower().Contains(trimmedSearch)));
            }

            var opList = await opQuery.OrderByDescending(o => o.CreatedAt).Take(200).ToListAsync();
            foreach (var op in opList)
            {
                results.Add(MapStorageOperationToReviewItem(op));
            }

            // 2.b Legacy Storage Transactions
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

        // 3. Fetch Attendance Forms
        if (normalizedType == "ALL" || normalizedType == "ATTENDANCE")
        {
            var attQuery = _db.AttendanceSubmissions
                .AsNoTracking()
                .Include(a => a.Records)
                    .ThenInclude(r => r.User)
                .Include(a => a.Attachments)
                .AsQueryable();

            if (normalizedStatus == "PENDING")
            {
                attQuery = attQuery.Where(a => a.ApprovalStatus == "PENDING" || a.ApprovalStatus == "PARTIALLY_REVIEWED");
            }
            else if (normalizedStatus == "APPROVED")
            {
                attQuery = attQuery.Where(a => a.ApprovalStatus == "APPROVED" || a.ApprovalStatus == "MIXED");
            }
            else if (normalizedStatus != "ALL")
            {
                attQuery = attQuery.Where(a => a.ApprovalStatus == normalizedStatus);
            }

            if (!string.IsNullOrEmpty(trimmedEmp))
            {
                attQuery = attQuery.Where(a => a.SubmittedBy != null && a.SubmittedBy.ToLower().Contains(trimmedEmp));
            }

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                attQuery = attQuery.Where(a => a.AttendanceDate >= fDate);
            }

            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                attQuery = attQuery.Where(a => a.AttendanceDate <= tDate);
            }

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                attQuery = attQuery.Where(a =>
                    a.Id.ToString().Contains(trimmedSearch) ||
                    (a.SubmittedBy != null && a.SubmittedBy.ToLower().Contains(trimmedSearch)) ||
                    (a.Notes != null && a.Notes.ToLower().Contains(trimmedSearch)));
            }

            var attList = await attQuery.OrderByDescending(a => a.SubmittedAt).Take(200).ToListAsync();
            foreach (var a in attList)
            {
                results.Add(MapAttendanceToReviewItem(a));
            }
        }

        // 4. Fetch Laboratory Forms (Raw Material Reports & Final Conformity Certificates)
        if (normalizedType == "ALL" || normalizedType == "LABORATORY" || normalizedType == "LAB")
        {
            // 4.a Raw Material Analysis Reports
            var labRawQuery = _db.LaboratoryRawMaterialReports.AsNoTracking().AsQueryable();

            if (normalizedStatus != "ALL")
            {
                labRawQuery = labRawQuery.Where(r => r.ApprovalStatus == normalizedStatus);
            }

            if (!string.IsNullOrEmpty(trimmedPo))
            {
                labRawQuery = labRawQuery.Where(r => r.PONumber != null && r.PONumber.ToLower().Contains(trimmedPo));
            }

            if (!string.IsNullOrEmpty(trimmedEmp))
            {
                labRawQuery = labRawQuery.Where(r => r.ResponsibleEmployee != null && r.ResponsibleEmployee.ToLower().Contains(trimmedEmp));
            }

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                labRawQuery = labRawQuery.Where(r => r.AnalysisDateTime >= fDate);
            }

            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                labRawQuery = labRawQuery.Where(r => r.AnalysisDateTime <= tDate);
            }

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                labRawQuery = labRawQuery.Where(r =>
                    r.ProductionReferenceNumber.ToString().Contains(trimmedSearch) ||
                    r.RawMaterialDescription.ToLower().Contains(trimmedSearch) ||
                    (r.SupplierName != null && r.SupplierName.ToLower().Contains(trimmedSearch)) ||
                    (r.PONumber != null && r.PONumber.ToLower().Contains(trimmedSearch)) ||
                    (r.ResponsibleEmployee != null && r.ResponsibleEmployee.ToLower().Contains(trimmedSearch)));
            }

            var labRawList = await labRawQuery.OrderByDescending(r => r.AnalysisDateTime).Take(200).ToListAsync();
            foreach (var r in labRawList)
            {
                results.Add(MapLaboratoryRawReportToReviewItem(r));
            }

            // 4.b Final Product Conformity Certificates
            var labFinalQuery = _db.LaboratoryFinalConformityCertificates.AsNoTracking().AsQueryable();

            if (normalizedStatus != "ALL")
            {
                labFinalQuery = labFinalQuery.Where(c => c.ApprovalStatus == normalizedStatus);
            }

            if (!string.IsNullOrEmpty(trimmedEmp))
            {
                labFinalQuery = labFinalQuery.Where(c => c.ResponsibleEmployee != null && c.ResponsibleEmployee.ToLower().Contains(trimmedEmp));
            }

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                labFinalQuery = labFinalQuery.Where(c => c.ProductionCompletionDateTime >= fDate);
            }

            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                labFinalQuery = labFinalQuery.Where(c => c.ProductionCompletionDateTime <= tDate);
            }

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                labFinalQuery = labFinalQuery.Where(c =>
                    c.CertificateNumber.ToLower().Contains(trimmedSearch) ||
                    c.ProductionReferenceNumber.ToString().Contains(trimmedSearch) ||
                    c.FinalProductName.ToLower().Contains(trimmedSearch) ||
                    (c.ResponsibleEmployee != null && c.ResponsibleEmployee.ToLower().Contains(trimmedSearch)));
            }

            var labFinalList = await labFinalQuery.OrderByDescending(c => c.ProductionCompletionDateTime).Take(200).ToListAsync();
            foreach (var c in labFinalList)
            {
                results.Add(MapLaboratoryFinalCertToReviewItem(c));
            }
        }

        // Sort combined items by submission date descending
        var sorted = results.OrderByDescending(r => r.SubmissionDate).ToList();
        return Ok(ApiResponse<List<ControlMonitoringReviewItemDto>>.Ok(sorted));
    }

    /// <summary>
    /// Retrieve full approval & rejection history for all Transportation and Storage requests across the whole system.
    /// Returns historical APPROVED and REJECTED requests with server-side pagination, sorting, audit trails, and filters.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<PagedHistoryResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string? status = "ALL",
        [FromQuery] string? formType = "ALL",
        [FromQuery] string? department = null,
        [FromQuery] string? po = null,
        [FromQuery] string? employee = null,
        [FromQuery] string? reviewer = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = "reviewedAt",
        [FromQuery] string? sortOrder = "desc")
    {
        var normStatus = (status ?? "ALL").Trim().ToUpperInvariant();
        var normType = (department ?? formType ?? "ALL").Trim().ToUpperInvariant();
        var trimmedPo = po?.Trim().ToLowerInvariant();
        var trimmedEmp = employee?.Trim().ToLowerInvariant();
        var trimmedRev = reviewer?.Trim().ToLowerInvariant();
        var trimmedSearch = search?.Trim().ToLowerInvariant();

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var allItems = new List<ControlMonitoringReviewItemDto>();

        // 1. Transportation history
        if (normType == "ALL" || normType == "TRANSPORTATION")
        {
            var transQuery = _db.Transportations.AsNoTracking().AsQueryable();

            if (normStatus == "APPROVED")
            {
                transQuery = transQuery.Where(t => t.ApprovalStatus == "APPROVED");
            }
            else if (normStatus == "REJECTED")
            {
                transQuery = transQuery.Where(t => t.ApprovalStatus == "REJECTED");
            }
            else if (normStatus == "PENDING")
            {
                transQuery = transQuery.Where(t => t.ApprovalStatus == "PENDING");
            }
            else // Default: Completed decisions across whole system (both APPROVED and REJECTED)
            {
                transQuery = transQuery.Where(t => t.ApprovalStatus == "APPROVED" || t.ApprovalStatus == "REJECTED");
            }

            if (!string.IsNullOrEmpty(trimmedPo))
            {
                transQuery = transQuery.Where(t => t.PONumber.ToLower().Contains(trimmedPo));
            }

            if (!string.IsNullOrEmpty(trimmedEmp))
            {
                transQuery = transQuery.Where(t => t.SubmittedBy != null && t.SubmittedBy.ToLower().Contains(trimmedEmp));
            }

            if (!string.IsNullOrEmpty(trimmedRev))
            {
                transQuery = transQuery.Where(t => t.ReviewedBy != null && t.ReviewedBy.ToLower().Contains(trimmedRev));
            }

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                transQuery = transQuery.Where(t => (t.ReviewedAt.HasValue && t.ReviewedAt.Value >= fDate) || t.TransactionDate >= fDate);
            }

            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                transQuery = transQuery.Where(t => (t.ReviewedAt.HasValue && t.ReviewedAt.Value <= tDate) || t.TransactionDate <= tDate);
            }

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                transQuery = transQuery.Where(t =>
                    t.PONumber.ToLower().Contains(trimmedSearch) ||
                    t.TransactionNumber.ToString().Contains(trimmedSearch) ||
                    t.Id.ToString().Contains(trimmedSearch) ||
                    (t.SubmittedBy != null && t.SubmittedBy.ToLower().Contains(trimmedSearch)) ||
                    (t.ReviewedBy != null && t.ReviewedBy.ToLower().Contains(trimmedSearch)) ||
                    (t.RejectionReason != null && t.RejectionReason.ToLower().Contains(trimmedSearch)) ||
                    t.DriverName.ToLower().Contains(trimmedSearch) ||
                    t.CarNumber.ToLower().Contains(trimmedSearch) ||
                    (t.CompanyName != null && t.CompanyName.ToLower().Contains(trimmedSearch)) ||
                    t.MaterialType.ToLower().Contains(trimmedSearch));
            }

            var transList = await transQuery.ToListAsync();
            foreach (var t in transList)
            {
                allItems.Add(MapTransportationToReviewItem(t));
            }
        }

        // 2. Storage history
        if (normType == "ALL" || normType == "STORAGE")
        {
            // 2.a New Storage Operations history
            var opHistoryQuery = _db.StorageOperations
                .AsNoTracking()
                .Include(o => o.StorageLocation)
                .Include(o => o.Client)
                .Include(o => o.Items)
                .AsQueryable();

            if (normStatus == "APPROVED")
            {
                opHistoryQuery = opHistoryQuery.Where(o => o.ApprovalStatus == "APPROVED");
            }
            else if (normStatus == "REJECTED")
            {
                opHistoryQuery = opHistoryQuery.Where(o => o.ApprovalStatus == "REJECTED");
            }
            else if (normStatus == "PENDING")
            {
                opHistoryQuery = opHistoryQuery.Where(o => o.ApprovalStatus == "PENDING");
            }
            else
            {
                opHistoryQuery = opHistoryQuery.Where(o => o.ApprovalStatus == "APPROVED" || o.ApprovalStatus == "REJECTED");
            }

            if (!string.IsNullOrEmpty(trimmedEmp))
            {
                opHistoryQuery = opHistoryQuery.Where(o =>
                    (o.ResponsibleEmployee != null && o.ResponsibleEmployee.ToLower().Contains(trimmedEmp)) ||
                    (o.CreatedBy != null && o.CreatedBy.ToLower().Contains(trimmedEmp)));
            }

            if (!string.IsNullOrEmpty(trimmedRev))
            {
                opHistoryQuery = opHistoryQuery.Where(o => o.ReviewedBy != null && o.ReviewedBy.ToLower().Contains(trimmedRev));
            }

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                opHistoryQuery = opHistoryQuery.Where(o => (o.ReviewedAt.HasValue && o.ReviewedAt.Value >= fDate) || o.TransactionDate >= fDate);
            }

            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                opHistoryQuery = opHistoryQuery.Where(o => (o.ReviewedAt.HasValue && o.ReviewedAt.Value <= tDate) || o.TransactionDate <= tDate);
            }

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                opHistoryQuery = opHistoryQuery.Where(o =>
                    o.TransactionNumber.ToString().Contains(trimmedSearch) ||
                    o.Id.ToString().Contains(trimmedSearch) ||
                    (o.ResponsibleEmployee != null && o.ResponsibleEmployee.ToLower().Contains(trimmedSearch)) ||
                    (o.ReviewedBy != null && o.ReviewedBy.ToLower().Contains(trimmedSearch)) ||
                    (o.RejectionReason != null && o.RejectionReason.ToLower().Contains(trimmedSearch)) ||
                    (o.Source != null && o.Source.ToLower().Contains(trimmedSearch)) ||
                    (o.Client != null && o.Client.Name.ToLower().Contains(trimmedSearch)) ||
                    (o.DestinationName != null && o.DestinationName.ToLower().Contains(trimmedSearch)) ||
                    o.OperationType.ToLower().Contains(trimmedSearch) ||
                    o.Items.Any(i => i.MaterialName.ToLower().Contains(trimmedSearch)));
            }

            var opHistList = await opHistoryQuery.ToListAsync();
            foreach (var op in opHistList)
            {
                allItems.Add(MapStorageOperationToReviewItem(op));
            }

            // 2.b Legacy Storage Transactions history
            var storageQuery = _db.StorageTransactions.AsNoTracking().AsQueryable();

            if (normStatus == "APPROVED")
            {
                storageQuery = storageQuery.Where(s => s.ApprovalStatus == "APPROVED");
            }
            else if (normStatus == "REJECTED")
            {
                storageQuery = storageQuery.Where(s => s.ApprovalStatus == "REJECTED");
            }
            else if (normStatus == "PENDING")
            {
                storageQuery = storageQuery.Where(s => s.ApprovalStatus == "PENDING");
            }
            else // Default: Completed decisions across whole system (both APPROVED and REJECTED)
            {
                storageQuery = storageQuery.Where(s => s.ApprovalStatus == "APPROVED" || s.ApprovalStatus == "REJECTED");
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

            if (!string.IsNullOrEmpty(trimmedRev))
            {
                storageQuery = storageQuery.Where(s => s.ReviewedBy != null && s.ReviewedBy.ToLower().Contains(trimmedRev));
            }

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                storageQuery = storageQuery.Where(s => (s.ReviewedAt.HasValue && s.ReviewedAt.Value >= fDate) || s.TransactionDate >= fDate);
            }

            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                storageQuery = storageQuery.Where(s => (s.ReviewedAt.HasValue && s.ReviewedAt.Value <= tDate) || s.TransactionDate <= tDate);
            }

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                storageQuery = storageQuery.Where(s =>
                    (s.PONumber != null && s.PONumber.ToLower().Contains(trimmedSearch)) ||
                    s.MovementNumber.ToString().Contains(trimmedSearch) ||
                    s.Id.ToString().Contains(trimmedSearch) ||
                    (s.CreatedBy != null && s.CreatedBy.ToLower().Contains(trimmedSearch)) ||
                    (s.ReviewedBy != null && s.ReviewedBy.ToLower().Contains(trimmedSearch)) ||
                    (s.RejectionReason != null && s.RejectionReason.ToLower().Contains(trimmedSearch)) ||
                    s.ReceivingEmployee.ToLower().Contains(trimmedSearch) ||
                    s.Material.ToLower().Contains(trimmedSearch) ||
                    s.Action.ToLower().Contains(trimmedSearch));
            }

            var storageList = await storageQuery.ToListAsync();
            foreach (var s in storageList)
            {
                allItems.Add(MapStorageToReviewItem(s));
            }
        }

        // 3. Attendance history
        if (normType == "ALL" || normType == "ATTENDANCE")
        {
            var attQuery = _db.AttendanceSubmissions
                .AsNoTracking()
                .Include(a => a.Records)
                    .ThenInclude(r => r.User)
                .Include(a => a.Attachments)
                .AsQueryable();

            if (normStatus == "APPROVED")
            {
                attQuery = attQuery.Where(a => a.ApprovalStatus == "APPROVED" || a.ApprovalStatus == "MIXED");
            }
            else if (normStatus == "REJECTED")
            {
                attQuery = attQuery.Where(a => a.ApprovalStatus == "REJECTED");
            }
            else if (normStatus == "PENDING")
            {
                attQuery = attQuery.Where(a => a.ApprovalStatus == "PENDING" || a.ApprovalStatus == "PARTIALLY_REVIEWED");
            }
            else
            {
                attQuery = attQuery.Where(a => a.ApprovalStatus == "APPROVED" || a.ApprovalStatus == "REJECTED" || a.ApprovalStatus == "MIXED");
            }

            if (!string.IsNullOrEmpty(trimmedEmp))
            {
                attQuery = attQuery.Where(a => a.SubmittedBy != null && a.SubmittedBy.ToLower().Contains(trimmedEmp));
            }

            if (!string.IsNullOrEmpty(trimmedRev))
            {
                attQuery = attQuery.Where(a => a.ReviewedBy != null && a.ReviewedBy.ToLower().Contains(trimmedRev));
            }

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                attQuery = attQuery.Where(a => (a.ReviewedAt.HasValue && a.ReviewedAt.Value >= fDate) || a.AttendanceDate >= fDate);
            }

            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                attQuery = attQuery.Where(a => (a.ReviewedAt.HasValue && a.ReviewedAt.Value <= tDate) || a.AttendanceDate <= tDate);
            }

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                attQuery = attQuery.Where(a =>
                    a.Id.ToString().Contains(trimmedSearch) ||
                    (a.SubmittedBy != null && a.SubmittedBy.ToLower().Contains(trimmedSearch)) ||
                    (a.ReviewedBy != null && a.ReviewedBy.ToLower().Contains(trimmedSearch)) ||
                    (a.RejectionReason != null && a.RejectionReason.ToLower().Contains(trimmedSearch)) ||
                    (a.Notes != null && a.Notes.ToLower().Contains(trimmedSearch)));
            }

            var attList = await attQuery.ToListAsync();
            foreach (var a in attList)
            {
                allItems.Add(MapAttendanceToReviewItem(a));
            }
        }

        // 4. Laboratory history
        if (normType == "ALL" || normType == "LABORATORY" || normType == "LAB")
        {
            // 4.a Raw Material Reports
            var labRawQuery = _db.LaboratoryRawMaterialReports.AsNoTracking().AsQueryable();

            if (normStatus == "APPROVED")
            {
                labRawQuery = labRawQuery.Where(r => r.ApprovalStatus == "APPROVED");
            }
            else if (normStatus == "REJECTED")
            {
                labRawQuery = labRawQuery.Where(r => r.ApprovalStatus == "REJECTED");
            }
            else if (normStatus == "PENDING")
            {
                labRawQuery = labRawQuery.Where(r => r.ApprovalStatus == "PENDING");
            }
            else
            {
                labRawQuery = labRawQuery.Where(r => r.ApprovalStatus == "APPROVED" || r.ApprovalStatus == "REJECTED");
            }

            if (!string.IsNullOrEmpty(trimmedPo))
            {
                labRawQuery = labRawQuery.Where(r => r.PONumber != null && r.PONumber.ToLower().Contains(trimmedPo));
            }

            if (!string.IsNullOrEmpty(trimmedEmp))
            {
                labRawQuery = labRawQuery.Where(r => r.ResponsibleEmployee != null && r.ResponsibleEmployee.ToLower().Contains(trimmedEmp));
            }

            if (!string.IsNullOrEmpty(trimmedRev))
            {
                labRawQuery = labRawQuery.Where(r => r.ReviewedBy != null && r.ReviewedBy.ToLower().Contains(trimmedRev));
            }

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                labRawQuery = labRawQuery.Where(r => (r.ReviewedAt.HasValue && r.ReviewedAt.Value >= fDate) || r.AnalysisDateTime >= fDate);
            }

            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                labRawQuery = labRawQuery.Where(r => (r.ReviewedAt.HasValue && r.ReviewedAt.Value <= tDate) || r.AnalysisDateTime <= tDate);
            }

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                labRawQuery = labRawQuery.Where(r =>
                    r.ProductionReferenceNumber.ToString().Contains(trimmedSearch) ||
                    r.RawMaterialDescription.ToLower().Contains(trimmedSearch) ||
                    (r.SupplierName != null && r.SupplierName.ToLower().Contains(trimmedSearch)) ||
                    (r.PONumber != null && r.PONumber.ToLower().Contains(trimmedSearch)) ||
                    (r.ResponsibleEmployee != null && r.ResponsibleEmployee.ToLower().Contains(trimmedSearch)) ||
                    (r.ReviewedBy != null && r.ReviewedBy.ToLower().Contains(trimmedRev)) ||
                    (r.RejectionReason != null && r.RejectionReason.ToLower().Contains(trimmedSearch)));
            }

            var labRawList = await labRawQuery.ToListAsync();
            foreach (var r in labRawList)
            {
                allItems.Add(MapLaboratoryRawReportToReviewItem(r));
            }

            // 4.b Final Product Conformity Certificates
            var labFinalQuery = _db.LaboratoryFinalConformityCertificates.AsNoTracking().AsQueryable();

            if (normStatus == "APPROVED")
            {
                labFinalQuery = labFinalQuery.Where(c => c.ApprovalStatus == "APPROVED");
            }
            else if (normStatus == "REJECTED")
            {
                labFinalQuery = labFinalQuery.Where(c => c.ApprovalStatus == "REJECTED");
            }
            else if (normStatus == "PENDING")
            {
                labFinalQuery = labFinalQuery.Where(c => c.ApprovalStatus == "PENDING");
            }
            else
            {
                labFinalQuery = labFinalQuery.Where(c => c.ApprovalStatus == "APPROVED" || c.ApprovalStatus == "REJECTED");
            }

            if (!string.IsNullOrEmpty(trimmedEmp))
            {
                labFinalQuery = labFinalQuery.Where(c => c.ResponsibleEmployee != null && c.ResponsibleEmployee.ToLower().Contains(trimmedEmp));
            }

            if (!string.IsNullOrEmpty(trimmedRev))
            {
                labFinalQuery = labFinalQuery.Where(c => c.ReviewedBy != null && c.ReviewedBy.ToLower().Contains(trimmedRev));
            }

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                labFinalQuery = labFinalQuery.Where(c => (c.ReviewedAt.HasValue && c.ReviewedAt.Value >= fDate) || c.ProductionCompletionDateTime >= fDate);
            }

            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                labFinalQuery = labFinalQuery.Where(c => (c.ReviewedAt.HasValue && c.ReviewedAt.Value <= tDate) || c.ProductionCompletionDateTime <= tDate);
            }

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                labFinalQuery = labFinalQuery.Where(c =>
                    c.CertificateNumber.ToLower().Contains(trimmedSearch) ||
                    c.ProductionReferenceNumber.ToString().Contains(trimmedSearch) ||
                    c.FinalProductName.ToLower().Contains(trimmedSearch) ||
                    (c.ResponsibleEmployee != null && c.ResponsibleEmployee.ToLower().Contains(trimmedSearch)) ||
                    (c.ReviewedBy != null && c.ReviewedBy.ToLower().Contains(trimmedRev)) ||
                    (c.RejectionReason != null && c.RejectionReason.ToLower().Contains(trimmedSearch)));
            }

            var labFinalList = await labFinalQuery.ToListAsync();
            foreach (var c in labFinalList)
            {
                allItems.Add(MapLaboratoryFinalCertToReviewItem(c));
            }
        }

        // Apply sorting
        IEnumerable<ControlMonitoringReviewItemDto> sortedQuery = allItems;
        var isAsc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        switch ((sortBy ?? "reviewedAt").Trim().ToLowerInvariant())
        {
            case "submissiondate":
            case "date":
                sortedQuery = isAsc
                    ? sortedQuery.OrderBy(r => r.SubmissionDate)
                    : sortedQuery.OrderByDescending(r => r.SubmissionDate);
                break;
            case "status":
                sortedQuery = isAsc
                    ? sortedQuery.OrderBy(r => r.Status)
                    : sortedQuery.OrderByDescending(r => r.Status);
                break;
            case "department":
            case "formtype":
                sortedQuery = isAsc
                    ? sortedQuery.OrderBy(r => r.FormType)
                    : sortedQuery.OrderByDescending(r => r.FormType);
                break;
            case "po":
            case "ponumber":
                sortedQuery = isAsc
                    ? sortedQuery.OrderBy(r => r.PONumber ?? string.Empty)
                    : sortedQuery.OrderByDescending(r => r.PONumber ?? string.Empty);
                break;
            case "reviewedat":
            case "reviewdate":
            default:
                sortedQuery = isAsc
                    ? sortedQuery.OrderBy(r => r.ReviewedAt ?? r.SubmissionDate)
                    : sortedQuery.OrderByDescending(r => r.ReviewedAt ?? r.SubmissionDate);
                break;
        }

        var totalCount = allItems.Count;
        var pagedItems = sortedQuery.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        // Attach full audit history for the paginated items
        var transIds = pagedItems.Where(r => r.FormType == "Transportation").Select(r => r.FormId).ToList();
        var storageIds = pagedItems.Where(r => r.FormType == "Storage").Select(r => r.FormId).ToList();
        var attendanceIds = pagedItems.Where(r => r.FormType == "Attendance").Select(r => r.FormId).ToList();
        var labIds = pagedItems.Where(r => r.FormType == "Laboratory").Select(r => r.FormId).ToList();
        var labFinalIds = pagedItems.Where(r => r.FormType == "LaboratoryFinalCert").Select(r => r.FormId).ToList();

        if (transIds.Count > 0 || storageIds.Count > 0 || attendanceIds.Count > 0 || labIds.Count > 0 || labFinalIds.Count > 0)
        {
            var histories = await _db.FormApprovalHistories
                .AsNoTracking()
                .Where(h => (h.FormType == "Transportation" && transIds.Contains(h.FormId)) ||
                            (h.FormType == "Storage" && storageIds.Contains(h.FormId)) ||
                            (h.FormType == "Attendance" && attendanceIds.Contains(h.FormId)) ||
                            (h.FormType == "Laboratory" && labIds.Contains(h.FormId)) ||
                            (h.FormType == "LaboratoryFinalCert" && labFinalIds.Contains(h.FormId)))
                .OrderByDescending(h => h.Timestamp)
                .ToListAsync();

            var historyLookup = histories.ToLookup(h => $"{h.FormType.ToLowerInvariant()}_{h.FormId}");

            foreach (var r in pagedItems)
            {
                var key = $"{r.FormType.ToLowerInvariant()}_{r.FormId}";
                if (historyLookup.Contains(key))
                {
                    r.AuditTrail = historyLookup[key].Select(h => new FormApprovalHistoryDto
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
                    }).ToList();
                }
            }
        }

        var pagedResult = new PagedHistoryResultDto
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedHistoryResultDto>.Ok(pagedResult));
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

        // Try storage (Operations or legacy Transactions)
        var storageOp = await _db.StorageOperations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (storageOp != null)
        {
            return await GetFormDetails("storage", id);
        }

        var storage = await _db.StorageTransactions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (storage != null)
        {
            return await GetFormDetails("storage", id);
        }

        // Try attendance
        var att = await _db.AttendanceSubmissions.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (att != null)
        {
            return await GetFormDetails("attendance", id);
        }

        // Try laboratory raw material report
        var labRaw = await _db.LaboratoryRawMaterialReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (labRaw != null)
        {
            return await GetFormDetails("laboratory", id);
        }

        // Try laboratory final conformity certificate
        var labFinal = await _db.LaboratoryFinalConformityCertificates.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (labFinal != null)
        {
            return await GetFormDetails("laboratoryfinalcert", id);
        }

        return NotFound(ApiResponse.Fail($"Request #{id} not found in Transportation, Storage, Attendance, or Laboratory."));
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
            var storageOp = await _db.StorageOperations
                .AsNoTracking()
                .Include(o => o.StorageLocation)
                .Include(o => o.Client)
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (storageOp != null)
            {
                item = MapStorageOperationToReviewItem(storageOp);
            }
            else
            {
                var storage = await _db.StorageTransactions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
                if (storage == null) return NotFound(ApiResponse.Fail("Storage form not found."));
                item = MapStorageToReviewItem(storage);
            }
        }
        else if (normType == "attendance")
        {
            var attendanceSub = await _db.AttendanceSubmissions
                .AsNoTracking()
                .Include(a => a.Records).ThenInclude(r => r.User)
                .Include(a => a.Attachments)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (attendanceSub == null) return NotFound(ApiResponse.Fail("Attendance form not found."));
            item = MapAttendanceToReviewItem(attendanceSub);
        }
        else if (normType == "laboratory" || normType == "laboratoryraw" || normType == "laboratory_raw")
        {
            var labRaw = await _db.LaboratoryRawMaterialReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (labRaw == null) return NotFound(ApiResponse.Fail("Laboratory raw material report not found."));
            item = MapLaboratoryRawReportToReviewItem(labRaw);
        }
        else if (normType == "laboratoryfinalcert" || normType == "laboratory_final" || normType == "laboratoryfinal")
        {
            var labFinal = await _db.LaboratoryFinalConformityCertificates.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (labFinal == null) return NotFound(ApiResponse.Fail("Laboratory final conformity certificate not found."));
            item = MapLaboratoryFinalCertToReviewItem(labFinal);
        }
        else
        {
            return BadRequest(ApiResponse.Fail("Invalid form type. Must be 'Transportation', 'Storage', 'Attendance', or 'Laboratory'."));
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
    /// Approve an individual employee attendance record without modifying other records.
    /// </summary>
    [HttpPost("attendance/records/{recordId:int}/approve")]
    [HttpPost("attendance/records/{recordId:int}/accept")]
    [ProducesResponseType(typeof(ApiResponse<ControlMonitoringReviewItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveAttendanceRecord(int recordId)
    {
        var reviewerName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "موظف التحكم والمراقبة";
        Guid? reviewerId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            reviewerId = parsedGuid;
        }
        var reviewerRole = User.FindFirst(ClaimTypes.Role)?.Value ?? AppRoles.ControlMonitoring;

        if (!AppRoles.HasControlMonitoringAccess(reviewerRole))
        {
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse.Fail("غير مصرح: اعتماد سجلات الحضور مقتصر على قسم التحكم والمراقبة والإدارة العليا."));
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var record = await _db.AttendanceRecords
                .Include(r => r.AttendanceSubmission)
                    .ThenInclude(s => s!.Records)
                .Include(r => r.AttendanceSubmission)
                    .ThenInclude(s => s!.Attachments)
                .FirstOrDefaultAsync(r => r.Id == recordId);

            if (record == null)
            {
                await dbTransaction.RollbackAsync();
                return NotFound(ApiResponse.Fail($"سجل الحضور رقم #{recordId} غير موجود."));
            }

            var submission = record.AttendanceSubmission;
            if (submission == null)
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(ApiResponse.Fail("سجل الحضور غير مرتبط بكشف معتمد."));
            }

            var prevRecordStatus = record.ApprovalStatus;
            record.ApprovalStatus = "APPROVED";
            record.ReviewedBy = reviewerName;
            record.ReviewedByUserId = reviewerId;
            record.ReviewedAt = DateTime.UtcNow;
            record.RejectionReason = null;

            // Recalculate parent submission status based on all records in this submission
            var allRecords = submission.Records.ToList();
            var newSubmissionStatus = AttendanceController.CalculateSubmissionStatus(allRecords);
            submission.ApprovalStatus = newSubmissionStatus;

            if (newSubmissionStatus == "APPROVED" || newSubmissionStatus == "REJECTED" || newSubmissionStatus == "MIXED")
            {
                submission.ReviewedBy = reviewerName;
                submission.ReviewedByUserId = reviewerId;
                submission.ReviewedAt = DateTime.UtcNow;
            }

            var txNumber = $"ATT-{submission.Id:D4}";
            var history = new FormApprovalHistory
            {
                FormType = "Attendance",
                FormId = submission.Id,
                PONumber = null,
                TransactionOrMovementNumber = txNumber,
                PreviousStatus = prevRecordStatus,
                NewStatus = "APPROVED",
                Action = "RECORD_APPROVED",
                ActorName = reviewerName,
                ActorUserId = reviewerId,
                ActorRole = reviewerRole,
                Comment = $"تم اعتماد سجل الحضور للموظف: {record.EmployeeName} (الحالة: {record.Status}) من قبل قسم التحكم والمراقبة.",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(history);

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Attendance record #{RecordId} ({Emp}) approved by {Reviewer}. Form #{FormId} is now {FormStatus}",
                record.Id, record.EmployeeName, reviewerName, submission.Id, newSubmissionStatus);

            await NotifyStatusChangeAsync("Attendance", submission.Id, null, txNumber, newSubmissionStatus, reviewerName, null, submission.SubmittedBy, submission.SubmittedByUserId);

            var fullAtt = await _db.AttendanceSubmissions
                .AsNoTracking()
                .Include(a => a.Records).ThenInclude(r => r.User)
                .Include(a => a.Attachments)
                .FirstAsync(a => a.Id == submission.Id);

            var dto = MapAttendanceToReviewItem(fullAtt);
            return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, $"تم اعتماد سجل الموظف {record.EmployeeName} بنجاح."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Error approving attendance record #{RecordId}", recordId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء اعتماد سجل الحضور."));
        }
    }

    /// <summary>
    /// Reject an individual employee attendance record with mandatory rejection reason,
    /// without rejecting or modifying the remaining records in the attendance form.
    /// </summary>
    [HttpPost("attendance/records/{recordId:int}/reject")]
    [ProducesResponseType(typeof(ApiResponse<ControlMonitoringReviewItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectAttendanceRecord(int recordId, [FromBody] RejectDecisionDto? body)
    {
        var reviewerName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "موظف التحكم والمراقبة";
        Guid? reviewerId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            reviewerId = parsedGuid;
        }
        var reviewerRole = User.FindFirst(ClaimTypes.Role)?.Value ?? AppRoles.ControlMonitoring;

        if (!AppRoles.HasControlMonitoringAccess(reviewerRole))
        {
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse.Fail("غير مصرح: رفض سجلات الحضور مقتصر على قسم التحكم والمراقبة والإدارة العليا."));
        }

        var reason = body?.GetReason()?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest(ApiResponse.Fail("سبب الرفض إلزامي ومطلوب عند رفض سجل الموظف."));
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var record = await _db.AttendanceRecords
                .Include(r => r.AttendanceSubmission)
                    .ThenInclude(s => s!.Records)
                .Include(r => r.AttendanceSubmission)
                    .ThenInclude(s => s!.Attachments)
                .FirstOrDefaultAsync(r => r.Id == recordId);

            if (record == null)
            {
                await dbTransaction.RollbackAsync();
                return NotFound(ApiResponse.Fail($"سجل الحضور رقم #{recordId} غير موجود."));
            }

            var submission = record.AttendanceSubmission;
            if (submission == null)
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(ApiResponse.Fail("سجل الحضور غير مرتبط بكشف معتمد."));
            }

            var prevRecordStatus = record.ApprovalStatus;
            record.ApprovalStatus = "REJECTED";
            record.RejectionReason = reason;
            record.ReviewedBy = reviewerName;
            record.ReviewedByUserId = reviewerId;
            record.ReviewedAt = DateTime.UtcNow;

            // Recalculate parent submission status based on all records in this submission
            var allRecords = submission.Records.ToList();
            var newSubmissionStatus = AttendanceController.CalculateSubmissionStatus(allRecords);
            submission.ApprovalStatus = newSubmissionStatus;

            if (newSubmissionStatus == "APPROVED" || newSubmissionStatus == "REJECTED" || newSubmissionStatus == "MIXED")
            {
                submission.ReviewedBy = reviewerName;
                submission.ReviewedByUserId = reviewerId;
                submission.ReviewedAt = DateTime.UtcNow;
            }

            var txNumber = $"ATT-{submission.Id:D4}";
            var history = new FormApprovalHistory
            {
                FormType = "Attendance",
                FormId = submission.Id,
                PONumber = null,
                TransactionOrMovementNumber = txNumber,
                PreviousStatus = prevRecordStatus,
                NewStatus = "REJECTED",
                Action = "RECORD_REJECTED",
                ActorName = reviewerName,
                ActorUserId = reviewerId,
                ActorRole = reviewerRole,
                Comment = $"تم رفض سجل الحضور للموظف: {record.EmployeeName}. سبب الرفض: {reason}",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(history);

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Attendance record #{RecordId} ({Emp}) rejected by {Reviewer} (Reason: {Reason}). Form #{FormId} is now {FormStatus}",
                record.Id, record.EmployeeName, reviewerName, reason, submission.Id, newSubmissionStatus);

            await NotifyStatusChangeAsync("Attendance", submission.Id, null, txNumber, newSubmissionStatus, reviewerName, reason, submission.SubmittedBy, submission.SubmittedByUserId);

            var fullAtt = await _db.AttendanceSubmissions
                .AsNoTracking()
                .Include(a => a.Records).ThenInclude(r => r.User)
                .Include(a => a.Attachments)
                .FirstAsync(a => a.Id == submission.Id);

            var dto = MapAttendanceToReviewItem(fullAtt);
            return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, $"تم رفض سجل الموظف {record.EmployeeName} وحفظ سبب الرفض بنجاح."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Error rejecting attendance record #{RecordId}", recordId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء رفض سجل الحضور."));
        }
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
                await NotifyStatusChangeAsync("Transportation", trans.Id, trans.PONumber, txNumber, "APPROVED", reviewerName, null, trans.SubmittedBy, trans.SubmittedByUserId);

                var dto = MapTransportationToReviewItem(trans);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تم اعتماد حركة النقل بنجاح."));
            }
            else if (normType == "storage")
            {
                var isStorageOp = await _db.StorageOperations.AnyAsync(o => o.Id == id);
                if (isStorageOp)
                {
                    await dbTransaction.RollbackAsync();
                    var (success, errorMsg, opDto) = await StorageController.ApproveStorageOperationCoreAsync(
                        _db, _logger, _hubContext, id, reviewerName, reviewerId, reviewerRole);

                    if (!success)
                    {
                        return BadRequest(ApiResponse.Fail(errorMsg ?? "حدث خطأ أثناء اعتماد حركة المستودع."));
                    }

                    var opEntity = await _db.StorageOperations
                        .AsNoTracking()
                        .Include(o => o.StorageLocation)
                        .Include(o => o.Client)
                        .Include(o => o.Items)
                        .FirstAsync(o => o.Id == id);

                    var reviewItem = MapStorageOperationToReviewItem(opEntity);
                    return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(reviewItem, "تم اعتماد حركة المستودع وترحيل المخزون بنجاح."));
                }

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
                await NotifyStatusChangeAsync("Storage", storage.Id, storage.PONumber, txNumber, "APPROVED", reviewerName, null, storage.CreatedBy, storage.CreatedByUserId);

                var dto = MapStorageToReviewItem(storage);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تم اعتماد حركة المستودع بنجاح."));
            }
            else if (normType == "attendance")
            {
                var att = await _db.AttendanceSubmissions
                    .Include(a => a.Records)
                    .FirstOrDefaultAsync(a => a.Id == id);
                if (att == null)
                {
                    await dbTransaction.RollbackAsync();
                    return NotFound(ApiResponse.Fail("Attendance form not found."));
                }

                if (att.ApprovalStatus == "APPROVED")
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("تم اعتماد كشف الحضور هذا بالفعل ولا يمكن إعادة معالجته."));
                }

                prevStatus = att.ApprovalStatus;
                txNumber = $"ATT-{att.Id:D4}";

                att.ApprovalStatus = "APPROVED";
                att.ReviewedBy = reviewerName;
                att.ReviewedByUserId = reviewerId;
                att.ReviewedAt = DateTime.UtcNow;
                att.RejectionReason = null;

                foreach (var r in att.Records)
                {
                    r.ApprovalStatus = "APPROVED";
                    r.ReviewedBy = reviewerName;
                    r.ReviewedByUserId = reviewerId;
                    r.ReviewedAt = DateTime.UtcNow;
                    r.RejectionReason = null;
                }

                // Add audit history
                var history = new FormApprovalHistory
                {
                    FormType = "Attendance",
                    FormId = att.Id,
                    PONumber = null,
                    TransactionOrMovementNumber = txNumber,
                    PreviousStatus = prevStatus,
                    NewStatus = "APPROVED",
                    Action = "APPROVED",
                    ActorName = reviewerName,
                    ActorUserId = reviewerId,
                    ActorRole = reviewerRole,
                    Comment = "تم قبول واعتماد كشف الحضور والانصراف رسمياً من قسم التحكم والمراقبة.",
                    Timestamp = DateTime.UtcNow
                };
                await _db.FormApprovalHistories.AddAsync(history);

                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                // SignalR Broadcast
                await NotifyStatusChangeAsync("Attendance", att.Id, null, txNumber, "APPROVED", reviewerName, null, att.SubmittedBy, att.SubmittedByUserId);

                var fullAtt = await _db.AttendanceSubmissions
                    .AsNoTracking()
                    .Include(a => a.Records).ThenInclude(r => r.User)
                    .Include(a => a.Attachments)
                    .FirstAsync(a => a.Id == att.Id);

                var dto = MapAttendanceToReviewItem(fullAtt);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تم اعتماد كشف الحضور والانصراف بنجاح."));
            }
            else if (normType == "laboratory" || normType == "laboratoryraw" || normType == "laboratory_raw" || normType == "laboratoryrawmaterial")
            {
                var report = await _db.LaboratoryRawMaterialReports.FirstOrDefaultAsync(l => l.Id == id);
                if (report == null)
                {
                    await dbTransaction.RollbackAsync();
                    return NotFound(ApiResponse.Fail("Laboratory raw material report not found."));
                }

                if (report.ApprovalStatus == "APPROVED")
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("تم اعتماد تقرير فحص المواد الخام هذا بالفعل ولا يمكن إعادة معالجته."));
                }

                prevStatus = report.ApprovalStatus;
                txNumber = $"REF-{report.ProductionReferenceNumber:D4}";

                report.ApprovalStatus = "APPROVED";
                report.ReviewedBy = reviewerName;
                report.ReviewedByUserId = reviewerId;
                report.ReviewedAt = DateTime.UtcNow;
                report.RejectionReason = null;

                var history = new FormApprovalHistory
                {
                    FormType = "Laboratory",
                    FormId = report.Id,
                    PONumber = report.PONumber,
                    TransactionOrMovementNumber = txNumber,
                    PreviousStatus = prevStatus,
                    NewStatus = "APPROVED",
                    Action = "APPROVED",
                    ActorName = reviewerName,
                    ActorUserId = reviewerId,
                    ActorRole = reviewerRole,
                    Comment = "تم قبول واعتماد تقرير فحص المواد الخام من قسم التحكم والمراقبة.",
                    Timestamp = DateTime.UtcNow
                };
                await _db.FormApprovalHistories.AddAsync(history);

                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                await NotifyStatusChangeAsync("Laboratory", report.Id, report.PONumber, txNumber, "APPROVED", reviewerName, null, report.ResponsibleEmployee, report.ResponsibleUserId);

                var dto = MapLaboratoryRawReportToReviewItem(report);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تم اعتماد تقرير فحص المواد الخام بنجاح."));
            }
            else if (normType == "laboratoryfinalcert" || normType == "laboratory_final" || normType == "laboratoryfinal")
            {
                var cert = await _db.LaboratoryFinalConformityCertificates.FirstOrDefaultAsync(c => c.Id == id);
                if (cert == null)
                {
                    await dbTransaction.RollbackAsync();
                    return NotFound(ApiResponse.Fail("Final product conformity certificate not found."));
                }

                if (cert.ApprovalStatus == "APPROVED")
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("تم اعتماد شهادة مطابقة المنتج النهائي هذه بالفعل ولا يمكن إعادة معالجتها."));
                }

                prevStatus = cert.ApprovalStatus;
                txNumber = cert.CertificateNumber;

                cert.ApprovalStatus = "APPROVED";
                cert.ReviewedBy = reviewerName;
                cert.ReviewedByUserId = reviewerId;
                cert.ReviewedAt = DateTime.UtcNow;
                cert.RejectionReason = null;

                var history = new FormApprovalHistory
                {
                    FormType = "LaboratoryFinalCert",
                    FormId = cert.Id,
                    PONumber = null,
                    TransactionOrMovementNumber = txNumber,
                    PreviousStatus = prevStatus,
                    NewStatus = "APPROVED",
                    Action = "APPROVED",
                    ActorName = reviewerName,
                    ActorUserId = reviewerId,
                    ActorRole = reviewerRole,
                    Comment = "تم قبول واعتماد شهادة مطابقة المنتج النهائي من قسم التحكم والمراقبة.",
                    Timestamp = DateTime.UtcNow
                };
                await _db.FormApprovalHistories.AddAsync(history);

                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                await NotifyStatusChangeAsync("LaboratoryFinalCert", cert.Id, null, txNumber, "APPROVED", reviewerName, null, cert.ResponsibleEmployee, cert.ResponsibleUserId);

                var dto = MapLaboratoryFinalCertToReviewItem(cert);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تم اعتماد شهادة مطابقة المنتج النهائي بنجاح."));
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
                await NotifyStatusChangeAsync("Transportation", trans.Id, trans.PONumber, txNumber, "REJECTED", reviewerName, reason, trans.SubmittedBy, trans.SubmittedByUserId);

                var dto = MapTransportationToReviewItem(trans);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تم رفض الحركة وحفظ سبب الرفض بنجاح."));
            }
            else if (normType == "storage")
            {
                var isStorageOp = await _db.StorageOperations.AnyAsync(o => o.Id == id);
                if (isStorageOp)
                {
                    await dbTransaction.RollbackAsync();
                    var (success, errorMsg, opDto) = await StorageController.RejectStorageOperationCoreAsync(
                        _db, _logger, _hubContext, id, reason ?? "تم الرفض من قبل قسم التحكم والمراقبة.", reviewerName, reviewerId, reviewerRole);

                    if (!success)
                    {
                        return BadRequest(ApiResponse.Fail(errorMsg ?? "حدث خطأ أثناء رفض حركة المستودع."));
                    }

                    var opEntity = await _db.StorageOperations
                        .AsNoTracking()
                        .Include(o => o.StorageLocation)
                        .Include(o => o.Client)
                        .Include(o => o.Items)
                        .FirstAsync(o => o.Id == id);

                    var reviewItem = MapStorageOperationToReviewItem(opEntity);
                    return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(reviewItem, "تم رفض الحركة وحفظ سبب الرفض بنجاح."));
                }

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
                await NotifyStatusChangeAsync("Storage", storage.Id, storage.PONumber, txNumber, "REJECTED", reviewerName, reason, storage.CreatedBy, storage.CreatedByUserId);

                var dto = MapStorageToReviewItem(storage);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تم رفض الحركة وحفظ سبب الرفض بنجاح."));
            }
            else if (normType == "attendance")
            {
                var att = await _db.AttendanceSubmissions
                    .Include(a => a.Records)
                    .FirstOrDefaultAsync(a => a.Id == id);
                if (att == null)
                {
                    await dbTransaction.RollbackAsync();
                    return NotFound(ApiResponse.Fail("Attendance form not found."));
                }

                if (att.ApprovalStatus == "APPROVED")
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("لا يمكن رفض كشف تم اعتماده مسبقاً."));
                }

                prevStatus = att.ApprovalStatus;
                txNumber = $"ATT-{att.Id:D4}";

                att.ApprovalStatus = "REJECTED";
                att.ReviewedBy = reviewerName;
                att.ReviewedByUserId = reviewerId;
                att.ReviewedAt = DateTime.UtcNow;
                att.RejectionReason = reason;

                foreach (var r in att.Records)
                {
                    r.ApprovalStatus = "REJECTED";
                    r.ReviewedBy = reviewerName;
                    r.ReviewedByUserId = reviewerId;
                    r.ReviewedAt = DateTime.UtcNow;
                    r.RejectionReason = reason;
                }

                // Add audit history
                var history = new FormApprovalHistory
                {
                    FormType = "Attendance",
                    FormId = att.Id,
                    PONumber = null,
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
                await NotifyStatusChangeAsync("Attendance", att.Id, null, txNumber, "REJECTED", reviewerName, reason, att.SubmittedBy, att.SubmittedByUserId);

                var fullAtt = await _db.AttendanceSubmissions
                    .AsNoTracking()
                    .Include(a => a.Records).ThenInclude(r => r.User)
                    .Include(a => a.Attachments)
                    .FirstAsync(a => a.Id == att.Id);

                var dto = MapAttendanceToReviewItem(fullAtt);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تم رفض كشف الحضور وحفظ سبب الرفض بنجاح."));
            }
            else if (normType == "laboratory" || normType == "laboratoryraw" || normType == "laboratory_raw" || normType == "laboratoryrawmaterial")
            {
                var report = await _db.LaboratoryRawMaterialReports.FirstOrDefaultAsync(l => l.Id == id);
                if (report == null)
                {
                    await dbTransaction.RollbackAsync();
                    return NotFound(ApiResponse.Fail("Laboratory raw material report not found."));
                }

                if (report.ApprovalStatus == "APPROVED")
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("لا يمكن رفض تقرير فحص مواد خام معتمد بالفعل."));
                }
                if (report.ApprovalStatus == "REJECTED")
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("تم رفض هذا التقرير بالفعل ولا يمكن إعادة رفضه."));
                }

                prevStatus = report.ApprovalStatus;
                txNumber = $"REF-{report.ProductionReferenceNumber:D4}";

                report.ApprovalStatus = "REJECTED";
                report.ReviewedBy = reviewerName;
                report.ReviewedByUserId = reviewerId;
                report.ReviewedAt = DateTime.UtcNow;
                report.RejectionReason = reason;

                var history = new FormApprovalHistory
                {
                    FormType = "Laboratory",
                    FormId = report.Id,
                    PONumber = report.PONumber,
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

                await NotifyStatusChangeAsync("Laboratory", report.Id, report.PONumber, txNumber, "REJECTED", reviewerName, reason, report.ResponsibleEmployee, report.ResponsibleUserId);

                var dto = MapLaboratoryRawReportToReviewItem(report);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تم رفض تقرير فحص المواد الخام وحفظ سبب الرفض بنجاح."));
            }
            else if (normType == "laboratoryfinalcert" || normType == "laboratory_final" || normType == "laboratoryfinal")
            {
                var cert = await _db.LaboratoryFinalConformityCertificates.FirstOrDefaultAsync(c => c.Id == id);
                if (cert == null)
                {
                    await dbTransaction.RollbackAsync();
                    return NotFound(ApiResponse.Fail("Final product conformity certificate not found."));
                }

                if (cert.ApprovalStatus == "APPROVED")
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("لا يمكن رفض شهادة مطابقة معتمدة بالفعل."));
                }
                if (cert.ApprovalStatus == "REJECTED")
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("تم رفض هذه الشهادة بالفعل ولا يمكن إعادة رفضها."));
                }

                prevStatus = cert.ApprovalStatus;
                txNumber = cert.CertificateNumber;

                cert.ApprovalStatus = "REJECTED";
                cert.ReviewedBy = reviewerName;
                cert.ReviewedByUserId = reviewerId;
                cert.ReviewedAt = DateTime.UtcNow;
                cert.RejectionReason = reason;

                var history = new FormApprovalHistory
                {
                    FormType = "LaboratoryFinalCert",
                    FormId = cert.Id,
                    PONumber = null,
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

                await NotifyStatusChangeAsync("LaboratoryFinalCert", cert.Id, null, txNumber, "REJECTED", reviewerName, reason, cert.ResponsibleEmployee, cert.ResponsibleUserId);

                var dto = MapLaboratoryFinalCertToReviewItem(cert);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تم رفض شهادة مطابقة المنتج النهائي وحفظ سبب الرفض بنجاح."));
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
                Comment = comment ?? "تمت إعادة إرسال حركة النقل للمراجعة والتدقيق بعد التصحيح.",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(history);
            await _db.SaveChangesAsync();
            await NotifyStatusChangeAsync("Transportation", trans.Id, trans.PONumber, $"#{trans.TransactionNumber}", "PENDING", submitterName, null, trans.SubmittedBy, trans.SubmittedByUserId);

            var dto = MapTransportationToReviewItem(trans);
            return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تمت إعادة إرسال حركة النقل لقسم التحكم والمراقبة بنجاح."));
        }
        else if (normType == "storage")
        {
            var isStorageOp = await _db.StorageOperations.AnyAsync(o => o.Id == id);
            if (isStorageOp)
            {
                var (success, errorMsg, opDto) = await StorageController.ResubmitStorageOperationCoreAsync(
                    _db, _logger, _hubContext, id, submitterName, null);

                if (!success)
                {
                    return BadRequest(ApiResponse.Fail(errorMsg ?? "حدث خطأ أثناء إعادة إرسال حركة المستودع."));
                }

                var opEntity = await _db.StorageOperations
                    .AsNoTracking()
                    .Include(o => o.StorageLocation)
                    .Include(o => o.Client)
                    .Include(o => o.Items)
                    .FirstAsync(o => o.Id == id);

                var reviewItem = MapStorageOperationToReviewItem(opEntity);
                return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(reviewItem, "تمت إعادة إرسال حركة المستودع لقسم التحكم والمراقبة بنجاح."));
            }

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

            await NotifyStatusChangeAsync("Storage", storage.Id, storage.PONumber, $"#{storage.MovementNumber}", "PENDING", submitterName, null, storage.CreatedBy, storage.CreatedByUserId);

            var dto = MapStorageToReviewItem(storage);
            return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تمت إعادة إرسال حركة المستودع لقسم التحكم والمراقبة بنجاح."));
        }
        else if (normType == "attendance")
        {
            var att = await _db.AttendanceSubmissions.Include(a => a.Records).FirstOrDefaultAsync(a => a.Id == id);
            if (att == null) return NotFound(ApiResponse.Fail("Attendance form not found."));

            var prevStatus = att.ApprovalStatus;
            att.ApprovalStatus = "PENDING";
            var comment = body?.GetComment()?.Trim();
            if (!string.IsNullOrWhiteSpace(comment))
            {
                att.Notes = comment;
            }

            foreach (var r in att.Records)
            {
                r.ApprovalStatus = "PENDING";
            }

            var txNumber = $"ATT-{att.Id:D4}";
            var history = new FormApprovalHistory
            {
                FormType = "Attendance",
                FormId = att.Id,
                PONumber = null,
                TransactionOrMovementNumber = txNumber,
                PreviousStatus = prevStatus,
                NewStatus = "PENDING",
                Action = "RESUBMITTED",
                ActorName = submitterName,
                ActorRole = submitterRole,
                Comment = comment ?? "تمت إعادة إرسال كشف الحضور للمراجعة والتدقيق بعد التصحيح.",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(history);
            await _db.SaveChangesAsync();

            await NotifyStatusChangeAsync("Attendance", att.Id, null, txNumber, "PENDING", submitterName, null, att.SubmittedBy, att.SubmittedByUserId);

            var fullAtt = await _db.AttendanceSubmissions
                .AsNoTracking()
                .Include(a => a.Records).ThenInclude(r => r.User)
                .Include(a => a.Attachments)
                .FirstAsync(a => a.Id == att.Id);

            var dto = MapAttendanceToReviewItem(fullAtt);
            return Ok(ApiResponse<ControlMonitoringReviewItemDto>.Ok(dto, "تمت إعادة إرسال كشف الحضور لقسم التحكم والمراقبة بنجاح."));
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
        if (!string.IsNullOrEmpty(normSource) && normSource != "transportation" && normSource != "storage" && normSource != "attendance")
        {
            // If source wasn't transportation, storage, or attendance, assume it might be fileName with empty source
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
            // 2.a Check modern StorageOperations (Import, Export, Transfer)
            var op = await _db.StorageOperations.AsNoTracking().FirstOrDefaultAsync(x =>
                x.WeighbridgeFileName == cleanName ||
                x.QocFileName == cleanName ||
                x.DocumentFileName == cleanName ||
                (x.WeighbridgeAttachmentPath != null && (x.WeighbridgeAttachmentPath == cleanName || x.WeighbridgeAttachmentPath.Contains(cleanName))) ||
                (x.QocAttachmentPath != null && (x.QocAttachmentPath == cleanName || x.QocAttachmentPath.Contains(cleanName))) ||
                (x.DocumentAttachmentPath != null && (x.DocumentAttachmentPath == cleanName || x.DocumentAttachmentPath.Contains(cleanName))));

            if (op != null)
            {
                var candidateOps = new[]
                {
                    (op.WeighbridgeAttachmentPath, op.WeighbridgeFileName),
                    (op.QocAttachmentPath, op.QocFileName),
                    (op.DocumentAttachmentPath, op.DocumentFileName)
                };

                foreach (var (path, orig) in candidateOps)
                {
                    if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(orig)) continue;
                    var storedFn = !string.IsNullOrWhiteSpace(path) ? Path.GetFileName(path) : orig!;
                    foreach (var root in rootPaths)
                    {
                        var diskP = Path.Combine(root, "uploads", "storage", storedFn);
                        if (System.IO.File.Exists(diskP)) return (diskP, orig);
                    }
                }
            }

            // 2.b Check legacy StorageTransactions
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

        // 3. Database reverse-lookup for Attendance
        if (normSource == "attendance" || string.IsNullOrEmpty(normSource))
        {
            var a = await _db.AttendanceAttachments.AsNoTracking().FirstOrDefaultAsync(x =>
                x.FileName == cleanName || x.OriginalFileName == cleanName);

            if (a != null)
            {
                foreach (var root in rootPaths)
                {
                    var diskP = Path.Combine(root, "uploads", "attendance", a.FileName);
                    if (System.IO.File.Exists(diskP)) return (diskP, a.OriginalFileName);
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
        var submitter = s.CreatedBy ?? s.ReceivingEmployee ?? "موظف المستودع";

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
            SubmittedByUserId = s.CreatedByUserId,
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

    private ControlMonitoringReviewItemDto MapStorageOperationToReviewItem(StorageOperation op)
    {
        var submitDate = op.CreatedAt != default ? op.CreatedAt : op.TransactionDate;
        var submitter = !string.IsNullOrWhiteSpace(op.ResponsibleEmployee) ? op.ResponsibleEmployee : (op.CreatedBy ?? "موظف المستودع");
        var itemsSummary = (op.Items != null && op.Items.Any())
            ? string.Join("، ", op.Items.Select(i => $"{i.MaterialName} ({i.Quantity:N3} طن)"))
            : "لا توجد أصناف";
        var totalQty = op.Items != null && op.Items.Any() ? op.Items.Sum(i => i.Quantity) : 0m;
        var actionLabel = op.OperationType switch
        {
            "IMPORT" => "وارد (إذن إضافة مواد)",
            "EXPORT" => "صادر (إذن صرف بضاعة)",
            "TRANSFER" => "تحويل (إلى خط الإنتاج)",
            _ => op.OperationType
        };

        var noteParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(op.Source)) noteParts.Add($"المورد: {op.Source} ({op.SourcePhone ?? "بدون هاتف"})");
        if (op.Client != null || !string.IsNullOrWhiteSpace(op.ClientName)) noteParts.Add($"العميل: {op.ClientName ?? op.Client?.Name} - الوجهة: {op.DestinationName ?? "غير محدد"} - هاتف: {op.ClientPhone ?? "—"}");
        if (!string.IsNullOrWhiteSpace(op.Notes)) noteParts.Add(op.Notes);

        var attachments = new List<FormAttachmentDto>();
        if (!string.IsNullOrWhiteSpace(op.WeighbridgeAttachmentPath) || !string.IsNullOrWhiteSpace(op.WeighbridgeFileName))
        {
            var fn = !string.IsNullOrWhiteSpace(op.WeighbridgeAttachmentPath)
                ? Path.GetFileName(op.WeighbridgeAttachmentPath)
                : op.WeighbridgeFileName!;
            var size = TryGetFileSize("storage", fn, op.WeighbridgeFileSize);
            attachments.Add(new FormAttachmentDto
            {
                Key = "Weighbridge",
                Title = "إيصال الميزان (Weighbridge)",
                FileName = op.WeighbridgeFileName ?? fn,
                FileType = op.WeighbridgeFileType ?? (fn.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Image"),
                FileSize = size ?? op.WeighbridgeFileSize,
                ViewUrl = $"/api/control-monitoring/attachments/storage/{fn}",
                DownloadUrl = $"/api/control-monitoring/attachments/storage/{fn}?download=true",
                UploadedBy = submitter,
                UploadDate = submitDate
            });
        }

        if (!string.IsNullOrWhiteSpace(op.QocAttachmentPath) || !string.IsNullOrWhiteSpace(op.QocFileName))
        {
            var fn = !string.IsNullOrWhiteSpace(op.QocAttachmentPath)
                ? Path.GetFileName(op.QocAttachmentPath)
                : op.QocFileName!;
            var size = TryGetFileSize("storage", fn, op.QocFileSize);
            attachments.Add(new FormAttachmentDto
            {
                Key = "QOC",
                Title = "تقرير QOC (QOC Report)",
                FileName = op.QocFileName ?? fn,
                FileType = op.QocFileType ?? (fn.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Image"),
                FileSize = size ?? op.QocFileSize,
                ViewUrl = $"/api/control-monitoring/attachments/storage/{fn}",
                DownloadUrl = $"/api/control-monitoring/attachments/storage/{fn}?download=true",
                UploadedBy = submitter,
                UploadDate = submitDate
            });
        }

        if (!string.IsNullOrWhiteSpace(op.DocumentAttachmentPath) || !string.IsNullOrWhiteSpace(op.DocumentFileName))
        {
            var fn = !string.IsNullOrWhiteSpace(op.DocumentAttachmentPath)
                ? Path.GetFileName(op.DocumentAttachmentPath)
                : op.DocumentFileName!;
            var size = TryGetFileSize("storage", fn, op.DocumentFileSize);
            attachments.Add(new FormAttachmentDto
            {
                Key = "Document",
                Title = "المستند الورقي (Written Document)",
                FileName = op.DocumentFileName ?? fn,
                FileType = op.DocumentFileType ?? (fn.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Image"),
                FileSize = size ?? op.DocumentFileSize,
                ViewUrl = $"/api/control-monitoring/attachments/storage/{fn}",
                DownloadUrl = $"/api/control-monitoring/attachments/storage/{fn}?download=true",
                UploadedBy = submitter,
                UploadDate = submitDate
            });
        }

        return new ControlMonitoringReviewItemDto
        {
            Id = op.Id,
            FormType = "Storage",
            FormId = op.Id,
            PONumber = null,
            TransactionNumber = $"#{op.TransactionNumber}",
            SubmittedBy = submitter,
            SubmittedByUserId = op.ResponsibleUserId ?? op.CreatedByUserId,
            SubmissionDate = submitDate,
            Status = string.IsNullOrWhiteSpace(op.ApprovalStatus) ? "PENDING" : op.ApprovalStatus,
            ReviewedBy = op.ReviewedBy,
            ReviewedAt = op.ReviewedAt,
            RejectionReason = op.RejectionReason,
            AttachmentCount = attachments.Count,
            Attachments = attachments,

            Material = itemsSummary,
            Action = actionLabel,
            ServiceType = op.OperationType,
            IncomingQuantity = op.OperationType == "IMPORT" ? totalQty : null,
            OutgoingQuantity = (op.OperationType == "EXPORT" || op.OperationType == "TRANSFER") ? totalQty : null,
            StorageLocation = op.StorageLocationName ?? op.StorageLocation?.Name ?? "المخزن",
            DestinationStorageLocation = op.DestinationName ?? (op.OperationType == "TRANSFER" ? "خط الإنتاج" : null),
            ResponsibleEmployee = op.ResponsibleEmployee ?? submitter,
            CreatedBy = op.CreatedBy ?? submitter,
            CreatedAt = op.CreatedAt,
            Notes = noteParts.Count > 0 ? string.Join(" | ", noteParts) : null
        };
    }

    private ControlMonitoringReviewItemDto MapAttendanceToReviewItem(AttendanceSubmission a)
    {
        var attachments = new List<FormAttachmentDto>();
        var submitDate = a.SubmittedAt;
        var submitter = a.SubmittedBy ?? "مسؤول الحضور";

        if (a.Attachments != null)
        {
            foreach (var att in a.Attachments)
            {
                var size = TryGetFileSize("attendance", att.FileName, att.FileSize);
                attachments.Add(new FormAttachmentDto
                {
                    Key = "Evidence",
                    Title = att.OriginalFileName,
                    FileName = att.FileName,
                    FileType = att.FileType.Contains("pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Image",
                    FileSize = size ?? att.FileSize,
                    ViewUrl = $"/api/control-monitoring/attachments/attendance/{att.FileName}",
                    DownloadUrl = $"/api/control-monitoring/attachments/attendance/{att.FileName}?download=true",
                    UploadedBy = submitter,
                    UploadDate = att.UploadedAt
                });
            }
        }

        var records = new List<AttendanceRecordItemDto>();
        if (a.Records != null)
        {
            foreach (var r in a.Records)
            {
                records.Add(new AttendanceRecordItemDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    FullName = !string.IsNullOrWhiteSpace(r.EmployeeName) ? r.EmployeeName : (r.User?.FullName ?? "Unknown"),
                    Role = !string.IsNullOrWhiteSpace(r.EmployeeRole) ? r.EmployeeRole : (r.User?.Role ?? "Employee"),
                    Status = r.Status,
                    AttendanceDateTime = r.CheckInTime,
                    CheckInTime = r.CheckInTime,
                    CheckOutTime = r.CheckOutTime,
                    Notes = r.Notes,
                    IsTestData = r.IsTestData,
                    ApprovalStatus = r.ApprovalStatus,
                    ReviewedBy = r.ReviewedBy,
                    ReviewedByUserId = r.ReviewedByUserId,
                    ReviewedAt = r.ReviewedAt,
                    RejectionReason = r.RejectionReason
                });
            }
        }

        return new ControlMonitoringReviewItemDto
        {
            Id = a.Id,
            FormType = "Attendance",
            FormId = a.Id,
            PONumber = null,
            TransactionNumber = $"ATT-{a.Id:D4}",
            SubmittedBy = submitter,
            SubmittedByUserId = a.SubmittedByUserId,
            SubmissionDate = submitDate,
            Status = string.IsNullOrWhiteSpace(a.ApprovalStatus) ? "PENDING" : a.ApprovalStatus,
            ReviewedBy = a.ReviewedBy,
            ReviewedAt = a.ReviewedAt,
            RejectionReason = a.RejectionReason,
            AttachmentCount = attachments.Count,
            Attachments = attachments,
            AttendanceDate = a.AttendanceDate,
            TotalEmployeesCount = a.TotalEmployeesCount,
            PresentCount = a.PresentCount,
            AbsentCount = a.AbsentCount,
            AttendanceRecords = records,
            Notes = a.Notes
        };
    }

    private ControlMonitoringReviewItemDto MapLaboratoryRawReportToReviewItem(LaboratoryRawMaterialReport r)
    {
        var attachments = new List<FormAttachmentDto>();
        if (!string.IsNullOrWhiteSpace(r.AttachmentFileName))
        {
            var fn = Path.GetFileName(r.AttachmentFileName);
            var size = TryGetFileSize("laboratory", fn, r.AttachmentFileSize);
            attachments.Add(new FormAttachmentDto
            {
                Key = "LaboratoryRawMaterialAttachment",
                Title = "مرفق فحص المواد الخام (Raw Material Test Attachment)",
                FileName = fn,
                FileType = r.AttachmentFileType ?? (fn.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Image"),
                FileSize = size ?? r.AttachmentFileSize,
                ViewUrl = $"/api/laboratory/attachments/{fn}",
                DownloadUrl = $"/api/laboratory/attachments/{fn}?download=true",
                UploadedBy = r.ResponsibleEmployee,
                UploadDate = r.AnalysisDateTime
            });
        }

        return new ControlMonitoringReviewItemDto
        {
            Id = r.Id,
            FormType = "Laboratory",
            FormId = r.Id,
            PONumber = r.PONumber,
            TransactionNumber = $"REF-{r.ProductionReferenceNumber:D4}",
            ProductionReferenceNumber = r.ProductionReferenceNumber,
            LaboratorySection = r.LaboratorySection,
            Material = r.RawMaterialDescription,
            CompanyName = r.SupplierName,
            IncomingQuantity = r.ReceivedQuantity,
            AcceptedQuantity = r.AcceptedQuantity,
            RejectedQuantity = r.RejectedQuantity,
            InspectionDecision = r.InspectionDecision,
            Status = string.IsNullOrWhiteSpace(r.ApprovalStatus) ? "PENDING" : r.ApprovalStatus,
            SubmittedBy = r.ResponsibleEmployee,
            SubmittedByUserId = r.ResponsibleUserId,
            SubmissionDate = r.AnalysisDateTime,
            ReviewedBy = r.ReviewedBy,
            ReviewedAt = r.ReviewedAt,
            RejectionReason = r.RejectionReason,
            AttachmentCount = attachments.Count,
            Attachments = attachments,
            Notes = r.Notes,
            AnalysisResultsJson = r.AnalysisResultsJson
        };
    }

    private ControlMonitoringReviewItemDto MapLaboratoryFinalCertToReviewItem(LaboratoryFinalConformityCertificate c)
    {
        var attachments = new List<FormAttachmentDto>();
        if (!string.IsNullOrWhiteSpace(c.AttachmentFileName))
        {
            var fn = Path.GetFileName(c.AttachmentFileName);
            var size = TryGetFileSize("laboratory", fn, c.AttachmentFileSize);
            attachments.Add(new FormAttachmentDto
            {
                Key = "LaboratoryFinalCertAttachment",
                Title = "مرفق شهادة مطابقة المنتج (Conformity Certificate Attachment)",
                FileName = fn,
                FileType = c.AttachmentFileType ?? (fn.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Image"),
                FileSize = size ?? c.AttachmentFileSize,
                ViewUrl = $"/api/laboratory/attachments/{fn}",
                DownloadUrl = $"/api/laboratory/attachments/{fn}?download=true",
                UploadedBy = c.ResponsibleEmployee,
                UploadDate = c.ProductionCompletionDateTime
            });
        }

        return new ControlMonitoringReviewItemDto
        {
            Id = c.Id,
            FormType = "LaboratoryFinalCert",
            FormId = c.Id,
            PONumber = null,
            TransactionNumber = c.CertificateNumber,
            ProductionReferenceNumber = c.ProductionReferenceNumber,
            LaboratorySection = c.LaboratorySection,
            Material = c.FinalProductName,
            OutputQuantity = c.ActualFinalProductQuantity,
            WasteQuantityDifference = c.CalculatedDifference,
            MeasuredWasteQuantity = c.ActualMeasuredWaste,
            WasteClassification = c.WasteClassification,
            InspectionDecision = c.ConformityDecision,
            NonConformingAction = c.NonConformityAction,
            Status = string.IsNullOrWhiteSpace(c.ApprovalStatus) ? "PENDING" : c.ApprovalStatus,
            SubmittedBy = c.ResponsibleEmployee,
            SubmittedByUserId = c.ResponsibleUserId,
            SubmissionDate = c.ProductionCompletionDateTime,
            ReviewedBy = c.ReviewedBy,
            ReviewedAt = c.ReviewedAt,
            RejectionReason = c.RejectionReason,
            AttachmentCount = attachments.Count,
            Attachments = attachments,
            Notes = c.Observations,
            AnalysisResultsJson = c.SpecificationResultsJson
        };
    }

    private async Task NotifyStatusChangeAsync(
        string formType, int formId, string? po, string txNumber, string status, string reviewer, string? reason, string? submitterName = null, Guid? submitterUserId = null)
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
                submitterName,
                submitterUserId = submitterUserId?.ToString(),
                timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", payload);
            await _hubContext.Clients.All.SendAsync("OnFormApprovalStatusChanged", payload);

            if (status == "APPROVED")
            {
                await _hubContext.Clients.All.SendAsync("OnFormApproved", payload);
            }
            else if (status == "REJECTED")
            {
                await _hubContext.Clients.All.SendAsync("OnFormRejected", payload);
                if (submitterUserId.HasValue)
                {
                    await _hubContext.Clients.Group($"Employee_{submitterUserId.Value}").SendAsync("OnFormRejectedForEmployee", payload);
                }
            }
            else if (status == "PENDING")
            {
                await _hubContext.Clients.All.SendAsync("OnFormSubmittedForReview", payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast SignalR notification for {FormType} #{Id}", formType, formId);
        }
    }
}
