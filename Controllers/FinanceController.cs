using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
[Authorize(Policy = "FinanceAccess")]
[Produces("application/json")]
public class FinanceController : ControllerBase
{
    private readonly FaceStoneDbContext _context;
    private readonly ILogger<FinanceController> _logger;

    public FinanceController(FaceStoneDbContext context, ILogger<FinanceController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves aggregated Finance Management data, summary KPIs, and filter options with server-side pagination.
    /// Strictly authorized only to: Accountants, IT, and CEO.
    /// </summary>
    [HttpGet("management-data")]
    [ProducesResponseType(typeof(ApiResponse<FinancePageResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFinanceManagementData([FromQuery] FinanceQueryParameters query)
    {
        try
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
            _logger.LogInformation("Finance management data requested by role '{Role}'. Search: '{Search}', Page: {Page}, PageSize: {PageSize}",
                userRole, query.Search, query.Page, query.PageSize);

            // 1. Fetch Purchase Orders with their Payments
            var poEntities = await _context.PurchaseOrders
                .AsNoTracking()
                .Include(p => p.Payments)
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync();

            // 2. Fetch Transportation records
            var transRecords = await _context.Transportations
                .AsNoTracking()
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            // 3. Fetch Management records
            var mgmtRecords = await _context.Management
                .AsNoTracking()
                .ToListAsync();

            // 4. Fetch Storage Transactions
            var storageRecords = await _context.StorageTransactions
                .AsNoTracking()
                .ToListAsync();

            // 5. Fetch all Payments (for direct PO mapping)
            var allPayments = await _context.FinancePayments
                .AsNoTracking()
                .ToListAsync();

            // Collect all unique PO Numbers across POs, Transportation, Management, Storage
            var poMap = new Dictionary<string, FinanceRecordDto>(StringComparer.OrdinalIgnoreCase);

            // Populate known Purchase Orders from database
            foreach (var po in poEntities)
            {
                var poNum = po.PONumber.Trim();
                var poPaymentsSum = allPayments
                    .Where(p => p.PONumber.Trim().Equals(poNum, StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Amount);

                poMap[poNum] = new FinanceRecordDto
                {
                    PoNumber = poNum,
                    Supplier = po.Supplier.Trim(),
                    OrderDate = po.OrderDate,
                    PoQuantity = po.Quantity,
                    TonPrice = po.PricePerTon,
                    PoValue = po.TotalValue > 0 ? po.TotalValue : Math.Round(po.Quantity * po.PricePerTon, 2),
                    InvoiceNumber = string.IsNullOrWhiteSpace(po.InvoiceNumber) ? null : po.InvoiceNumber.Trim(),
                    PaidAmount = poPaymentsSum,
                    SuppliedQuantity = 0m,
                    MovementsCount = 0,
                    ApprovedQuantity = 0m,
                    UnapprovedQuantity = 0m
                };
            }

            // Discover and aggregate from Transportation records
            var transByPo = transRecords
                .Where(t => !string.IsNullOrWhiteSpace(t.PONumber))
                .GroupBy(t => t.PONumber.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var group in transByPo)
            {
                var poNum = group.Key;
                var sortedTrans = group.OrderBy(t => t.TransactionDate).ToList();
                var firstTrans = sortedTrans.First();
                var latestTrans = sortedTrans.Last();

                var transSupplied = group.Sum(t => t.NetWeight);
                var transMovements = group.Count();
                var transApproved = group
                    .Where(t => t.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase))
                    .Sum(t => t.NetWeight);
                var transUnapproved = group
                    .Where(t => !t.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase))
                    .Sum(t => t.NetWeight);

                if (poMap.TryGetValue(poNum, out var record))
                {
                    record.SuppliedQuantity = transSupplied;
                    record.MovementsCount = transMovements;
                    record.ApprovedQuantity = transApproved;
                    record.UnapprovedQuantity = transUnapproved;
                    if (string.IsNullOrWhiteSpace(record.Supplier) && !string.IsNullOrWhiteSpace(latestTrans.Source))
                    {
                        record.Supplier = latestTrans.Source.Trim();
                    }
                }
                else
                {
                    // PO exists in Transportation but wasn't in PO table yet
                    var supplier = !string.IsNullOrWhiteSpace(latestTrans.Source) ? latestTrans.Source.Trim() : "المورد الرئيسي للخرسانة";
                    var orderDate = firstTrans.TransactionDate;
                    var estimatedTonPrice = 1450.00m;
                    var estimatedPoQty = Math.Max(transSupplied * 1.2m, 100.0m);
                    var poVal = Math.Round(estimatedPoQty * estimatedTonPrice, 2);

                    var poPaymentsSum = allPayments
                        .Where(p => p.PONumber.Trim().Equals(poNum, StringComparison.OrdinalIgnoreCase))
                        .Sum(p => p.Amount);

                    poMap[poNum] = new FinanceRecordDto
                    {
                        PoNumber = poNum,
                        Supplier = supplier,
                        OrderDate = orderDate,
                        PoQuantity = Math.Round(estimatedPoQty, 3),
                        TonPrice = estimatedTonPrice,
                        PoValue = poVal,
                        InvoiceNumber = null,
                        SuppliedQuantity = transSupplied,
                        MovementsCount = transMovements,
                        ApprovedQuantity = transApproved,
                        UnapprovedQuantity = transUnapproved,
                        PaidAmount = poPaymentsSum
                    };
                }
            }

            // Incorporate Management records if applicable
            var mgmtByPo = mgmtRecords
                .Where(m => !string.IsNullOrWhiteSpace(m.PO))
                .GroupBy(m => m.PO!.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var group in mgmtByPo)
            {
                var poNum = group.Key;
                var mgmtTotal = group.Sum(m => m.TotalReceivedAmount);
                var mgmtAccepted = group.Sum(m => m.AcceptedQuantity);
                var mgmtUnaccepted = group.Sum(m => m.UnacceptedPendingQuantity);
                var mgmtMovements = group.Sum(m => m.NumberOfTransactions);

                if (poMap.TryGetValue(poNum, out var record))
                {
                    if (record.SuppliedQuantity == 0 && mgmtTotal > 0)
                    {
                        record.SuppliedQuantity = mgmtTotal;
                    }
                    if (record.MovementsCount == 0 && mgmtMovements > 0)
                    {
                        record.MovementsCount = mgmtMovements;
                    }
                    if (record.ApprovedQuantity == 0 && mgmtAccepted > 0)
                    {
                        record.ApprovedQuantity = mgmtAccepted;
                    }
                    if (record.UnapprovedQuantity == 0 && mgmtUnaccepted > 0)
                    {
                        record.UnapprovedQuantity = mgmtUnaccepted;
                    }
                }
            }

            // Incorporate Storage transactions if applicable
            var storageByPo = storageRecords
                .Where(s => !string.IsNullOrWhiteSpace(s.PONumber))
                .GroupBy(s => s.PONumber.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var group in storageByPo)
            {
                var poNum = group.Key;
                var incomingSum = group.Where(s => s.ServiceType.Equals("إضافة", StringComparison.OrdinalIgnoreCase)).Sum(s => s.IncomingQuantity);

                if (poMap.TryGetValue(poNum, out var record))
                {
                    if (record.SuppliedQuantity == 0 && incomingSum > 0)
                    {
                        record.SuppliedQuantity = incomingSum;
                        if (record.ApprovedQuantity == 0) record.ApprovedQuantity = incomingSum;
                    }
                }
            }

            // Compute all calculated financial formulas for each record (Read-Only fields)
            var allRecords = poMap.Values.ToList();
            foreach (var r in allRecords)
            {
                // Ensure proper precision
                r.PoQuantity = Math.Round(r.PoQuantity, 3);
                r.TonPrice = Math.Round(r.TonPrice, 2);
                r.SuppliedQuantity = Math.Round(r.SuppliedQuantity, 3);

                // 5. قيمة أمر الشراء = كمية أمر الشراء × سعر الطن
                r.PoValue = Math.Round(r.PoQuantity * r.TonPrice, 2);

                // 8. قيمة الكمية التي تم توريدها = الكمية التي تم توريدها × سعر الطن
                r.SuppliedValue = Math.Round(r.SuppliedQuantity * r.TonPrice, 2);

                // 9. الكمية المتبقية = كمية أمر الشراء - الكمية التي تم توريدها
                r.RemainingQuantity = Math.Round(Math.Max(0m, r.PoQuantity - r.SuppliedQuantity), 3);

                // 10. القيمة المتبقية = الكمية المتبقية × سعر الطن
                r.RemainingValue = Math.Round(r.RemainingQuantity * r.TonPrice, 2);

                // 12 & 13. الكمية المعتمدة والغير معتمدة
                r.ApprovedQuantity = Math.Round(r.ApprovedQuantity, 3);
                r.UnapprovedQuantity = Math.Round(Math.Max(0m, r.SuppliedQuantity - r.ApprovedQuantity), 3);

                // 14. المبلغ المدفوع
                r.PaidAmount = Math.Round(r.PaidAmount, 2);

                // 15. المبلغ المستحق = قيمة الكمية التي تم توريدها - المبلغ المدفوع
                r.OutstandingAmount = Math.Round(Math.Max(0m, r.SuppliedValue - r.PaidAmount), 2);

                // 16. حالة السداد
                if (r.PaidAmount <= 0)
                {
                    r.PaymentStatus = "غير مدفوع";
                }
                else if (r.PaidAmount > 0 && r.PaidAmount < r.SuppliedValue)
                {
                    r.PaymentStatus = "مدفوع جزئياً";
                }
                else
                {
                    r.PaymentStatus = "مدفوع بالكامل";
                }
            }

            // Order records chronologically (newest POs first)
            allRecords = allRecords.OrderByDescending(r => r.OrderDate).ToList();

            // Calculate overall KPI Summary before filters
            var kpiSummary = new FinanceKpiSummaryDto
            {
                TotalPoValue = Math.Round(allRecords.Sum(r => r.PoValue), 2),
                TotalSuppliedQuantity = Math.Round(allRecords.Sum(r => r.SuppliedQuantity), 3),
                TotalRemainingQuantity = Math.Round(allRecords.Sum(r => r.RemainingQuantity), 3),
                TotalPaidAmount = Math.Round(allRecords.Sum(r => r.PaidAmount), 2),
                TotalOutstandingAmount = Math.Round(allRecords.Sum(r => r.OutstandingAmount), 2),
                TotalMovementsCount = allRecords.Sum(r => r.MovementsCount),
                TotalPosCount = allRecords.Count
            };

            // Build dynamic Filter Options
            var filterOptions = new FinanceFilterOptionsDto
            {
                Suppliers = allRecords.Select(r => r.Supplier).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList(),
                PaymentStatuses = new List<string> { "غير مدفوع", "مدفوع جزئياً", "مدفوع بالكامل" },
                InvoiceNumbers = allRecords.Select(r => r.InvoiceNumber).Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i!).Distinct().OrderBy(i => i).ToList()
            };

            // Apply Search & Filters
            var filtered = allRecords.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                filtered = filtered.Where(r =>
                    r.PoNumber.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    r.Supplier.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (r.InvoiceNumber != null && r.InvoiceNumber.Contains(s, StringComparison.OrdinalIgnoreCase))
                );
            }

            if (!string.IsNullOrWhiteSpace(query.Po) && !query.Po.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r => r.PoNumber.Contains(query.Po.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.Supplier) && !query.Supplier.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r => r.Supplier.Equals(query.Supplier.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.PaymentStatus) && !query.PaymentStatus.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r => r.PaymentStatus.Equals(query.PaymentStatus.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.InvoiceNumber) && !query.InvoiceNumber.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r => r.InvoiceNumber != null && r.InvoiceNumber.Equals(query.InvoiceNumber.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (query.DateFrom.HasValue)
            {
                var dFrom = query.DateFrom.Value.Date;
                filtered = filtered.Where(r => r.OrderDate.Date >= dFrom);
            }

            if (query.DateTo.HasValue)
            {
                var dTo = query.DateTo.Value.Date;
                filtered = filtered.Where(r => r.OrderDate.Date <= dTo);
            }

            var filteredList = filtered.ToList();
            var totalCount = filteredList.Count;

            // Apply Server-Side Pagination
            var pageSize = Math.Clamp(query.PageSize, 5, 100);
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            var page = Math.Clamp(query.Page, 1, Math.Max(1, totalPages));

            var pagedRecords = filteredList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new FinancePageResponseDto
            {
                KpiSummary = kpiSummary,
                Records = pagedRecords,
                FilterOptions = filterOptions,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return Ok(ApiResponse<FinancePageResponseDto>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Finance management data.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("حدث خطأ أثناء تحميل البيانات المالية. يرجى المحاولة لاحقاً."));
        }
    }

    /// <summary>
    /// Retrieves payment history for a given Purchase Order.
    /// Supports the future CEO Dashboard architecture.
    /// </summary>
    [HttpGet("payments/{poNumber}")]
    [ProducesResponseType(typeof(ApiResponse<List<FinancePaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentsByPo(string poNumber)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
        {
            return BadRequest(ApiResponse.Fail("رقم أمر الشراء مطلوب."));
        }

        var targetPo = poNumber.Trim();
        var payments = await _context.FinancePayments
            .AsNoTracking()
            .Where(p => p.PONumber == targetPo)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new FinancePaymentDto
            {
                Id = p.Id,
                PoNumber = p.PONumber,
                Amount = p.Amount,
                PaymentDate = p.PaymentDate,
                PaymentMethod = p.PaymentMethod,
                ReferenceNumber = p.ReferenceNumber,
                PaidBy = p.PaidBy,
                Notes = p.Notes,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<List<FinancePaymentDto>>.Ok(payments));
    }

    /// <summary>
    /// Backend Architecture Foundation for Future CEO Payment Entry.
    /// Records an official payment against a Purchase Order.
    /// Strictly authorized to CEO and IT roles.
    /// </summary>
    [HttpPost("payments")]
    [Authorize(Roles = "CEO,IT")]
    [ProducesResponseType(typeof(ApiResponse<FinancePaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RecordCeoPayment([FromBody] CreateFinancePaymentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PoNumber))
        {
            return BadRequest(ApiResponse.Fail("رقم أمر الشراء مطلوب لتسجيل الدفعة."));
        }

        if (dto.Amount <= 0)
        {
            return BadRequest(ApiResponse.Fail("مبلغ الدفعة يجب أن يكون أكبر من الصفر."));
        }

        var poNum = dto.PoNumber.Trim();
        var po = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.PONumber == poNum);

        var callerName = User.FindFirst(ClaimTypes.Name)?.Value 
            ?? User.FindFirst("fullName")?.Value 
            ?? "CEO Office";

        var payment = new FinancePayment
        {
            PONumber = poNum,
            PurchaseOrderId = po?.Id,
            Amount = Math.Round(dto.Amount, 2),
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Bank Transfer" : dto.PaymentMethod.Trim(),
            ReferenceNumber = dto.ReferenceNumber?.Trim(),
            PaidBy = callerName,
            Notes = dto.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _context.FinancePayments.AddAsync(payment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("CEO Payment of {Amount} recorded for PO '{PONumber}' by '{Caller}'.",
            payment.Amount, poNum, callerName);

        var resultDto = new FinancePaymentDto
        {
            Id = payment.Id,
            PoNumber = payment.PONumber,
            Amount = payment.Amount,
            PaymentDate = payment.PaymentDate,
            PaymentMethod = payment.PaymentMethod,
            ReferenceNumber = payment.ReferenceNumber,
            PaidBy = payment.PaidBy,
            Notes = payment.Notes,
            CreatedAt = payment.CreatedAt
        };

        return Ok(ApiResponse<FinancePaymentDto>.Ok(resultDto, "تم تسجيل الدفعة المالية بنجاح."));
    }
}
