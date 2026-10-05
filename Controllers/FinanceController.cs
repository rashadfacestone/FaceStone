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
    public FinanceController(
        FaceStoneDbContext context, 
        ILogger<FinanceController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves Finance Statistics connecting Purchase Orders, Transportation, and Storage.
    /// Primary Filter: Date (defaults to server Today). Major Filter: PO.
    /// Distinguishes Daily Activity from Cumulative PO Status.
    /// Authorized only to: Accountants, IT, and CEO.
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<FinanceStatisticsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStatistics([FromQuery] DateTime? date, [FromQuery] string? po)
    {
        try
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
            var targetDate = (date ?? DateTime.Today).Date;
            var startOfDay = targetDate;
            var endOfDay = targetDate.AddDays(1);
            var selectedPoTrimmed = string.IsNullOrWhiteSpace(po) || po.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? null
                : po.Trim();

            _logger.LogInformation("Finance statistics requested by '{Role}' for Date: {Date:yyyy-MM-dd}, PO: '{PO}'",
                userRole, targetDate, selectedPoTrimmed ?? "ALL");

            // 1. Authoritative Purchase Orders
            var allPos = await _context.PurchaseOrders
                .AsNoTracking()
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync();

            // 2. Authoritative Payments (for paid & outstanding calculations)
            var allPayments = await _context.FinancePayments
                .AsNoTracking()
                .ToListAsync();

            // Build available POs list for dropdown
            var availablePos = allPos.Select(p => new FinancePoDropdownItemDto
            {
                PoNumber = p.PONumber,
                Supplier = p.Supplier,
                Destination = p.Destination,
                TonPrice = p.TonPrice > 0 ? p.TonPrice : p.PricePerTon,
                Quantity = p.Quantity,
                RemainingQuantity = p.RemainingQuantity,
                OrderDate = p.OrderDate
            }).ToList();

            // 3. Pre-fetch Transportation aggregations per PO (server-side group by)
            var transStatsByPo = await _context.Transportations
                .AsNoTracking()
                .GroupBy(t => t.PONumber)
                .Select(g => new
                {
                    PONumber = g.Key,
                    Count = g.Count(),
                    TotalNetWeight = g.Sum(t => t.NetWeight),
                    TotalWaste = g.Sum(t => t.WasteQuantity)
                })
                .ToDictionaryAsync(x => x.PONumber, StringComparer.OrdinalIgnoreCase);

            // 4. Pre-fetch Storage PO-related waste (server-side query)
            var storageWasteByPoList = await _context.StorageTransactions
                .AsNoTracking()
                .Where(s => s.IsExportRelatedToPO && s.PONumber != null && s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue)
                .Select(s => new
                {
                    PONumber = s.PONumber!,
                    Waste = Math.Abs(s.InternalNetWeight - s.ExternalNetWeight!.Value)
                })
                .ToListAsync();

            var storageWasteByPo = storageWasteByPoList
                .GroupBy(s => s.PONumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Waste), StringComparer.OrdinalIgnoreCase);

            // 5. Build cumulative summaries for each PO
            var allPosSummary = new List<FinancePoSummaryDto>();
            foreach (var p in allPos)
            {
                var tonPrice = p.TonPrice > 0 ? p.TonPrice : (p.PricePerTon > 0 ? p.PricePerTon : 0m);
                var expectedQty = p.Quantity;
                var expectedPrice = Math.Round(expectedQty * tonPrice, 2);

                transStatsByPo.TryGetValue(p.PONumber, out var tStats);
                var transCount = tStats?.Count ?? 0;
                var transNet = tStats?.TotalNetWeight ?? 0m;
                var transWaste = Math.Round(tStats?.TotalWaste ?? 0m, 3);
                var transWastePrice = Math.Round(transWaste * tonPrice, 2);

                storageWasteByPo.TryGetValue(p.PONumber, out var sWaste);
                var storageWaste = Math.Round(sWaste, 3);
                var storageWastePrice = Math.Round(storageWaste * tonPrice, 2);

                var totalWaste = Math.Round(transWaste + storageWaste, 3);
                var totalWastePrice = Math.Round(totalWaste * tonPrice, 2);

                var deliveredQty = Math.Round(Math.Max(0m, expectedQty - totalWaste), 3);
                var deliveredPrice = Math.Round(deliveredQty * tonPrice, 2);

                var remainingQty = Math.Round(p.RemainingQuantity, 3);
                var remainingPrice = Math.Round(remainingQty * tonPrice, 2);

                var perfPct = expectedQty > 0
                    ? Math.Round((deliveredQty / expectedQty) * 100m, 2)
                    : 100m;
                var perfPricePct = expectedPrice > 0
                    ? Math.Round((deliveredPrice / expectedPrice) * 100m, 2)
                    : 100m;

                var poPaymentsSum = allPayments
                    .Where(pay => pay.PONumber.Equals(p.PONumber, StringComparison.OrdinalIgnoreCase))
                    .Sum(pay => pay.Amount);
                var outstanding = Math.Round(Math.Max(0m, deliveredPrice - poPaymentsSum), 2);

                allPosSummary.Add(new FinancePoSummaryDto
                {
                    PoNumber = p.PONumber,
                    Supplier = p.Supplier,
                    Destination = p.Destination,
                    TonPrice = tonPrice,
                    OrderDate = p.OrderDate,
                    ExpectedQuantity = expectedQty,
                    ExpectedPrice = expectedPrice,
                    DeliveredQuantity = deliveredQty,
                    DeliveredPrice = deliveredPrice,
                    RemainingQuantity = remainingQty,
                    RemainingPrice = remainingPrice,
                    TransportationTransactionsCount = transCount,
                    TransportationWasteQuantity = transWaste,
                    TransportationWastePrice = transWastePrice,
                    StorageWasteQuantity = storageWaste,
                    StorageWastePrice = storageWastePrice,
                    TotalWasteQuantity = totalWaste,
                    TotalWastePrice = totalWastePrice,
                    ActualQuantity = deliveredQty,
                    ActualPrice = deliveredPrice,
                    PerformancePercentage = perfPct,
                    PerformancePricePercentage = perfPricePct,
                    TotalSuppliedNetWeight = transNet,
                    PaidAmount = Math.Round(poPaymentsSum, 2),
                    OutstandingAmount = outstanding
                });
            }

            // 6. Selected PO Summary vs Overall Summary
            FinancePoSummaryDto activePoSummary;
            FactoryPerformanceDto activePerformance;

            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                var targetPo = allPosSummary.FirstOrDefault(x => x.PoNumber.Equals(selectedPoTrimmed, StringComparison.OrdinalIgnoreCase));
                if (targetPo == null)
                {
                    targetPo = new FinancePoSummaryDto
                    {
                        Scope = "SinglePo",
                        PoNumber = selectedPoTrimmed,
                        Supplier = "غير محدد",
                        Destination = "-",
                        TonPrice = 0m,
                        PerformancePercentage = 0m
                    };
                }
                targetPo.Scope = "SinglePo";
                activePoSummary = targetPo;

                var status = targetPo.PerformancePercentage >= 95m ? "High" : (targetPo.PerformancePercentage >= 80m ? "Medium" : "Low");
                var statusArabic = status == "High" ? "أداء ممتاز" : (status == "Medium" ? "أداء مستقر" : "تنبيه هالك");

                activePerformance = new FactoryPerformanceDto
                {
                    ExpectedQuantity = targetPo.ExpectedQuantity,
                    ExpectedPrice = targetPo.ExpectedPrice,
                    ActualQuantity = targetPo.DeliveredQuantity,
                    ActualPrice = targetPo.DeliveredPrice,
                    WasteQuantity = targetPo.TotalWasteQuantity,
                    WastePrice = targetPo.TotalWastePrice,
                    RemainingQuantity = targetPo.RemainingQuantity,
                    RemainingPrice = targetPo.RemainingPrice,
                    PerformancePercentage = targetPo.PerformancePercentage,
                    PerformancePricePercentage = targetPo.PerformancePricePercentage,
                    Status = status,
                    StatusLabelArabic = statusArabic
                };
            }
            else
            {
                var sumExpectedQty = allPosSummary.Sum(p => p.ExpectedQuantity);
                var sumExpectedPrice = allPosSummary.Sum(p => p.ExpectedPrice);
                var sumDeliveredQty = allPosSummary.Sum(p => p.DeliveredQuantity);
                var sumDeliveredPrice = allPosSummary.Sum(p => p.DeliveredPrice);
                var sumTotalWaste = allPosSummary.Sum(p => p.TotalWasteQuantity);
                var sumTotalWastePrice = allPosSummary.Sum(p => p.TotalWastePrice);
                var sumRemainingQty = allPosSummary.Sum(p => p.RemainingQuantity);
                var sumRemainingPrice = allPosSummary.Sum(p => p.RemainingPrice);
                var sumTransCount = allPosSummary.Sum(p => p.TransportationTransactionsCount);
                var sumTransWaste = allPosSummary.Sum(p => p.TransportationWasteQuantity);
                var sumTransWastePrice = allPosSummary.Sum(p => p.TransportationWastePrice);
                var sumStorageWaste = allPosSummary.Sum(p => p.StorageWasteQuantity);
                var sumStorageWastePrice = allPosSummary.Sum(p => p.StorageWastePrice);
                var sumPaid = allPosSummary.Sum(p => p.PaidAmount);
                var sumOutstanding = allPosSummary.Sum(p => p.OutstandingAmount);

                var overallPerf = sumExpectedQty > 0
                    ? Math.Round((sumDeliveredQty / sumExpectedQty) * 100m, 2)
                    : 100m;
                var overallPricePerf = sumExpectedPrice > 0
                    ? Math.Round((sumDeliveredPrice / sumExpectedPrice) * 100m, 2)
                    : 100m;

                activePoSummary = new FinancePoSummaryDto
                {
                    Scope = "AllPos",
                    PoNumber = "جميع أوامر الشراء",
                    Supplier = "كافة الموردين",
                    Destination = "جميع المحطات والمصانع",
                    TonPrice = allPosSummary.Count > 0 ? Math.Round(allPosSummary.Average(p => p.TonPrice), 2) : 0m,
                    OrderDate = allPosSummary.Count > 0 ? allPosSummary.Min(p => p.OrderDate) : targetDate,
                    ExpectedQuantity = sumExpectedQty,
                    ExpectedPrice = sumExpectedPrice,
                    DeliveredQuantity = sumDeliveredQty,
                    DeliveredPrice = sumDeliveredPrice,
                    RemainingQuantity = sumRemainingQty,
                    RemainingPrice = sumRemainingPrice,
                    TransportationTransactionsCount = sumTransCount,
                    TransportationWasteQuantity = sumTransWaste,
                    TransportationWastePrice = sumTransWastePrice,
                    StorageWasteQuantity = sumStorageWaste,
                    StorageWastePrice = sumStorageWastePrice,
                    TotalWasteQuantity = sumTotalWaste,
                    TotalWastePrice = sumTotalWastePrice,
                    ActualQuantity = sumDeliveredQty,
                    ActualPrice = sumDeliveredPrice,
                    PerformancePercentage = overallPerf,
                    PerformancePricePercentage = overallPricePerf,
                    PaidAmount = sumPaid,
                    OutstandingAmount = sumOutstanding
                };

                var status = overallPerf >= 95m ? "High" : (overallPerf >= 80m ? "Medium" : "Low");
                var statusArabic = status == "High" ? "أداء ممتاز" : (status == "Medium" ? "أداء مستقر" : "تنبيه هالك");

                activePerformance = new FactoryPerformanceDto
                {
                    ExpectedQuantity = sumExpectedQty,
                    ExpectedPrice = sumExpectedPrice,
                    ActualQuantity = sumDeliveredQty,
                    ActualPrice = sumDeliveredPrice,
                    WasteQuantity = sumTotalWaste,
                    WastePrice = sumTotalWastePrice,
                    RemainingQuantity = sumRemainingQty,
                    RemainingPrice = sumRemainingPrice,
                    PerformancePercentage = overallPerf,
                    PerformancePricePercentage = overallPricePerf,
                    Status = status,
                    StatusLabelArabic = statusArabic
                };
            }

            // 7. DAILY ACTIVITY CALCULATION (FOR SELECTED DATE)
            var dailyTransQuery = _context.Transportations
                .AsNoTracking()
                .Where(t => t.TransactionDate >= startOfDay && t.TransactionDate < endOfDay);

            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                dailyTransQuery = dailyTransQuery.Where(t => t.PONumber == selectedPoTrimmed);
            }

            var dailyTransList = await dailyTransQuery
                .Select(t => new { t.PONumber, t.NetWeight, t.WasteQuantity })
                .ToListAsync();

            var dailyTransCount = dailyTransList.Count;
            var dailyTransNetWeight = Math.Round(dailyTransList.Sum(t => t.NetWeight), 3);
            var dailyTransWaste = Math.Round(dailyTransList.Sum(t => t.WasteQuantity), 3);

            decimal dailyTransWastePrice = 0m;
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                dailyTransWastePrice = Math.Round(dailyTransWaste * activePoSummary.TonPrice, 2);
            }
            else
            {
                var poPriceMap = allPosSummary.ToDictionary(p => p.PoNumber, p => p.TonPrice, StringComparer.OrdinalIgnoreCase);
                dailyTransWastePrice = Math.Round(dailyTransList.Sum(t =>
                {
                    poPriceMap.TryGetValue(t.PONumber, out var price);
                    return t.WasteQuantity * price;
                }), 2);
            }

            // 7.2 Storage Imports (Independent, non-PO)
            var dailyStorageImports = await _context.StorageTransactions
                .AsNoTracking()
                .Where(s => s.TransactionDate >= startOfDay && s.TransactionDate < endOfDay)
                .Where(s => s.Action == "Import" || s.ServiceType == "إيداع")
                .SumAsync(s => (decimal?)s.IncomingQuantity) ?? 0m;
            dailyStorageImports = Math.Round(dailyStorageImports, 3);

            // 7.3 Storage Exports
            var dailyStorageExportsQuery = _context.StorageTransactions
                .AsNoTracking()
                .Where(s => s.TransactionDate >= startOfDay && s.TransactionDate < endOfDay)
                .Where(s => s.Action == "Export" || s.ServiceType == "صرف");

            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                dailyStorageExportsQuery = dailyStorageExportsQuery.Where(s => s.PONumber == selectedPoTrimmed && s.IsExportRelatedToPO);
            }

            var dailyStorageExports = await dailyStorageExportsQuery.SumAsync(s => (decimal?)s.OutgoingQuantity) ?? 0m;
            dailyStorageExports = Math.Round(dailyStorageExports, 3);

            // 7.4 Storage Waste
            var dailyStorageWasteQuery = _context.StorageTransactions
                .AsNoTracking()
                .Where(s => s.TransactionDate >= startOfDay && s.TransactionDate < endOfDay)
                .Where(s => s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue);

            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                dailyStorageWasteQuery = dailyStorageWasteQuery.Where(s => s.PONumber == selectedPoTrimmed && s.IsExportRelatedToPO);
            }

            var dailyStorageWasteRows = await dailyStorageWasteQuery
                .Select(s => new
                {
                    s.PONumber,
                    Waste = Math.Abs(s.InternalNetWeight - s.ExternalNetWeight!.Value)
                })
                .ToListAsync();

            var dailyStorageWaste = Math.Round(dailyStorageWasteRows.Sum(s => s.Waste), 3);
            decimal dailyStorageWastePrice = 0m;
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                dailyStorageWastePrice = Math.Round(dailyStorageWaste * activePoSummary.TonPrice, 2);
            }
            else
            {
                var poPriceMap = allPosSummary.ToDictionary(p => p.PoNumber, p => p.TonPrice, StringComparer.OrdinalIgnoreCase);
                dailyStorageWastePrice = Math.Round(dailyStorageWasteRows.Sum(s =>
                {
                    var price = 0m;
                    if (!string.IsNullOrWhiteSpace(s.PONumber))
                    {
                        poPriceMap.TryGetValue(s.PONumber, out price);
                    }
                    return s.Waste * price;
                }), 2);
            }

            var totalDailyWaste = Math.Round(dailyTransWaste + dailyStorageWaste, 3);
            var totalDailyWastePrice = Math.Round(dailyTransWastePrice + dailyStorageWastePrice, 2);

            var dailyActivity = new FinanceDailyActivityDto
            {
                SelectedDate = targetDate,
                TransportationTransactionsCount = dailyTransCount,
                TransportationNetWeight = dailyTransNetWeight,
                TransportationWasteQuantity = dailyTransWaste,
                TransportationWastePrice = dailyTransWastePrice,
                StorageImportedQuantity = dailyStorageImports,
                StorageImportedPrice = null,
                StorageImportedPriceNote = "لا يوجد مصدر تسعير للواردات المخزنية في قاعدة البيانات",
                StorageExportedQuantity = dailyStorageExports,
                StorageWasteQuantity = dailyStorageWaste,
                StorageWastePrice = dailyStorageWastePrice,
                TotalDailyWasteQuantity = totalDailyWaste,
                TotalDailyWastePrice = totalDailyWastePrice
            };

            var response = new FinanceStatisticsResponseDto
            {
                SelectedDate = targetDate,
                SelectedPo = selectedPoTrimmed,
                DailyActivity = dailyActivity,
                PoSummary = activePoSummary,
                Performance = activePerformance,
                AvailablePos = availablePos,
                AllPosSummary = allPosSummary
            };

            return Ok(ApiResponse<FinanceStatisticsResponseDto>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing Finance statistics.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("حدث خطأ أثناء احتساب الإحصائيات المالية."));
        }
    }

    /// <summary>
    /// Retrieves list of all Purchase Orders for dropdowns and filter selection.
    /// </summary>
    [HttpGet("pos")]
    [ProducesResponseType(typeof(ApiResponse<List<FinancePoDropdownItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPurchaseOrders()
    {
        var pos = await _context.PurchaseOrders
            .AsNoTracking()
            .OrderByDescending(p => p.OrderDate)
            .Select(p => new FinancePoDropdownItemDto
            {
                PoNumber = p.PONumber,
                Supplier = p.Supplier,
                Destination = p.Destination,
                TonPrice = p.TonPrice > 0 ? p.TonPrice : p.PricePerTon,
                Quantity = p.Quantity,
                RemainingQuantity = p.RemainingQuantity,
                OrderDate = p.OrderDate
            })
            .ToListAsync();

        return Ok(ApiResponse<List<FinancePoDropdownItemDto>>.Ok(pos));
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
                    TonPrice = po.TonPrice > 0 ? po.TonPrice : po.PricePerTon,
                    PoValue = po.TotalValue > 0 ? po.TotalValue : Math.Round(po.Quantity * (po.TonPrice > 0 ? po.TonPrice : po.PricePerTon), 2),
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
                    var supplier = !string.IsNullOrWhiteSpace(latestTrans.Source) ? latestTrans.Source.Trim() : "Ø§Ù„Ù…ÙˆØ±Ø¯ Ø§Ù„Ø±Ø¦ÙŠØ³ÙŠ Ù„Ù„Ø®Ø±Ø³Ø§Ù†Ø©";
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
                .GroupBy(s => s.PONumber!.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var group in storageByPo)
            {
                var poNum = group.Key;
                var incomingSum = group.Where(s => s.ServiceType.Equals("Ø¥Ø¶Ø§ÙØ©", StringComparison.OrdinalIgnoreCase)).Sum(s => s.IncomingQuantity);

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

                // 5. Ù‚ÙŠÙ…Ø© Ø£Ù…Ø± Ø§Ù„Ø´Ø±Ø§Ø¡ = ÙƒÙ…ÙŠØ© Ø£Ù…Ø± Ø§Ù„Ø´Ø±Ø§Ø¡ Ã— Ø³Ø¹Ø± Ø§Ù„Ø·Ù†
                r.PoValue = Math.Round(r.PoQuantity * r.TonPrice, 2);

                // 8. Ù‚ÙŠÙ…Ø© Ø§Ù„ÙƒÙ…ÙŠØ© Ø§Ù„ØªÙŠ ØªÙ… ØªÙˆØ±ÙŠØ¯Ù‡Ø§ = Ø§Ù„ÙƒÙ…ÙŠØ© Ø§Ù„ØªÙŠ ØªÙ… ØªÙˆØ±ÙŠØ¯Ù‡Ø§ Ã— Ø³Ø¹Ø± Ø§Ù„Ø·Ù†
                r.SuppliedValue = Math.Round(r.SuppliedQuantity * r.TonPrice, 2);

                // 9. Ø§Ù„ÙƒÙ…ÙŠØ© Ø§Ù„Ù…ØªØ¨Ù‚ÙŠØ© = ÙƒÙ…ÙŠØ© Ø£Ù…Ø± Ø§Ù„Ø´Ø±Ø§Ø¡ - Ø§Ù„ÙƒÙ…ÙŠØ© Ø§Ù„ØªÙŠ ØªÙ… ØªÙˆØ±ÙŠØ¯Ù‡Ø§
                r.RemainingQuantity = Math.Round(Math.Max(0m, r.PoQuantity - r.SuppliedQuantity), 3);

                // 10. Ø§Ù„Ù‚ÙŠÙ…Ø© Ø§Ù„Ù…ØªØ¨Ù‚ÙŠØ© = Ø§Ù„ÙƒÙ…ÙŠØ© Ø§Ù„Ù…ØªØ¨Ù‚ÙŠØ© Ã— Ø³Ø¹Ø± Ø§Ù„Ø·Ù†
                r.RemainingValue = Math.Round(r.RemainingQuantity * r.TonPrice, 2);

                // 12 & 13. Ø§Ù„ÙƒÙ…ÙŠØ© Ø§Ù„Ù…Ø¹ØªÙ…Ø¯Ø© ÙˆØ§Ù„ØºÙŠØ± Ù…Ø¹ØªÙ…Ø¯Ø©
                r.ApprovedQuantity = Math.Round(r.ApprovedQuantity, 3);
                r.UnapprovedQuantity = Math.Round(Math.Max(0m, r.SuppliedQuantity - r.ApprovedQuantity), 3);

                // 14. Ø§Ù„Ù…Ø¨Ù„Øº Ø§Ù„Ù…Ø¯ÙÙˆØ¹
                r.PaidAmount = Math.Round(r.PaidAmount, 2);

                // 15. Ø§Ù„Ù…Ø¨Ù„Øº Ø§Ù„Ù…Ø³ØªØ­Ù‚ = Ù‚ÙŠÙ…Ø© Ø§Ù„ÙƒÙ…ÙŠØ© Ø§Ù„ØªÙŠ ØªÙ… ØªÙˆØ±ÙŠØ¯Ù‡Ø§ - Ø§Ù„Ù…Ø¨Ù„Øº Ø§Ù„Ù…Ø¯ÙÙˆØ¹
                r.OutstandingAmount = Math.Round(Math.Max(0m, r.SuppliedValue - r.PaidAmount), 2);

                // 16. Ø­Ø§Ù„Ø© Ø§Ù„Ø³Ø¯Ø§Ø¯
                if (r.PaidAmount <= 0)
                {
                    r.PaymentStatus = "ØºÙŠØ± Ù…Ø¯ÙÙˆØ¹";
                }
                else if (r.PaidAmount > 0 && r.PaidAmount < r.SuppliedValue)
                {
                    r.PaymentStatus = "Ù…Ø¯ÙÙˆØ¹ Ø¬Ø²Ø¦ÙŠØ§Ù‹";
                }
                else
                {
                    r.PaymentStatus = "Ù…Ø¯ÙÙˆØ¹ Ø¨Ø§Ù„ÙƒØ§Ù…Ù„";
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
                PaymentStatuses = new List<string> { "ØºÙŠØ± Ù…Ø¯ÙÙˆØ¹", "Ù…Ø¯ÙÙˆØ¹ Ø¬Ø²Ø¦ÙŠØ§Ù‹", "Ù…Ø¯ÙÙˆØ¹ Ø¨Ø§Ù„ÙƒØ§Ù…Ù„" },
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
                ApiResponse.Fail("Ø­Ø¯Ø« Ø®Ø·Ø£ Ø£Ø«Ù†Ø§Ø¡ ØªØ­Ù…ÙŠÙ„ Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª Ø§Ù„Ù…Ø§Ù„ÙŠØ©. ÙŠØ±Ø¬Ù‰ Ø§Ù„Ù…Ø­Ø§ÙˆÙ„Ø© Ù„Ø§Ø­Ù‚Ø§Ù‹."));
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
            return BadRequest(ApiResponse.Fail("Ø±Ù‚Ù… Ø£Ù…Ø± Ø§Ù„Ø´Ø±Ø§Ø¡ Ù…Ø·Ù„ÙˆØ¨."));
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
            return BadRequest(ApiResponse.Fail("Ø±Ù‚Ù… Ø£Ù…Ø± Ø§Ù„Ø´Ø±Ø§Ø¡ Ù…Ø·Ù„ÙˆØ¨ Ù„ØªØ³Ø¬ÙŠÙ„ Ø§Ù„Ø¯ÙØ¹Ø©."));
        }

        if (dto.Amount <= 0)
        {
            return BadRequest(ApiResponse.Fail("Ù…Ø¨Ù„Øº Ø§Ù„Ø¯ÙØ¹Ø© ÙŠØ¬Ø¨ Ø£Ù† ÙŠÙƒÙˆÙ† Ø£ÙƒØ¨Ø± Ù…Ù† Ø§Ù„ØµÙØ±."));
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

        return Ok(ApiResponse<FinancePaymentDto>.Ok(resultDto, "ØªÙ… ØªØ³Ø¬ÙŠÙ„ Ø§Ù„Ø¯ÙØ¹Ø© Ø§Ù„Ù…Ø§Ù„ÙŠØ© Ø¨Ù†Ø¬Ø§Ø­."));
    }
}
