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
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] DateTime? date,
        [FromQuery] string? po)
    {
        try
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
            var isFinanceDirector = AppRoles.IsFinanceDirector(userRole);

            // Date Range logic: defaults to today if not provided
            var dFrom = (fromDate ?? date ?? DateTime.Today).Date;
            var dTo = (toDate ?? date ?? dFrom).Date;
            if (dFrom > dTo)
            {
                var temp = dFrom;
                dFrom = dTo;
                dTo = temp;
            }
            var startRange = dFrom;
            var endRange = dTo.AddDays(1); // inclusive of entire toDate day

            var selectedPoTrimmed = string.IsNullOrWhiteSpace(po) || po.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? null
                : po.Trim();

            _logger.LogInformation("Finance statistics requested by '{Role}' for Range: {From:yyyy-MM-dd} to {To:yyyy-MM-dd}, PO: '{PO}'",
                userRole, dFrom, dTo, selectedPoTrimmed ?? "ALL");

            // 1. Authoritative Purchase Orders from database
            var allPos = await _context.PurchaseOrders
                .AsNoTracking()
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync();

            // 2. Authoritative Payments (for paid & outstanding calculations)
            var allPayments = await _context.FinancePayments
                .AsNoTracking()
                .ToListAsync();

            // 3. Pre-fetch Transportation aggregations per PO (server-side group by across all time)
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

            // 4. Pre-fetch Storage PO-related waste (server-side query across all time)
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

            // Build available POs list for dropdown
            var availablePos = allPos.Select(p => new FinancePoDropdownItemDto
            {
                PoNumber = p.PONumber,
                Supplier = p.Supplier,
                Destination = p.Destination,
                TonPrice = p.TonPrice > 0 ? p.TonPrice : p.PricePerTon,
                Quantity = p.Quantity,
                RemainingQuantity = p.RemainingQuantity,
                OrderDate = p.OrderDate,
                Status = p.RemainingQuantity <= 0 ? "مكتمل" : (transStatsByPo.ContainsKey(p.PONumber) ? "قيد التوريد" : "معلق"),
                PaymentStatus = !string.IsNullOrWhiteSpace(p.PaymentStatus) ? p.PaymentStatus : "غير مدفوع"
            }).ToList();

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

                var poStatus = remainingQty <= 0 ? "مكتمل" : (transCount > 0 ? "قيد التوريد" : "معلق");
                var paymentStatus = !string.IsNullOrWhiteSpace(p.PaymentStatus) ? p.PaymentStatus : "غير مدفوع";

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
                    OutstandingAmount = outstanding,
                    Status = poStatus,
                    PaymentStatus = paymentStatus,
                    PaymentStatusUpdatedAt = p.PaymentStatusUpdatedAt,
                    PaymentStatusUpdatedBy = p.PaymentStatusUpdatedBy
                });
            }

            // 6. Selected PO Summary vs Overall Cumulative Summary
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
                        PerformancePercentage = 0m,
                        Status = "معلق",
                        PaymentStatus = "غير مدفوع"
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
                    OrderDate = allPosSummary.Count > 0 ? allPosSummary.Min(p => p.OrderDate) : dFrom,
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
                    OutstandingAmount = sumOutstanding,
                    Status = "كافة الأوامر",
                    PaymentStatus = "متنوع"
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

            // 7. RANGE / DAILY ACTIVITY CALCULATION (FOR SELECTED DATE RANGE)
            var rangeTransQuery = _context.Transportations
                .AsNoTracking()
                .Where(t => t.TransactionDate >= startRange && t.TransactionDate < endRange);

            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeTransQuery = rangeTransQuery.Where(t => t.PONumber == selectedPoTrimmed);
            }

            var rangeTransList = await rangeTransQuery
                .Select(t => new { t.PONumber, t.NetWeight, t.WasteQuantity })
                .ToListAsync();

            var rangeTransCount = rangeTransList.Count;
            var rangeTransNetWeight = Math.Round(rangeTransList.Sum(t => t.NetWeight), 3);
            var rangeTransWaste = Math.Round(rangeTransList.Sum(t => t.WasteQuantity), 3);

            decimal rangeTransWastePrice = 0m;
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeTransWastePrice = Math.Round(rangeTransWaste * activePoSummary.TonPrice, 2);
            }
            else
            {
                var poPriceMap = allPosSummary.ToDictionary(p => p.PoNumber, p => p.TonPrice, StringComparer.OrdinalIgnoreCase);
                rangeTransWastePrice = Math.Round(rangeTransList.Sum(t =>
                {
                    poPriceMap.TryGetValue(t.PONumber, out var price);
                    return t.WasteQuantity * price;
                }), 2);
            }

            // 7.2 Storage Imports (Independent, non-PO unless recorded otherwise)
            var rangeStorageImports = await _context.StorageTransactions
                .AsNoTracking()
                .Where(s => s.TransactionDate >= startRange && s.TransactionDate < endRange)
                .Where(s => s.Action == "Import" || s.ServiceType == "إيداع")
                .SumAsync(s => (decimal?)s.IncomingQuantity) ?? 0m;
            rangeStorageImports = Math.Round(rangeStorageImports, 3);

            // 7.3 Storage Exports
            var rangeStorageExportsQuery = _context.StorageTransactions
                .AsNoTracking()
                .Where(s => s.TransactionDate >= startRange && s.TransactionDate < endRange)
                .Where(s => s.Action == "Export" || s.ServiceType == "صرف");

            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeStorageExportsQuery = rangeStorageExportsQuery.Where(s => s.PONumber == selectedPoTrimmed && s.IsExportRelatedToPO);
            }

            var rangeStorageExports = await rangeStorageExportsQuery.SumAsync(s => (decimal?)s.OutgoingQuantity) ?? 0m;
            rangeStorageExports = Math.Round(rangeStorageExports, 3);

            // 7.4 Storage Waste
            var rangeStorageWasteQuery = _context.StorageTransactions
                .AsNoTracking()
                .Where(s => s.TransactionDate >= startRange && s.TransactionDate < endRange)
                .Where(s => s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue);

            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeStorageWasteQuery = rangeStorageWasteQuery.Where(s => s.PONumber == selectedPoTrimmed && s.IsExportRelatedToPO);
            }

            var rangeStorageWasteRows = await rangeStorageWasteQuery
                .Select(s => new
                {
                    s.PONumber,
                    Waste = Math.Abs(s.InternalNetWeight - s.ExternalNetWeight!.Value)
                })
                .ToListAsync();

            var rangeStorageWaste = Math.Round(rangeStorageWasteRows.Sum(s => s.Waste), 3);
            decimal rangeStorageWastePrice = 0m;
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeStorageWastePrice = Math.Round(rangeStorageWaste * activePoSummary.TonPrice, 2);
            }
            else
            {
                var poPriceMap = allPosSummary.ToDictionary(p => p.PoNumber, p => p.TonPrice, StringComparer.OrdinalIgnoreCase);
                rangeStorageWastePrice = Math.Round(rangeStorageWasteRows.Sum(s =>
                {
                    var price = 0m;
                    if (!string.IsNullOrWhiteSpace(s.PONumber))
                    {
                        poPriceMap.TryGetValue(s.PONumber, out price);
                    }
                    return s.Waste * price;
                }), 2);
            }

            var totalRangeWaste = Math.Round(rangeTransWaste + rangeStorageWaste, 3);
            var totalRangeWastePrice = Math.Round(rangeTransWastePrice + rangeStorageWastePrice, 2);

            var dailyActivity = new FinanceDailyActivityDto
            {
                FromDate = dFrom,
                ToDate = dTo,
                SelectedDate = dFrom,
                TransportationTransactionsCount = rangeTransCount,
                TransportationNetWeight = rangeTransNetWeight,
                TransportationWasteQuantity = rangeTransWaste,
                TransportationWastePrice = rangeTransWastePrice,
                StorageImportedQuantity = rangeStorageImports,
                StorageImportedPrice = null,
                StorageImportedPriceNote = "لا يوجد مصدر تسعير للواردات المخزنية في قاعدة البيانات",
                StorageExportedQuantity = rangeStorageExports,
                StorageWasteQuantity = rangeStorageWaste,
                StorageWastePrice = rangeStorageWastePrice,
                TotalDailyWasteQuantity = totalRangeWaste,
                TotalDailyWastePrice = totalRangeWastePrice
            };

            var response = new FinanceStatisticsResponseDto
            {
                FromDate = dFrom,
                ToDate = dTo,
                SelectedDate = dFrom,
                SelectedPo = selectedPoTrimmed,
                IsCurrentUserFinanceDirector = isFinanceDirector,
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
    /// Retrieves historical list of all Purchase Orders with real database metrics.
    /// Supports both /api/finance/pos and /api/finance/purchase-orders.
    /// </summary>
    [HttpGet("pos")]
    [HttpGet("purchase-orders")]
    [ProducesResponseType(typeof(ApiResponse<List<FinancePoSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPurchaseOrders()
    {
        try
        {
            var pos = await _context.PurchaseOrders
                .AsNoTracking()
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync();

            var allPayments = await _context.FinancePayments
                .AsNoTracking()
                .ToListAsync();

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

            var results = new List<FinancePoSummaryDto>();
            foreach (var p in pos)
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

                var poStatus = remainingQty <= 0 ? "مكتمل" : (transCount > 0 ? "قيد التوريد" : "معلق");
                var paymentStatus = !string.IsNullOrWhiteSpace(p.PaymentStatus) ? p.PaymentStatus : "غير مدفوع";

                results.Add(new FinancePoSummaryDto
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
                    OutstandingAmount = outstanding,
                    Status = poStatus,
                    PaymentStatus = paymentStatus,
                    PaymentStatusUpdatedAt = p.PaymentStatusUpdatedAt,
                    PaymentStatusUpdatedBy = p.PaymentStatusUpdatedBy
                });
            }

            return Ok(ApiResponse<List<FinancePoSummaryDto>>.Ok(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase orders list.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("حدث خطأ أثناء استرجاع قائمة أوامر الشراء."));
        }
    }

    /// <summary>
    /// Retrieves comprehensive details for a specific Purchase Order including transportation history,
    /// storage history, waste analysis, chronological timeline, and payment status details.
    /// </summary>
    [HttpGet("purchase-orders/{poNumber}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPurchaseOrderDetails(string poNumber)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
        {
            return BadRequest(ApiResponse.Fail("رقم أمر الشراء مطلوب."));
        }

        var poNum = poNumber.Trim();
        var po = await _context.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Payments)
            .FirstOrDefaultAsync(p => p.PONumber == poNum);

        if (po == null)
        {
            return NotFound(ApiResponse.Fail($"لم يتم العثور على أمر الشراء '{poNum}'."));
        }

        var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
        var isFinanceDirector = AppRoles.IsFinanceDirector(userRole);

        // Fetch Transportation transactions
        var transRecords = await _context.Transportations
            .AsNoTracking()
            .Where(t => t.PONumber == poNum)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        // Fetch Storage transactions
        var storageRecords = await _context.StorageTransactions
            .AsNoTracking()
            .Where(s => s.PONumber == poNum)
            .OrderByDescending(s => s.TransactionDate)
            .ToListAsync();

        // Fetch Payment audits
        var paymentAudits = await _context.PurchaseOrderPaymentAudits
            .AsNoTracking()
            .Where(a => a.PONumber == poNum)
            .OrderByDescending(a => a.ChangedAt)
            .Select(a => new PaymentStatusAuditDto
            {
                Id = a.Id,
                PoNumber = a.PONumber,
                PreviousStatus = a.PreviousStatus,
                NewStatus = a.NewStatus,
                ChangedBy = a.ChangedBy,
                ChangedAt = a.ChangedAt,
                Notes = a.Notes
            })
            .ToListAsync();

        var tonPrice = po.TonPrice > 0 ? po.TonPrice : (po.PricePerTon > 0 ? po.PricePerTon : 0m);
        var expectedQty = po.Quantity;
        var expectedPrice = Math.Round(expectedQty * tonPrice, 2);

        var transCount = transRecords.Count;
        var transNet = Math.Round(transRecords.Sum(t => t.NetWeight), 3);
        var transWaste = Math.Round(transRecords.Sum(t => t.WasteQuantity), 3);
        var transWastePrice = Math.Round(transWaste * tonPrice, 2);

        var storageWaste = Math.Round(storageRecords
            .Where(s => s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue)
            .Sum(s => Math.Abs(s.InternalNetWeight - s.ExternalNetWeight!.Value)), 3);
        var storageWastePrice = Math.Round(storageWaste * tonPrice, 2);

        var totalWaste = Math.Round(transWaste + storageWaste, 3);
        var totalWastePrice = Math.Round(totalWaste * tonPrice, 2);

        var deliveredQty = Math.Round(Math.Max(0m, expectedQty - totalWaste), 3);
        var deliveredPrice = Math.Round(deliveredQty * tonPrice, 2);

        var remainingQty = Math.Round(po.RemainingQuantity, 3);
        var remainingPrice = Math.Round(remainingQty * tonPrice, 2);

        var perfPct = expectedQty > 0
            ? Math.Round((deliveredQty / expectedQty) * 100m, 2)
            : 100m;
        var perfPricePct = expectedPrice > 0
            ? Math.Round((deliveredPrice / expectedPrice) * 100m, 2)
            : 100m;

        var paidSum = Math.Round(po.Payments.Sum(p => p.Amount), 2);
        var outstanding = Math.Round(Math.Max(0m, deliveredPrice - paidSum), 2);

        var status = remainingQty <= 0 ? "مكتمل" : (transCount > 0 ? "قيد التوريد" : "معلق");
        var paymentStatus = !string.IsNullOrWhiteSpace(po.PaymentStatus) ? po.PaymentStatus : "غير مدفوع";

        // Map transportation items
        var transDtoList = transRecords.Select(t => new PoTransportationItemDto
        {
            TransactionNumber = t.TransactionNumber,
            TransactionDate = t.TransactionDate,
            CarNumber = t.CarNumber,
            DriverName = t.DriverName,
            Source = t.Source,
            Destination = t.Destination,
            MaterialType = t.MaterialType,
            InternalWeight = t.InternalWeight,
            ExternalNetWeight = t.ExternalNetWeight,
            NetWeight = t.NetWeight,
            WasteQuantity = t.WasteQuantity,
            TransactionStatus = t.TransactionStatus,
            ReceivingStatus = t.ReceivingStatus,
            Notes = t.Notes,
            ShippingSupervisor = t.ShippingSupervisor,
            DepartmentSupervisor = t.DepartmentSupervisor
        }).ToList();

        // Map storage items
        var storageDtoList = storageRecords.Select(s => new PoStorageItemDto
        {
            MovementNumber = s.MovementNumber,
            TransactionDate = s.TransactionDate,
            Action = s.Action,
            ServiceType = s.ServiceType,
            Material = s.Material,
            IncomingQuantity = s.IncomingQuantity,
            OutgoingQuantity = s.OutgoingQuantity,
            InternalNetWeight = s.InternalNetWeight,
            ExternalNetWeight = s.ExternalNetWeight,
            WasteQuantity = (s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue) ? Math.Abs(s.InternalNetWeight - s.ExternalNetWeight.Value) : 0m,
            StorageLocation = s.StorageLocation,
            ReceivingEmployee = s.ReceivingEmployee,
            Notes = s.Notes
        }).ToList();

        // Map payments
        var paymentsDtoList = po.Payments.Select(pay => new FinancePaymentDto
        {
            Id = pay.Id,
            PoNumber = pay.PONumber,
            Amount = pay.Amount,
            PaymentDate = pay.PaymentDate,
            PaymentMethod = pay.PaymentMethod,
            ReferenceNumber = pay.ReferenceNumber,
            PaidBy = pay.PaidBy,
            Notes = pay.Notes,
            CreatedAt = pay.CreatedAt
        }).OrderByDescending(p => p.PaymentDate).ToList();

        // Build Chronological Timeline (Requirement 9)
        var timeline = new List<PoTimelineEventDto>();

        // 1. PO Created Event
        timeline.Add(new PoTimelineEventDto
        {
            Timestamp = po.OrderDate,
            EventType = "PO_CREATED",
            Title = "إصدار أمر الشراء",
            Description = $"تم تسجيل أمر الشراء بكمية تعاقدية {po.Quantity:N3} طن وبسعر {tonPrice:N2} ج.م/طن للمورد {po.Supplier}.",
            Details = $"الوجهة: {po.Destination} | إجمالي القيمة: {expectedPrice:N2} ج.م",
            BadgeType = "info"
        });

        // 2. Transportation Transactions
        foreach (var t in transRecords)
        {
            timeline.Add(new PoTimelineEventDto
            {
                Timestamp = t.TransactionDate,
                EventType = "TRANSPORTATION",
                Title = $"حركة نقل #{t.TransactionNumber}",
                Description = $"سيارة: {t.CarNumber} | السائق: {t.DriverName} | صافي الشحنة: {t.NetWeight:N3} طن",
                Details = $"هالك النقل: {t.WasteQuantity:N3} طن | حالة الاستلام: {t.ReceivingStatus}",
                BadgeType = t.WasteQuantity > 0 ? "warning" : "success"
            });
        }

        // 3. Storage Transactions
        foreach (var s in storageRecords)
        {
            var sWaste = (s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue)
                ? Math.Abs(s.InternalNetWeight - s.ExternalNetWeight.Value)
                : 0m;
            timeline.Add(new PoTimelineEventDto
            {
                Timestamp = s.TransactionDate,
                EventType = "STORAGE",
                Title = $"حركة مستودع #{s.MovementNumber} ({s.Action})",
                Description = $"الكمية: {(s.Action == "Import" ? s.IncomingQuantity : s.OutgoingQuantity):N3} طن | المستلم: {s.ReceivingEmployee}",
                Details = $"الموقع: {s.StorageLocation} | هالك التخزين: {sWaste:N3} طن",
                BadgeType = "neutral"
            });
        }

        // 4. CEO Payments
        foreach (var pay in po.Payments)
        {
            timeline.Add(new PoTimelineEventDto
            {
                Timestamp = pay.PaymentDate,
                EventType = "PAYMENT",
                Title = $"سداد دفعة مالية ({pay.Amount:N2} ج.م)",
                Description = $"طريقة السداد: {pay.PaymentMethod} | معتمد من: {pay.PaidBy}",
                Details = $"المرجع: {pay.ReferenceNumber ?? "-"} | ملاحظات: {pay.Notes ?? "-"}",
                BadgeType = "success"
            });
        }

        // 5. Payment Status Changes
        foreach (var aud in paymentAudits)
        {
            timeline.Add(new PoTimelineEventDto
            {
                Timestamp = aud.ChangedAt,
                EventType = "PAYMENT_STATUS_CHANGE",
                Title = $"تغيير حالة السداد: {aud.NewStatus}",
                Description = $"تم التعديل بواسطة: {aud.ChangedBy} (الحالة السابقة: {aud.PreviousStatus})",
                Details = aud.Notes ?? "تحديث معتمد من الإدارة المالية",
                BadgeType = "accent"
            });
        }

        // Sort chronologically (oldest to newest)
        timeline = timeline.OrderBy(x => x.Timestamp).ToList();

        var detailDto = new PurchaseOrderDetailDto
        {
            Id = po.Id,
            PoNumber = po.PONumber,
            Supplier = po.Supplier,
            Destination = po.Destination,
            OrderDate = po.OrderDate,
            Quantity = po.Quantity,
            TonPrice = tonPrice,
            TotalValue = expectedPrice,
            InvoiceNumber = po.InvoiceNumber,
            Notes = po.Notes,
            Status = status,
            CreatedAt = po.CreatedAt,
            DeliveredQuantity = deliveredQty,
            DeliveredPrice = deliveredPrice,
            RemainingQuantity = remainingQty,
            RemainingPrice = remainingPrice,
            TransportationTransactionsCount = transCount,
            TotalSuppliedNetWeight = transNet,
            TransportationWasteQuantity = transWaste,
            TransportationWastePrice = transWastePrice,
            StorageWasteQuantity = storageWaste,
            StorageWastePrice = storageWastePrice,
            TotalWasteQuantity = totalWaste,
            TotalWastePrice = totalWastePrice,
            PerformancePercentage = perfPct,
            PerformancePricePercentage = perfPricePct,
            PaidAmount = paidSum,
            OutstandingAmount = outstanding,
            PaymentStatus = paymentStatus,
            PaymentStatusUpdatedAt = po.PaymentStatusUpdatedAt,
            PaymentStatusUpdatedBy = po.PaymentStatusUpdatedBy,
            IsCurrentUserFinanceDirector = isFinanceDirector,
            PaymentAudits = paymentAudits,
            Payments = paymentsDtoList,
            TransportationTransactions = transDtoList,
            StorageTransactions = storageDtoList,
            Timeline = timeline
        };

        return Ok(ApiResponse<PurchaseOrderDetailDto>.Ok(detailDto));
    }

    /// <summary>
    /// Updates the Payment Status for a Purchase Order.
    /// Strictly authorized to Finance Director role server-side.
    /// Rejects unauthorized users with 403 Forbidden.
    /// </summary>
    [HttpPut("purchase-orders/{poNumber}/payment-status")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePaymentStatus(string poNumber, [FromBody] UpdatePaymentStatusDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات التحديث غير صالحة."));
        }

        var rawStatus = !string.IsNullOrWhiteSpace(dto.Status) 
            ? dto.Status 
            : (!string.IsNullOrWhiteSpace(dto.PaymentStatus) ? dto.PaymentStatus : dto.NewStatus);
        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            return BadRequest(ApiResponse.Fail("حالة السداد مطلوبة."));
        }

        var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
        if (!AppRoles.IsFinanceDirector(userRole))
        {
            _logger.LogWarning("Unauthorized attempt to update payment status for PO '{PO}' by role '{Role}'.", poNumber, userRole);
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("عذراً، تغيير حالة السداد مسموح به لمدير المالية فقط."));
        }

        var poNum = poNumber.Trim();
        var po = await _context.PurchaseOrders
            .FirstOrDefaultAsync(p => p.PONumber == poNum);

        if (po == null)
        {
            return NotFound(ApiResponse.Fail($"أمر الشراء '{poNum}' غير موجود."));
        }

        var prevStatus = string.IsNullOrWhiteSpace(po.PaymentStatus) ? "غير مدفوع" : po.PaymentStatus;
        var newStatus = rawStatus.Trim();
        var callerName = User.FindFirst(ClaimTypes.Name)?.Value 
            ?? User.FindFirst("fullName")?.Value 
            ?? "Finance Director";

        po.PaymentStatus = newStatus;
        po.PaymentStatusUpdatedAt = DateTime.UtcNow;
        po.PaymentStatusUpdatedBy = callerName;
        po.UpdatedAt = DateTime.UtcNow;

        var audit = new PurchaseOrderPaymentAudit
        {
            PurchaseOrderId = po.Id,
            PONumber = po.PONumber,
            PreviousStatus = prevStatus,
            NewStatus = newStatus,
            ChangedBy = callerName,
            ChangedAt = DateTime.UtcNow,
            Notes = dto.Notes?.Trim()
        };

        await _context.PurchaseOrderPaymentAudits.AddAsync(audit);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment status for PO '{PO}' changed from '{Prev}' to '{New}' by '{Caller}'.",
            poNum, prevStatus, newStatus, callerName);

        return await GetPurchaseOrderDetails(poNum);
    }

    /// <summary>
    /// Retrieves Factory Performance Details explaining WHERE every summary performance number came from,
    /// showing transparent step-by-step arithmetic formulas, contributing transportation/storage records,
    /// and clearly distinguishing Date Range Activity from Cumulative PO Status.
    /// </summary>
    [HttpGet("performance")]
    [ProducesResponseType(typeof(ApiResponse<FinancePerformanceDetailsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPerformanceDetails(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] DateTime? date,
        [FromQuery] string? po)
    {
        try
        {
            var dFrom = (fromDate ?? date ?? DateTime.Today).Date;
            var dTo = (toDate ?? date ?? dFrom).Date;
            if (dFrom > dTo)
            {
                var temp = dFrom;
                dFrom = dTo;
                dTo = temp;
            }
            var startRange = dFrom;
            var endRange = dTo.AddDays(1);

            var selectedPoTrimmed = string.IsNullOrWhiteSpace(po) || po.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? null
                : po.Trim();

            // 1. Authoritative POs
            var poQuery = _context.PurchaseOrders.AsNoTracking();
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                poQuery = poQuery.Where(p => p.PONumber == selectedPoTrimmed);
            }
            var poList = await poQuery.ToListAsync();

            decimal expectedQty = poList.Sum(p => p.Quantity);
            decimal expectedPrice = poList.Sum(p => p.Quantity * (p.TonPrice > 0 ? p.TonPrice : (p.PricePerTon > 0 ? p.PricePerTon : 0m)));
            decimal averageTonPrice = expectedQty > 0 ? Math.Round(expectedPrice / expectedQty, 2) : 0m;

            // 2. Cumulative Transportation for these PO(s)
            var transQuery = _context.Transportations.AsNoTracking();
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                transQuery = transQuery.Where(t => t.PONumber == selectedPoTrimmed);
            }
            var cumTransList = await transQuery
                .Select(t => new { t.PONumber, t.NetWeight, t.WasteQuantity, t.TransactionDate })
                .ToListAsync();

            decimal cumTransWaste = Math.Round(cumTransList.Sum(t => t.WasteQuantity), 3);
            decimal cumTransWastePrice = Math.Round(cumTransWaste * averageTonPrice, 2);

            // 3. Cumulative Storage Waste for these PO(s)
            var storageQuery = _context.StorageTransactions.AsNoTracking()
                .Where(s => s.IsExportRelatedToPO && s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue);
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                storageQuery = storageQuery.Where(s => s.PONumber == selectedPoTrimmed);
            }
            var cumStorageRows = await storageQuery
                .Select(s => new { s.PONumber, Waste = Math.Abs(s.InternalNetWeight - s.ExternalNetWeight!.Value) })
                .ToListAsync();

            decimal cumStorageWaste = Math.Round(cumStorageRows.Sum(s => s.Waste), 3);
            decimal cumStorageWastePrice = Math.Round(cumStorageWaste * averageTonPrice, 2);

            decimal totalWaste = Math.Round(cumTransWaste + cumStorageWaste, 3);
            decimal totalWastePrice = Math.Round(cumTransWastePrice + cumStorageWastePrice, 2);

            decimal deliveredQty = Math.Round(Math.Max(0m, expectedQty - totalWaste), 3);
            decimal deliveredPrice = Math.Round(deliveredQty * averageTonPrice, 2);

            decimal remainingQty = Math.Round(poList.Sum(p => p.RemainingQuantity), 3);
            decimal remainingPrice = Math.Round(remainingQty * averageTonPrice, 2);

            decimal perfPct = expectedQty > 0 ? Math.Round((deliveredQty / expectedQty) * 100m, 2) : 100m;
            decimal perfPricePct = expectedPrice > 0 ? Math.Round((deliveredPrice / expectedPrice) * 100m, 2) : 100m;

            var status = perfPct >= 95m ? "High" : (perfPct >= 80m ? "Medium" : "Low");
            var statusArabic = status == "High" ? "أداء ممتاز" : (status == "Medium" ? "أداء مستقر" : "تنبيه هالك");

            // 4. Selected Date Range Activity (strictly within [dFrom, dTo])
            var rangeTransQuery = _context.Transportations.AsNoTracking()
                .Where(t => t.TransactionDate >= startRange && t.TransactionDate < endRange);
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeTransQuery = rangeTransQuery.Where(t => t.PONumber == selectedPoTrimmed);
            }
            var rangeTransList = await rangeTransQuery.Select(t => new { t.NetWeight, t.WasteQuantity }).ToListAsync();
            var rangeTransCount = rangeTransList.Count;
            var rangeTransNet = Math.Round(rangeTransList.Sum(t => t.NetWeight), 3);
            var rangeTransWaste = Math.Round(rangeTransList.Sum(t => t.WasteQuantity), 3);

            var rangeStorageImports = await _context.StorageTransactions.AsNoTracking()
                .Where(s => s.TransactionDate >= startRange && s.TransactionDate < endRange)
                .Where(s => s.Action == "Import" || s.ServiceType == "إيداع")
                .SumAsync(s => (decimal?)s.IncomingQuantity) ?? 0m;
            rangeStorageImports = Math.Round(rangeStorageImports, 3);

            var rangeStorageExportsQuery = _context.StorageTransactions.AsNoTracking()
                .Where(s => s.TransactionDate >= startRange && s.TransactionDate < endRange)
                .Where(s => s.Action == "Export" || s.ServiceType == "صرف");
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeStorageExportsQuery = rangeStorageExportsQuery.Where(s => s.PONumber == selectedPoTrimmed && s.IsExportRelatedToPO);
            }
            var rangeStorageExports = await rangeStorageExportsQuery.SumAsync(s => (decimal?)s.OutgoingQuantity) ?? 0m;
            rangeStorageExports = Math.Round(rangeStorageExports, 3);

            var rangeStorageWasteQuery = _context.StorageTransactions.AsNoTracking()
                .Where(s => s.TransactionDate >= startRange && s.TransactionDate < endRange)
                .Where(s => s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue);
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeStorageWasteQuery = rangeStorageWasteQuery.Where(s => s.PONumber == selectedPoTrimmed && s.IsExportRelatedToPO);
            }
            var rangeStorageWaste = await rangeStorageWasteQuery
                .Select(s => Math.Abs(s.InternalNetWeight - s.ExternalNetWeight!.Value))
                .SumAsync();
            rangeStorageWaste = Math.Round(rangeStorageWaste, 3);

            var rangeTotalWaste = Math.Round(rangeTransWaste + rangeStorageWaste, 3);

            // Step-by-step arithmetic explanations (Requirements 11, 12, 13)
            var steps = new List<string>
            {
                $"الكمية المتوقعة: {expectedQty:N3} طن (تمثل إجمالي الكمية المتعاقد عليها لأوامر الشراء المحددة).",
                $"متوسط سعر الطن: {averageTonPrice:N2} ج.م/طن (المحدد في عقود أوامر الشراء).",
                $"القيمة المالية المتوقعة: {expectedQty:N3} طن × {averageTonPrice:N2} ج.م = {expectedPrice:N2} ج.م.",
                $"هالك النقل التراكمي: {cumTransWaste:N3} طن (مجموع الفروق بين أوزان الميزان الداخلي والخارجي لحركات النقل).",
                $"هالك التخزين التراكمي: {cumStorageWaste:N3} طن (مجموع الفروق بين أوزان الميزان الداخلي والخارجي لصادر المخزن).",
                $"إجمالي الهالك = هالك النقل ({cumTransWaste:N3}) + هالك التخزين ({cumStorageWaste:N3}) = {totalWaste:N3} طن.",
                $"القيمة المالية للهالك: {totalWaste:N3} طن × {averageTonPrice:N2} ج.م = {totalWastePrice:N2} ج.م.",
                $"الكمية المسلمة الفعلية = الكمية المتوقعة ({expectedQty:N3}) - إجمالي الهالك ({totalWaste:N3}) = {deliveredQty:N3} طن.",
                $"القيمة المالية للكمية المسلمة: {deliveredQty:N3} طن × {averageTonPrice:N2} ج.م = {deliveredPrice:N2} ج.م.",
                $"الكمية المتبقية: {remainingQty:N3} طن بقيمة تعاقدية {remainingPrice:N2} ج.م.",
                $"نسبة أداء المصنع (الكمية) = ({deliveredQty:N3} / {expectedQty:N3}) × 100 = {perfPct:F2}%.",
                $"نسبة الأداء المالي = ({deliveredPrice:N2} / {expectedPrice:N2}) × 100 = {perfPricePct:F2}%."
            };

            var poBreakdowns = new List<FinancePoSummaryDto>();
            foreach (var p in poList)
            {
                var pQty = p.Quantity;
                var pTonPrice = p.TonPrice > 0 ? p.TonPrice : (p.PricePerTon > 0 ? p.PricePerTon : 0m);
                var pExpectedPrice = Math.Round(pQty * pTonPrice, 2);

                var pTrans = cumTransList.Where(t => t.PONumber.Equals(p.PONumber, StringComparison.OrdinalIgnoreCase)).ToList();
                var pTransWaste = Math.Round(pTrans.Sum(t => t.WasteQuantity), 3);
                var pTransNet = Math.Round(pTrans.Sum(t => t.NetWeight), 3);

                var pStorageWaste = Math.Round(cumStorageRows.Where(s => !string.IsNullOrEmpty(s.PONumber) && s.PONumber.Equals(p.PONumber, StringComparison.OrdinalIgnoreCase)).Sum(s => s.Waste), 3);
                var pTotalWaste = Math.Round(pTransWaste + pStorageWaste, 3);
                var pDeliveredQty = Math.Round(Math.Max(0m, pQty - pTotalWaste), 3);
                var pDeliveredPrice = Math.Round(pDeliveredQty * pTonPrice, 2);
                var pRemQty = Math.Round(p.RemainingQuantity, 3);
                var pRemPrice = Math.Round(pRemQty * pTonPrice, 2);
                var pPerf = pQty > 0 ? Math.Round((pDeliveredQty / pQty) * 100m, 2) : 100m;

                poBreakdowns.Add(new FinancePoSummaryDto
                {
                    PoNumber = p.PONumber,
                    Supplier = p.Supplier,
                    Destination = p.Destination,
                    TonPrice = pTonPrice,
                    OrderDate = p.OrderDate,
                    ExpectedQuantity = pQty,
                    ExpectedPrice = pExpectedPrice,
                    DeliveredQuantity = pDeliveredQty,
                    DeliveredPrice = pDeliveredPrice,
                    RemainingQuantity = pRemQty,
                    RemainingPrice = pRemPrice,
                    TransportationTransactionsCount = pTrans.Count,
                    TransportationWasteQuantity = pTransWaste,
                    StorageWasteQuantity = pStorageWaste,
                    TotalWasteQuantity = pTotalWaste,
                    TotalWastePrice = Math.Round(pTotalWaste * pTonPrice, 2),
                    PerformancePercentage = pPerf,
                    Status = pRemQty <= 0 ? "مكتمل" : (pTrans.Count > 0 ? "قيد التوريد" : "معلق"),
                    PaymentStatus = !string.IsNullOrWhiteSpace(p.PaymentStatus) ? p.PaymentStatus : "غير مدفوع"
                });
            }

            var detailsDto = new FinancePerformanceDetailsDto
            {
                FromDate = dFrom,
                ToDate = dTo,
                SelectedPo = selectedPoTrimmed,
                Scope = selectedPoTrimmed == null ? "AllPos" : "SinglePo",
                ExpectedQuantity = expectedQty,
                TonPrice = averageTonPrice,
                ExpectedPrice = expectedPrice,
                ExpectedFormula = $"{expectedQty:N3} طن × {averageTonPrice:N2} ج.م = {expectedPrice:N2} ج.م",
                DeliveredQuantity = deliveredQty,
                DeliveredPrice = deliveredPrice,
                DeliveredFormula = $"{expectedQty:N3} طن (المتوقع) - {totalWaste:N3} طن (الهالك) = {deliveredQty:N3} طن",
                TransportationWasteQuantity = cumTransWaste,
                TransportationWastePrice = cumTransWastePrice,
                TransportationWasteFormula = $"مجموع هالك حركات النقل = {cumTransWaste:N3} طن (بقيمة {cumTransWastePrice:N2} ج.م)",
                StorageWasteQuantity = cumStorageWaste,
                StorageWastePrice = cumStorageWastePrice,
                StorageWasteFormula = $"مجموع هالك حركات المخازن = {cumStorageWaste:N3} طن (بقيمة {cumStorageWastePrice:N2} ج.م)",
                TotalWasteQuantity = totalWaste,
                TotalWastePrice = totalWastePrice,
                TotalWasteFormula = $"هالك النقل ({cumTransWaste:N3}) + هالك التخزين ({cumStorageWaste:N3}) = {totalWaste:N3} طن",
                RemainingQuantity = remainingQty,
                RemainingPrice = remainingPrice,
                RemainingFormula = $"{remainingQty:N3} طن × {averageTonPrice:N2} ج.م = {remainingPrice:N2} ج.م",
                QuantityPerformancePercentage = perfPct,
                QuantityPerformanceFormula = $"({deliveredQty:N3} / {expectedQty:N3}) × 100 = {perfPct:F2}%",
                PricePerformancePercentage = perfPricePct,
                PricePerformanceFormula = $"({deliveredPrice:N2} / {expectedPrice:N2}) × 100 = {perfPricePct:F2}%",
                Status = status,
                StatusLabelArabic = statusArabic,
                RangeTransportationTransactionsCount = rangeTransCount,
                RangeTransportationNetWeight = rangeTransNet,
                RangeTransportationWaste = rangeTransWaste,
                RangeStorageImportQuantity = rangeStorageImports,
                RangeStorageExportQuantity = rangeStorageExports,
                RangeStorageWaste = rangeStorageWaste,
                RangeTotalWaste = rangeTotalWaste,
                StepByStepCalculations = steps,
                PoBreakdowns = poBreakdowns
            };

            // Real Forecasting Calculation from active database POs
            var activePos = poList.Where(p => p.RemainingQuantity > 0).ToList();
            var expectedRemQty = activePos.Sum(p => p.RemainingQuantity);
            var expectedRemVal = activePos.Sum(p => p.RemainingQuantity * (p.TonPrice > 0 ? p.TonPrice : p.PricePerTon));
            var overallProgress = expectedQty > 0 ? Math.Round((deliveredQty / expectedQty) * 100, 2) : 0m;
            
            // Average daily delivery pace based on real transportation transactions
            var earliestTransDate = cumTransList.Any() ? cumTransList.Min(t => t.TransactionDate) : (DateTime?)null;
            var daysActive = earliestTransDate.HasValue ? Math.Max(1, (int)(DateTime.UtcNow.Date - earliestTransDate.Value.Date).TotalDays + 1) : 1;
            var avgDailyQty = daysActive > 0 ? Math.Round(deliveredQty / daysActive, 3) : 0m;
            var estimatedDays = avgDailyQty > 0 ? (int)Math.Ceiling(expectedRemQty / avgDailyQty) : 0;
            var projDate = estimatedDays > 0 ? DateTime.UtcNow.Date.AddDays(estimatedDays) : (DateTime?)null;

            detailsDto.Forecasting = new FinanceForecastingDto
            {
                ExpectedRemainingQuantity = expectedRemQty,
                ExpectedRemainingValue = expectedRemVal,
                OverallProgressPercentage = overallProgress,
                AverageDailyDeliveredQuantity = avgDailyQty,
                EstimatedDaysToCompletion = estimatedDays,
                ProjectedCompletionDate = projDate,
                ExpectedFinalFinancialYield = Math.Max(0, expectedPrice - totalWastePrice),
                ForecastingSummaryArabic = avgDailyQty > 0
                    ? $"بناءً على متوسط توريد فعلي قدره {avgDailyQty:N2} طن/يوم عبر {cumTransList.Count} نقلة منفذة، يُتوقع استكمال الكميات المتبقية ({expectedRemQty:N2} طن) خلال {estimatedDays} يوماً تشغيلياً تقريباً بحلول {projDate:yyyy-MM-dd}."
                    : $"المتبقي للتوريد {expectedRemQty:N2} طن بقيمة متوقعة {expectedRemVal:N2} ج.م في انتظار استئناف دورة حركات النقل لتحديد متوسط الإنجاز اليومي."
            };

            return Ok(ApiResponse<FinancePerformanceDetailsDto>.Ok(detailsDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance details.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("حدث خطأ أثناء احتساب تفاصيل أداء المصنع."));
        }
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

    // ========================================================================
    // 1. التاريخ — FINANCE HISTORY (/api/finance/history)
    // ========================================================================
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<FinanceHistoryResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFinanceHistory(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? transactionType,
        [FromQuery] string? po,
        [FromQuery] string? supplier,
        [FromQuery] string? search)
    {
        try
        {
            var dFrom = fromDate?.Date;
            var dTo = toDate?.Date.AddDays(1); // inclusive

            var items = new List<FinanceHistoryItemDto>();

            // 1. Finance Payments
            var paymentsQuery = _context.FinancePayments.AsNoTracking().AsQueryable();
            if (dFrom.HasValue) paymentsQuery = paymentsQuery.Where(p => p.PaymentDate >= dFrom.Value);
            if (dTo.HasValue) paymentsQuery = paymentsQuery.Where(p => p.PaymentDate < dTo.Value);
            var payments = await paymentsQuery.ToListAsync();

            var poSuppliers = await _context.PurchaseOrders.AsNoTracking()
                .ToDictionaryAsync(p => p.PONumber, p => p.Supplier, StringComparer.OrdinalIgnoreCase);

            foreach (var p in payments)
            {
                poSuppliers.TryGetValue(p.PONumber, out var supName);
                items.Add(new FinanceHistoryItemDto
                {
                    Id = $"PAY-{p.Id}",
                    Date = p.PaymentDate,
                    TransactionType = "سداد دفعة مورد",
                    TransactionNumber = p.ReferenceNumber ?? $"PAY-{p.Id}",
                    PoNumber = p.PONumber,
                    Supplier = supName ?? "-",
                    Amount = p.Amount,
                    Direction = "Out",
                    Status = "مدفوع",
                    User = p.PaidBy ?? "الإدارة المالية",
                    Details = $"سداد دفعة لأمر الشراء {p.PONumber} عبر {p.PaymentMethod}. {p.Notes}",
                    ReferenceNumber = p.ReferenceNumber
                });
            }

            // 2. Bank Transactions
            var bankQuery = _context.BankTransactions.AsNoTracking().AsQueryable();
            if (dFrom.HasValue) bankQuery = bankQuery.Where(b => b.TransactionDate >= dFrom.Value);
            if (dTo.HasValue) bankQuery = bankQuery.Where(b => b.TransactionDate < dTo.Value);
            var bankTxs = await bankQuery.ToListAsync();

            foreach (var b in bankTxs)
            {
                items.Add(new FinanceHistoryItemDto
                {
                    Id = $"BANK-{b.Id}",
                    Date = b.TransactionDate,
                    TransactionType = $"معاملة بنكية: {b.TransactionType}",
                    TransactionNumber = b.ReferenceNumber ?? $"BNK-{b.Id}",
                    PoNumber = null,
                    Supplier = b.BankName,
                    Amount = b.Amount,
                    Direction = b.Direction,
                    Status = "معتمد",
                    User = b.CreatedBy,
                    Details = $"{b.BankName} | {b.Description}. {b.Notes}",
                    ReferenceNumber = b.ReferenceNumber
                });
            }

            // 3. Expenses
            var expensesQuery = _context.Expenses.AsNoTracking().AsQueryable();
            if (dFrom.HasValue) expensesQuery = expensesQuery.Where(e => e.ExpenseDate >= dFrom.Value);
            if (dTo.HasValue) expensesQuery = expensesQuery.Where(e => e.ExpenseDate < dTo.Value);
            var expenses = await expensesQuery.ToListAsync();

            foreach (var e in expenses)
            {
                items.Add(new FinanceHistoryItemDto
                {
                    Id = $"EXP-{e.Id}",
                    Date = e.ExpenseDate,
                    TransactionType = $"مصروف: {e.Category}",
                    TransactionNumber = e.ReferenceNumber ?? $"EXP-{e.Id}",
                    PoNumber = null,
                    Supplier = e.Beneficiary,
                    Amount = e.Amount,
                    Direction = "Out",
                    Status = "مصروف",
                    User = e.CreatedBy,
                    Details = $"{e.Title} | المستفيد: {e.Beneficiary} ({e.PaymentMethod}). {e.Notes}",
                    ReferenceNumber = e.ReferenceNumber
                });
            }

            // 4. Physical Transportation deliveries
            var transQuery = _context.Transportations.AsNoTracking().AsQueryable();
            if (dFrom.HasValue) transQuery = transQuery.Where(t => t.TransactionDate >= dFrom.Value);
            if (dTo.HasValue) transQuery = transQuery.Where(t => t.TransactionDate < dTo.Value);
            var deliveries = await transQuery.OrderByDescending(t => t.TransactionDate).Take(100).ToListAsync();

            var poPrices = await _context.PurchaseOrders.AsNoTracking()
                .ToDictionaryAsync(p => p.PONumber, p => p.TonPrice > 0 ? p.TonPrice : p.PricePerTon, StringComparer.OrdinalIgnoreCase);

            foreach (var t in deliveries)
            {
                poPrices.TryGetValue(t.PONumber, out var tonPrice);
                var estimatedVal = Math.Round(t.NetWeight * tonPrice, 2);
                items.Add(new FinanceHistoryItemDto
                {
                    Id = $"TR-{t.Id}",
                    Date = t.TransactionDate,
                    TransactionType = "توريد خامات (نقل)",
                    TransactionNumber = $"TR-{t.TransactionNumber}",
                    PoNumber = t.PONumber,
                    Supplier = !string.IsNullOrWhiteSpace(t.CompanyName) ? t.CompanyName : t.Source,
                    Amount = estimatedVal,
                    Direction = "In",
                    Status = t.ReceivingStatus ?? "مستلم",
                    User = t.ShippingSupervisor ?? "مسؤول الشحن",
                    Details = $"توريد {t.NetWeight:N3} طن خامة {t.MaterialType} | سيارة: {t.CarNumber} | هالك: {t.WasteQuantity:N3} طن",
                    ReferenceNumber = t.InternalWeighbridgeCode ?? t.BillOfLadingFileName
                });
            }

            // In-Memory filtering for search, po, supplier, type
            if (!string.IsNullOrWhiteSpace(transactionType) && !transactionType.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(i => i.TransactionType.Contains(transactionType, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(po) && !po.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(i => i.PoNumber != null && i.PoNumber.Equals(po.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(supplier) && !supplier.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(i => i.Supplier != null && i.Supplier.Contains(supplier.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim();
                items = items.Where(i => 
                    i.TransactionNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (i.PoNumber != null && i.PoNumber.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (i.Supplier != null && i.Supplier.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    i.Details.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (i.ReferenceNumber != null && i.ReferenceNumber.Contains(q, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            items = items.OrderByDescending(i => i.Date).ToList();

            var totalIn = items.Where(i => i.Direction.Equals("In", StringComparison.OrdinalIgnoreCase)).Sum(i => i.Amount);
            var totalOut = items.Where(i => i.Direction.Equals("Out", StringComparison.OrdinalIgnoreCase)).Sum(i => i.Amount);

            return Ok(ApiResponse<FinanceHistoryResponseDto>.Ok(new FinanceHistoryResponseDto
            {
                Items = items,
                TotalCount = items.Count,
                TotalInflow = totalIn,
                TotalOutflow = totalOut,
                FilterFromDate = fromDate?.ToString("yyyy-MM-dd"),
                FilterToDate = toDate?.ToString("yyyy-MM-dd"),
                FilterTransactionType = transactionType,
                FilterPo = po,
                FilterSupplier = supplier
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Finance History.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب السجل التاريخي للمالية."));
        }
    }

    // ========================================================================
    // 2. الموردين — SUPPLIERS (/api/finance/suppliers)
    // ========================================================================
    [HttpGet("suppliers")]
    [ProducesResponseType(typeof(ApiResponse<List<FinanceSupplierItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuppliers()
    {
        try
        {
            var pos = await _context.PurchaseOrders.AsNoTracking().ToListAsync();
            var trans = await _context.Transportations.AsNoTracking().ToListAsync();
            var payments = await _context.FinancePayments.AsNoTracking().ToListAsync();

            var supplierGroups = pos.GroupBy(p => p.Supplier.Trim(), StringComparer.OrdinalIgnoreCase);
            var result = new List<FinanceSupplierItemDto>();

            foreach (var g in supplierGroups)
            {
                var sName = g.Key;
                var sPos = g.ToList();
                var sPoNumbers = sPos.Select(p => p.PONumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

                var sTrans = trans.Where(t => sPoNumbers.Contains(t.PONumber)).ToList();
                var sPayments = payments.Where(p => sPoNumbers.Contains(p.PONumber)).ToList();

                var contractedQty = sPos.Sum(p => p.Quantity);
                var deliveredQty = sTrans.Sum(t => t.NetWeight);
                var remQty = sPos.Sum(p => p.RemainingQuantity);
                var totalVal = sPos.Sum(p => p.TotalValue > 0 ? p.TotalValue : (p.Quantity * (p.TonPrice > 0 ? p.TonPrice : p.PricePerTon)));
                var paidAmt = sPayments.Sum(p => p.Amount);
                var outstandingAmt = Math.Max(0, totalVal - paidAmt);

                var materials = sTrans.Select(t => t.MaterialType).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
                if (!materials.Any()) materials.Add("مواد خرسانية / ركام");

                var activeCount = sPos.Count(p => p.RemainingQuantity > 0);
                var completedCount = sPos.Count(p => p.RemainingQuantity <= 0);

                result.Add(new FinanceSupplierItemDto
                {
                    SupplierName = sName,
                    Materials = string.Join(", ", materials),
                    PurchaseOrdersCount = sPos.Count,
                    TotalContractedQuantity = contractedQty,
                    TotalDeliveredQuantity = deliveredQty,
                    TotalRemainingQuantity = remQty,
                    TotalFinancialValue = totalVal,
                    TotalPaidAmount = paidAmt,
                    TotalOutstandingAmount = outstandingAmt,
                    ActivePoCount = activeCount,
                    CompletedPoCount = completedCount
                });
            }

            result = result.OrderByDescending(s => s.TotalContractedQuantity).ToList();
            return Ok(ApiResponse<List<FinanceSupplierItemDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving suppliers list.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب بيانات الموردين."));
        }
    }

    [HttpGet("suppliers/{id}")]
    [ProducesResponseType(typeof(ApiResponse<FinanceSupplierDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupplierDetail(string id)
    {
        try
        {
            var sName = Uri.UnescapeDataString(id).Trim();
            var pos = await _context.PurchaseOrders.AsNoTracking()
                .Where(p => p.Supplier.ToLower() == sName.ToLower())
                .ToListAsync();

            if (!pos.Any())
            {
                pos = await _context.PurchaseOrders.AsNoTracking()
                    .Where(p => p.Supplier.Contains(sName))
                    .ToListAsync();
            }

            if (!pos.Any())
            {
                return NotFound(ApiResponse.Fail($"المورد '{sName}' غير موجود في قاعدة البيانات."));
            }

            var actualSupplierName = pos.First().Supplier;
            var poNumbers = pos.Select(p => p.PONumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var trans = await _context.Transportations.AsNoTracking()
                .Where(t => poNumbers.Contains(t.PONumber))
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            var payments = await _context.FinancePayments.AsNoTracking()
                .Where(p => poNumbers.Contains(p.PONumber))
                .ToListAsync();

            var contractedQty = pos.Sum(p => p.Quantity);
            var deliveredQty = trans.Sum(t => t.NetWeight);
            var remQty = pos.Sum(p => p.RemainingQuantity);
            var totalVal = pos.Sum(p => p.TotalValue > 0 ? p.TotalValue : (p.Quantity * (p.TonPrice > 0 ? p.TonPrice : p.PricePerTon)));
            var paidAmt = payments.Sum(p => p.Amount);
            var outstandingAmt = Math.Max(0, totalVal - paidAmt);

            var materials = trans.Select(t => t.MaterialType).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
            if (!materials.Any()) materials.Add("مواد خرسانية / ركام");

            var poDtos = pos.Select(p => {
                var pTrans = trans.Where(t => t.PONumber.Equals(p.PONumber, StringComparison.OrdinalIgnoreCase)).ToList();
                var pDelivered = pTrans.Sum(t => t.NetWeight);
                var pWaste = pTrans.Sum(t => t.WasteQuantity);
                var pPerf = p.Quantity > 0 ? Math.Round((pDelivered / p.Quantity) * 100, 2) : 0m;
                var pPrice = p.TonPrice > 0 ? p.TonPrice : p.PricePerTon;
                return new FinancePoSummaryDto
                {
                    PoNumber = p.PONumber,
                    Supplier = p.Supplier,
                    OrderDate = p.OrderDate,
                    ExpectedQuantity = p.Quantity,
                    TonPrice = pPrice,
                    ExpectedPrice = p.TotalValue > 0 ? p.TotalValue : p.Quantity * pPrice,
                    DeliveredQuantity = pDelivered,
                    DeliveredPrice = Math.Round(pDelivered * pPrice, 2),
                    RemainingQuantity = p.RemainingQuantity,
                    RemainingPrice = Math.Round(p.RemainingQuantity * pPrice, 2),
                    TransportationTransactionsCount = pTrans.Count,
                    TransportationWasteQuantity = pWaste,
                    StorageWasteQuantity = 0,
                    TotalWasteQuantity = pWaste,
                    TotalWastePrice = Math.Round(pWaste * pPrice, 2),
                    PerformancePercentage = pPerf,
                    Status = p.RemainingQuantity <= 0 ? "مكتمل" : (pTrans.Count > 0 ? "قيد التوريد" : "معلق"),
                    PaymentStatus = p.PaymentStatus
                };
            }).ToList();

            var recentDeliveries = trans.Take(20).Select(t => new PoTransportationItemDto
            {
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                CarNumber = t.CarNumber,
                DriverName = t.DriverName,
                Source = t.Source,
                Destination = t.Destination,
                MaterialType = t.MaterialType,
                NetWeight = t.NetWeight,
                WasteQuantity = t.WasteQuantity,
                ReceivingStatus = t.ReceivingStatus ?? "مستلم"
            }).ToList();

            return Ok(ApiResponse<FinanceSupplierDetailDto>.Ok(new FinanceSupplierDetailDto
            {
                SupplierName = actualSupplierName,
                Materials = string.Join(", ", materials),
                PurchaseOrdersCount = pos.Count,
                TotalContractedQuantity = contractedQty,
                TotalDeliveredQuantity = deliveredQty,
                TotalRemainingQuantity = remQty,
                TotalFinancialValue = totalVal,
                TotalPaidAmount = paidAmt,
                TotalOutstandingAmount = outstandingAmt,
                PurchaseOrders = poDtos,
                RecentDeliveries = recentDeliveries
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving supplier details for id: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب تفاصيل المورد."));
        }
    }

    // ========================================================================
    // 3. البنك — BANK TRANSACTIONS (/api/finance/bank)
    // ========================================================================
    [HttpGet("bank")]
    [ProducesResponseType(typeof(ApiResponse<BankSummaryResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBankTransactions()
    {
        try
        {
            var txs = await _context.BankTransactions.AsNoTracking()
                .OrderByDescending(b => b.TransactionDate)
                .ToListAsync();

            var dtos = txs.Select(b => new BankTransactionDto
            {
                Id = b.Id,
                TransactionDate = b.TransactionDate,
                TransactionType = b.TransactionType,
                Direction = b.Direction,
                Amount = b.Amount,
                BankName = b.BankName,
                ReferenceNumber = b.ReferenceNumber,
                Description = b.Description,
                CreatedBy = b.CreatedBy,
                Notes = b.Notes,
                CreatedAt = b.CreatedAt
            }).ToList();

            var totalReceived = dtos.Where(d => d.Direction.Equals("In", StringComparison.OrdinalIgnoreCase)).Sum(d => d.Amount);
            var totalSent = dtos.Where(d => d.Direction.Equals("Out", StringComparison.OrdinalIgnoreCase)).Sum(d => d.Amount);
            var balance = totalReceived - totalSent;

            var bankBalances = dtos.GroupBy(d => d.BankName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Where(x => x.Direction.Equals("In", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount)
                       - g.Where(x => x.Direction.Equals("Out", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount)
                );

            return Ok(ApiResponse<BankSummaryResponseDto>.Ok(new BankSummaryResponseDto
            {
                CurrentBalance = balance,
                TotalReceived = totalReceived,
                TotalSent = totalSent,
                TransactionsCount = dtos.Count,
                Transactions = dtos,
                BankBalances = bankBalances
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bank transactions.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب المعاملات البنكية."));
        }
    }

    [HttpPost("bank")]
    [ProducesResponseType(typeof(ApiResponse<BankTransactionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateBankTransaction([FromBody] CreateBankTransactionDto dto)
    {
        try
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (!AppRoles.IsFinanceDirector(userRole))
            {
                return StatusCode(StatusCodes.Status403Forbidden, 
                    ApiResponse.Fail("غير مصرح: إضافة المعاملات البنكية مقتصر حصرياً على الإدارة المالية والمدير المالي."));
            }

            if (dto.Amount <= 0)
            {
                return BadRequest(ApiResponse.Fail("مبلغ المعاملة يجب أن يكون قيمة رقمية موجبة أكبر من الصفر."));
            }

            if (string.IsNullOrWhiteSpace(dto.BankName))
            {
                return BadRequest(ApiResponse.Fail("اسم البنك مطلوب."));
            }

            if (string.IsNullOrWhiteSpace(dto.TransactionType))
            {
                return BadRequest(ApiResponse.Fail("نوع المعاملة البنكية مطلوب."));
            }

            var dir = !string.IsNullOrWhiteSpace(dto.Direction) && (dto.Direction.Equals("Out", StringComparison.OrdinalIgnoreCase) || dto.Direction.Contains("صرف") || dto.Direction.Contains("Sent") || dto.Direction.Contains("سحب")) ? "Out" : "In";
            var callerName = User.FindFirst(ClaimTypes.Name)?.Value 
                ?? User.FindFirst("fullName")?.Value 
                ?? "مدير الإدارة المالية";

            var record = new BankTransaction
            {
                TransactionDate = dto.TransactionDate == default ? DateTime.UtcNow : dto.TransactionDate,
                TransactionType = dto.TransactionType.Trim(),
                Direction = dir,
                Amount = Math.Round(dto.Amount, 2),
                BankName = dto.BankName.Trim(),
                ReferenceNumber = dto.ReferenceNumber?.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description) ? $"{dto.TransactionType} في {dto.BankName}" : dto.Description.Trim(),
                CreatedBy = callerName,
                Notes = dto.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _context.BankTransactions.AddAsync(record);
            await _context.SaveChangesAsync();

            var result = new BankTransactionDto
            {
                Id = record.Id,
                TransactionDate = record.TransactionDate,
                TransactionType = record.TransactionType,
                Direction = record.Direction,
                Amount = record.Amount,
                BankName = record.BankName,
                ReferenceNumber = record.ReferenceNumber,
                Description = record.Description,
                CreatedBy = record.CreatedBy,
                Notes = record.Notes,
                CreatedAt = record.CreatedAt
            };

            return StatusCode(StatusCodes.Status201Created, ApiResponse<BankTransactionDto>.Ok(result, "تم حفظ المعاملة البنكية بنجاح."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding bank transaction.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ المعاملة البنكية."));
        }
    }

    // ========================================================================
    // 4. المصاريف — EXPENSES (/api/finance/expenses)
    // ========================================================================
    [HttpGet("expenses")]
    [ProducesResponseType(typeof(ApiResponse<ExpensesSummaryResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? category)
    {
        try
        {
            var allExpenses = await _context.Expenses.AsNoTracking()
                .OrderByDescending(e => e.ExpenseDate)
                .ToListAsync();

            var totalAll = allExpenses.Sum(e => e.Amount);
            var today = DateTime.UtcNow.Date;
            var todayExpenses = allExpenses.Where(e => e.ExpenseDate.Date == today).Sum(e => e.Amount);

            var filtered = allExpenses.AsEnumerable();
            if (fromDate.HasValue) filtered = filtered.Where(e => e.ExpenseDate.Date >= fromDate.Value.Date);
            if (toDate.HasValue) filtered = filtered.Where(e => e.ExpenseDate.Date <= toDate.Value.Date);
            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(e => e.Category.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filtered.ToList();
            var rangeExpenses = filteredList.Sum(e => e.Amount);

            var byCat = filteredList.GroupBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

            var dtos = filteredList.Select(e => new ExpenseDto
            {
                Id = e.Id,
                ExpenseDate = e.ExpenseDate,
                Category = e.Category,
                Amount = e.Amount,
                Title = e.Title,
                Beneficiary = e.Beneficiary,
                ReferenceNumber = e.ReferenceNumber,
                PaymentMethod = e.PaymentMethod,
                CreatedBy = e.CreatedBy,
                Notes = e.Notes,
                CreatedAt = e.CreatedAt
            }).ToList();

            var catBreakdown = byCat.Select(kv => new ExpenseCategoryBreakdownDto
            {
                Category = kv.Key,
                TotalAmount = kv.Value,
                Count = filteredList.Count(e => e.Category.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)),
                Percentage = totalAll > 0 ? Math.Round((kv.Value / totalAll) * 100, 2) : 0
            }).ToList();

            return Ok(ApiResponse<ExpensesSummaryResponseDto>.Ok(new ExpensesSummaryResponseDto
            {
                TotalExpenses = totalAll,
                TodayExpenses = todayExpenses,
                RangeExpenses = rangeExpenses,
                ExpensesCount = dtos.Count,
                ExpensesByCategory = byCat,
                CategoryBreakdown = catBreakdown,
                Expenses = dtos
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب بيانات المصاريف."));
        }
    }

    [HttpPost("expenses")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseDto dto)
    {
        try
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (!AppRoles.IsFinanceDirector(userRole))
            {
                return StatusCode(StatusCodes.Status403Forbidden, 
                    ApiResponse.Fail("غير مصرح: إضافة المصاريف مقتصر حصرياً على الإدارة المالية والمدير المالي."));
            }

            if (dto.Amount <= 0)
            {
                return BadRequest(ApiResponse.Fail("مبلغ المصروف يجب أن يكون قيمة رقمية موجبة أكبر من الصفر."));
            }

            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return BadRequest(ApiResponse.Fail("بيان المصروف مطلوب."));
            }

            if (string.IsNullOrWhiteSpace(dto.Beneficiary))
            {
                return BadRequest(ApiResponse.Fail("المستفيد / الجهة مطلوبة."));
            }

            var callerName = User.FindFirst(ClaimTypes.Name)?.Value 
                ?? User.FindFirst("fullName")?.Value 
                ?? "مدير الإدارة المالية";

            var expense = new Expense
            {
                ExpenseDate = dto.ExpenseDate == default ? DateTime.UtcNow : dto.ExpenseDate,
                Category = string.IsNullOrWhiteSpace(dto.Category) ? "أخرى" : dto.Category.Trim(),
                Amount = Math.Round(dto.Amount, 2),
                Title = dto.Title.Trim(),
                Beneficiary = dto.Beneficiary.Trim(),
                ReferenceNumber = dto.ReferenceNumber?.Trim(),
                PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Bank Transfer" : dto.PaymentMethod.Trim(),
                CreatedBy = callerName,
                Notes = dto.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _context.Expenses.AddAsync(expense);
            await _context.SaveChangesAsync();

            var result = new ExpenseDto
            {
                Id = expense.Id,
                ExpenseDate = expense.ExpenseDate,
                Category = expense.Category,
                Amount = expense.Amount,
                Title = expense.Title,
                Beneficiary = expense.Beneficiary,
                ReferenceNumber = expense.ReferenceNumber,
                PaymentMethod = expense.PaymentMethod,
                CreatedBy = expense.CreatedBy,
                Notes = expense.Notes,
                CreatedAt = expense.CreatedAt
            };

            return StatusCode(StatusCodes.Status201Created, ApiResponse<ExpenseDto>.Ok(result, "تم تسجيل المصروف بنجاح."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تسجيل المصروف."));
        }
    }

    // ========================================================================
    // 5. الموظفين — EMPLOYEES FINANCE (/api/finance/employees)
    // ========================================================================
    [HttpGet("employees")]
    [ProducesResponseType(typeof(ApiResponse<List<FinanceEmployeeSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployeesFinance()
    {
        try
        {
            var users = await _context.Users.AsNoTracking()
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var finRecords = await _context.EmployeeFinancialRecords.AsNoTracking()
                .ToDictionaryAsync(r => r.UserId);

            var lendings = await _context.EmployeeLendings.AsNoTracking().ToListAsync();
            var attendance = await _context.AttendanceRecords.AsNoTracking().ToListAsync();

            var result = new List<FinanceEmployeeSummaryDto>();

            foreach (var u in users)
            {
                finRecords.TryGetValue(u.Id, out var fin);
                var uLendings = lendings.Where(l => l.UserId == u.Id).ToList();
                var uAtt = attendance.Where(a => a.UserId == u.Id).ToList();

                var totLending = uLendings.Sum(l => l.LendingAmount);
                var paidLending = uLendings.Sum(l => l.PaidAmount);
                var remLending = uLendings.Sum(l => l.RemainingAmount);

                var daysPresent = uAtt.Count(a => a.Status.Equals("Present", StringComparison.OrdinalIgnoreCase));
                var daysAbsent = uAtt.Count(a => a.Status.Equals("Absent", StringComparison.OrdinalIgnoreCase));
                var totDays = daysPresent + daysAbsent;
                decimal? attPct = totDays > 0 ? Math.Round((decimal)daysPresent / totDays * 100, 1) : null;

                var dept = u.Role switch
                {
                    "TRANSPORTATION_MANAGER" or "Transportation Logistics Manager" => "إدارة النقل واللوجستيات",
                    "STORAGE_MANAGER" or "Storage & Factory Operations Manager" => "إدارة المخازن والمستودعات",
                    "FACTORY_MANAGER" or "Factory Manager" => "إدارة المصنع والتشغيل",
                    "FINANCE_DIRECTOR" or "Accountant" => "الإدارة المالية والمحاسبة",
                    "CEO" => "الإدارة العليا",
                    "IT" => "تكنولوجيا المعلومات",
                    _ => "شؤون العمليات والمواقع"
                };

                result.Add(new FinanceEmployeeSummaryDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Username = u.Username,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    Role = u.Role,
                    Department = dept,
                    BasicSalary = fin?.BasicSalary ?? 0m,
                    NetSalary = fin?.NetSalary ?? 0m,
                    HasSalaryRecord = fin != null,
                    TotalLending = totLending,
                    PaidLending = paidLending,
                    RemainingLending = remLending,
                    DaysPresent = daysPresent,
                    DaysAbsent = daysAbsent,
                    AttendancePercentage = attPct,
                    FinancialStatus = remLending > 0 ? "توجد سلفة مستحقة" : "منتظم"
                });
            }

            return Ok(ApiResponse<List<FinanceEmployeeSummaryDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employee financial summaries.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب بيانات الموظفين المالية."));
        }
    }

    [HttpGet("employees/{id}")]
    [ProducesResponseType(typeof(ApiResponse<FinanceEmployeeDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmployeeFinanceDetail(string id)
    {
        try
        {
            UserAccount? user = null;
            if (Guid.TryParse(id, out var userGuid))
            {
                user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userGuid);
            }
            if (user == null)
            {
                user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == id);
            }

            if (user == null)
            {
                return NotFound(ApiResponse.Fail($"الموظف '{id}' غير موجود."));
            }

            var fin = await _context.EmployeeFinancialRecords.AsNoTracking().FirstOrDefaultAsync(f => f.UserId == user.Id);
            var lendings = await _context.EmployeeLendings.AsNoTracking()
                .Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.RequestDate)
                .ToListAsync();

            var attendance = await _context.AttendanceRecords.AsNoTracking()
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.Date)
                .Take(30)
                .ToListAsync();

            var totLending = lendings.Sum(l => l.LendingAmount);
            var paidLending = lendings.Sum(l => l.PaidAmount);
            var remLending = lendings.Sum(l => l.RemainingAmount);

            var daysPresent = attendance.Count(a => a.Status.Equals("Present", StringComparison.OrdinalIgnoreCase));
            var daysAbsent = attendance.Count(a => a.Status.Equals("Absent", StringComparison.OrdinalIgnoreCase));
            var totDays = daysPresent + daysAbsent;
            decimal? attPct = totDays > 0 ? Math.Round((decimal)daysPresent / totDays * 100, 1) : null;

            var dept = user.Role switch
            {
                "TRANSPORTATION_MANAGER" or "Transportation Logistics Manager" => "إدارة النقل واللوجستيات",
                "STORAGE_MANAGER" or "Storage & Factory Operations Manager" => "إدارة المخازن والمستودعات",
                "FACTORY_MANAGER" or "Factory Manager" => "إدارة المصنع والتشغيل",
                "FINANCE_DIRECTOR" or "Accountant" => "الإدارة المالية والمحاسبة",
                "CEO" => "الإدارة العليا",
                "IT" => "تكنولوجيا المعلومات",
                _ => "شؤون العمليات والمواقع"
            };

            var lendingDtos = lendings.Select(l => new EmployeeLendingDto
            {
                Id = l.Id,
                LendingAmount = l.LendingAmount,
                PaidAmount = l.PaidAmount,
                RemainingAmount = l.RemainingAmount,
                RequestDate = l.RequestDate,
                ApprovalStatus = l.ApprovalStatus,
                ApprovedBy = l.ApprovedBy,
                Notes = l.Notes
            }).ToList();

            var attendanceDtos = attendance.Select(a => new AttendanceRecordDto
            {
                Id = a.Id,
                Date = a.Date,
                Status = a.Status,
                CheckInTime = a.CheckInTime,
                CheckOutTime = a.CheckOutTime,
                Notes = a.Notes
            }).ToList();

            return Ok(ApiResponse<FinanceEmployeeDetailDto>.Ok(new FinanceEmployeeDetailDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Username = user.Username,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                Department = dept,
                BasicSalary = fin?.BasicSalary ?? 0m,
                Allowances = fin?.Allowances ?? 0m,
                InsuranceDeductions = fin?.InsuranceDeductions ?? 0m,
                NetSalary = fin?.NetSalary ?? 0m,
                HasSalaryRecord = fin != null,
                PaymentMethod = fin?.PaymentMethod ?? "تحويل بنكي",
                BankAccountNumber = fin?.BankAccountNumber,
                TotalLending = totLending,
                PaidLending = paidLending,
                RemainingLending = remLending,
                DaysPresent = daysPresent,
                DaysAbsent = daysAbsent,
                AttendancePercentage = attPct,
                FinancialStatus = remLending > 0 ? "توجد سلفة مستحقة" : "منتظم",
                Lendings = lendingDtos,
                RecentAttendance = attendanceDtos
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employee financial detail for: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب تفاصيل الموظف المالية."));
        }
    }
}
