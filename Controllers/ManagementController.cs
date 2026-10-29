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
[Authorize(Policy = "ManagementAccess")]
public class ManagementController : ControllerBase
{
    private readonly FaceStoneDbContext _context;
    private readonly ILogger<ManagementController> _logger;

    public ManagementController(FaceStoneDbContext context, ILogger<ManagementController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ========================================================================
    // 1. OVERVIEW: Summary for 5 Main Dashboard Cards
    // ========================================================================
    [HttpGet("overview")]
    [ProducesResponseType(typeof(ApiResponse<ManagementOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview()
    {
        try
        {
            // 1. Transportation
            var trans = await _context.Transportations.AsNoTracking().Where(t => t.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(t.ApprovalStatus)).ToListAsync();
            var tripsCount = trans.Count;
            var transReceived = trans.Sum(t => t.NetWeight);
            var transWaste = trans.Sum(t => t.WasteQuantity);

            // 2. Storage
            var storage = await _context.StorageTransactions.AsNoTracking().Where(s => s.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(s.ApprovalStatus)).ToListAsync();
            var storageDeposit = storage.Sum(s => s.IncomingQuantity);
            var storageIssue = storage.Sum(s => s.OutgoingQuantity);
            var currentBalance = storage.Count > 0 
                ? storage.OrderByDescending(s => s.TransactionDate).First().ClosingBalance 
                : (storageDeposit - storageIssue);

            // 3. Finance & POs
            var pos = await _context.PurchaseOrders.AsNoTracking().ToListAsync();
            var expenses = await _context.Expenses.AsNoTracking().ToListAsync();

            var totalExpenses = expenses.Sum(e => e.Amount);
            var totalPoVal = pos.Sum(p => p.TotalValue > 0 ? p.TotalValue : (p.Quantity * (p.TonPrice > 0 ? p.TonPrice : p.PricePerTon)));
            var totalPoQty = pos.Sum(p => p.Quantity);

            // Calculate delivered value across POs based on linked transportation net weight
            var poDeliveredQty = 0m;
            var poDeliveredVal = 0m;
            foreach (var po in pos)
            {
                var poTrans = trans.Where(t => t.PONumber.Equals(po.PONumber, StringComparison.OrdinalIgnoreCase)).ToList();
                var dQty = poTrans.Sum(t => t.NetWeight);
                var price = po.TonPrice > 0 ? po.TonPrice : po.PricePerTon;
                poDeliveredQty += dQty;
                poDeliveredVal += Math.Round(dQty * price, 2);
            }
            var perfPct = totalPoQty > 0 ? Math.Round((poDeliveredQty / totalPoQty) * 100, 1) : 0m;

            // 4. Employees
            var users = await _context.Users.AsNoTracking().ToListAsync();
            var finRecords = await _context.EmployeeFinancialRecords.AsNoTracking().ToListAsync();
            var lendings = await _context.EmployeeLendings.AsNoTracking().ToListAsync();

            var totEmployees = users.Count;
            var salariedCount = finRecords.Count;
            var totSalaries = finRecords.Sum(f => f.NetSalary);
            var totRemLending = lendings.Sum(l => l.RemainingAmount);
            var activeLendingsCount = lendings.Count(l => l.RemainingAmount > 0);

            // 5. Suppliers
            var supplierGroups = pos.GroupBy(p => p.Supplier.Trim(), StringComparer.OrdinalIgnoreCase).ToList();
            var totSuppliers = supplierGroups.Count;
            var totPosCount = pos.Count;
            var totRemQty = pos.Sum(p => p.RemainingQuantity);

            var overview = new ManagementOverviewDto
            {
                Transportation = new ManagementOverviewCardTransportationDto
                {
                    TripsCount = tripsCount,
                    TotalReceivedQuantity = transReceived,
                    TotalWasteQuantity = transWaste
                },
                Storage = new ManagementOverviewCardStorageDto
                {
                    TotalDeposit = storageDeposit,
                    TotalIssue = storageIssue,
                    CurrentBalance = currentBalance
                },
                Finance = new ManagementOverviewCardFinanceDto
                {
                    TotalExpenses = totalExpenses,
                    TotalPoValue = totalPoVal,
                    DeliveredValue = poDeliveredVal,
                    PerformancePercentage = perfPct
                },
                Employees = new ManagementOverviewCardEmployeesDto
                {
                    TotalEmployees = totEmployees,
                    SalariedEmployeesCount = salariedCount,
                    TotalSalariesRecorded = totSalaries,
                    TotalRemainingLending = totRemLending,
                    ActiveLendingsCount = activeLendingsCount
                },
                Suppliers = new ManagementOverviewCardSuppliersDto
                {
                    TotalSuppliers = totSuppliers,
                    TotalPos = totPosCount,
                    TotalRemainingQuantity = totRemQty
                }
            };

            return Ok(ApiResponse<ManagementOverviewDto>.Ok(overview));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating management overview.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تحميل المؤشرات العامة للوحة الإدارة."));
        }
    }

    // ========================================================================
    // 2. GLOBAL SEARCH: Search Across All Management Content (Grouped Results)
    // ========================================================================
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<ManagementSearchResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? q)
    {
        try
        {
            var query = (q ?? string.Empty).Trim();
            var response = new ManagementSearchResponseDto
            {
                Query = query
            };

            if (string.IsNullOrWhiteSpace(query))
            {
                return Ok(ApiResponse<ManagementSearchResponseDto>.Ok(response));
            }

            var s = query.ToLowerInvariant();
            var isNumeric = decimal.TryParse(query, out var numVal);

            // 1. PURCHASE ORDERS
            var poMatches = await _context.PurchaseOrders.AsNoTracking()
                .Where(p =>
                    p.PONumber.ToLower().Contains(s) ||
                    p.Supplier.ToLower().Contains(s) ||
                    (p.InvoiceNumber != null && p.InvoiceNumber.ToLower().Contains(s)) ||
                    (p.Notes != null && p.Notes.ToLower().Contains(s)) ||
                    (isNumeric && (p.Quantity == numVal || p.TotalValue == numVal || p.TonPrice == numVal))
                )
                .OrderByDescending(p => p.OrderDate)
                .Take(10)
                .ToListAsync();

            response.PurchaseOrders = poMatches.Select(p => new ManagementSearchItemDto
            {
                Id = p.PONumber,
                Category = "purchaseOrders",
                Title = p.PONumber,
                Subtitle = $"{p.Supplier} • {p.Quantity:N3} طن",
                Details = $"القيمة: {p.TotalValue:N2} ج.م | المتبقي: {p.RemainingQuantity:N3} طن | سعر الطن: {p.TonPrice:N2} ج.م",
                Badge = p.PaymentStatus,
                Route = $"/management/finance?po={Uri.EscapeDataString(p.PONumber)}",
                ActionLabel = "عرض أمر الشراء",
                Date = p.OrderDate
            }).ToList();

            // 2. TRANSPORTATION
            var transMatches = await _context.Transportations.AsNoTracking()
                .Where(t => 
                    t.TransactionNumber.ToString().Contains(query) ||
                    t.PONumber.ToLower().Contains(s) ||
                    t.CarNumber.ToLower().Contains(s) ||
                    t.DriverName.ToLower().Contains(s) ||
                    t.MaterialType.ToLower().Contains(s) ||
                    t.CompanyName.ToLower().Contains(s) ||
                    t.Source.ToLower().Contains(s) ||
                    t.Destination.ToLower().Contains(s) ||
                    (t.InternalWeighbridgeCode != null && t.InternalWeighbridgeCode.ToLower().Contains(s)) ||
                    (t.ExternalWeighbridgeCode != null && t.ExternalWeighbridgeCode.ToLower().Contains(s)) ||
                    (t.Notes != null && t.Notes.ToLower().Contains(s)) ||
                    (isNumeric && (t.NetWeight == numVal || t.ExpectedWeight == numVal || t.InternalWeight == numVal || t.ExternalNetWeight == numVal))
                )
                .OrderByDescending(t => t.TransactionDate)
                .Take(10)
                .ToListAsync();

            response.Transportation = transMatches.Select(t => new ManagementSearchItemDto
            {
                Id = t.TransactionNumber.ToString(),
                Category = "transportation",
                Title = string.IsNullOrWhiteSpace(t.PONumber) ? $"حركة نقل #{t.TransactionNumber}" : $"{t.PONumber} (حركة #{t.TransactionNumber})",
                Subtitle = $"{t.MaterialType} • {t.NetWeight:N3} طن",
                Details = $"سيارة: {t.CarNumber} | سائق: {t.DriverName} | وجهة: {t.Destination}" + 
                          (!string.IsNullOrWhiteSpace(t.InternalWeighbridgeCode) ? $" | ميزان داخلي: {t.InternalWeighbridgeCode}" : "") +
                          (!string.IsNullOrWhiteSpace(t.ExternalWeighbridgeCode) ? $" | ميزان خارجي: {t.ExternalWeighbridgeCode}" : ""),
                Badge = t.ReceivingStatus ?? "مستلم",
                Route = $"/management/transportation?search={Uri.EscapeDataString(t.TransactionNumber.ToString())}",
                ActionLabel = "عرض النقل",
                Date = t.TransactionDate
            }).ToList();

            // 3. STORAGE
            var storageMatches = await _context.StorageTransactions.AsNoTracking()
                .Where(st =>
                    st.MovementNumber.ToString().Contains(query) ||
                    st.ReferenceNumber.ToString().Contains(query) ||
                    (st.PONumber != null && st.PONumber.ToLower().Contains(s)) ||
                    st.Material.ToLower().Contains(s) ||
                    st.Action.ToLower().Contains(s) ||
                    st.ServiceType.ToLower().Contains(s) ||
                    st.StorageLocation.ToLower().Contains(s) ||
                    st.ReceivingEmployee.ToLower().Contains(s) ||
                    (st.InternalWeighbridgeReference != null && st.InternalWeighbridgeReference.ToLower().Contains(s)) ||
                    (st.ExternalWeighbridgeReference != null && st.ExternalWeighbridgeReference.ToLower().Contains(s)) ||
                    (st.Notes != null && st.Notes.ToLower().Contains(s)) ||
                    (isNumeric && (st.IncomingQuantity == numVal || st.OutgoingQuantity == numVal || st.ClosingBalance == numVal))
                )
                .OrderByDescending(st => st.TransactionDate)
                .Take(10)
                .ToListAsync();

            response.Storage = storageMatches.Select(st => new ManagementSearchItemDto
            {
                Id = st.MovementNumber.ToString(),
                Category = "storage",
                Title = $"حركة مخزن #{st.MovementNumber} (مرجع: {st.ReferenceNumber})",
                Subtitle = $"{st.Action} ({st.Material}) • {(st.IncomingQuantity > 0 ? st.IncomingQuantity : st.OutgoingQuantity):N3} طن",
                Details = $"موقع: {st.StorageLocation} | الرصيد: {st.ClosingBalance:N3} طن | مسؤول: {st.ReceivingEmployee}" +
                          (!string.IsNullOrWhiteSpace(st.InternalWeighbridgeReference) ? $" | ميزان: {st.InternalWeighbridgeReference}" : ""),
                Badge = st.ServiceType,
                Route = $"/management/storage?search={Uri.EscapeDataString(st.MovementNumber.ToString())}",
                ActionLabel = "عرض المخزن",
                Date = st.TransactionDate
            }).ToList();

            // 4. FINANCE (Expenses & Bank Transactions)
            var expenseMatches = await _context.Expenses.AsNoTracking()
                .Where(e =>
                    e.Category.ToLower().Contains(s) ||
                    e.Title.ToLower().Contains(s) ||
                    e.Beneficiary.ToLower().Contains(s) ||
                    e.CreatedBy.ToLower().Contains(s) ||
                    (isNumeric && e.Amount == numVal)
                )
                .OrderByDescending(e => e.ExpenseDate)
                .Take(5)
                .ToListAsync();

            var bankMatches = await _context.BankTransactions.AsNoTracking()
                .Where(b =>
                    b.Description.ToLower().Contains(s) ||
                    b.BankName.ToLower().Contains(s) ||
                    (b.ReferenceNumber != null && b.ReferenceNumber.ToLower().Contains(s)) ||
                    b.TransactionType.ToLower().Contains(s) ||
                    (isNumeric && b.Amount == numVal)
                )
                .OrderByDescending(b => b.TransactionDate)
                .Take(5)
                .ToListAsync();

            var financeItems = new List<ManagementSearchItemDto>();
            financeItems.AddRange(expenseMatches.Select(e => new ManagementSearchItemDto
            {
                Id = e.Id.ToString(),
                Category = "finance",
                Title = $"مصروف: {e.Category} ({e.Title})",
                Subtitle = $"{e.Amount:N2} ج.م • مستفيد: {e.Beneficiary}",
                Details = $"مسجل بواسطة: {e.CreatedBy} | طريقة الدفع: {e.PaymentMethod} | مرجع: {e.ReferenceNumber ?? "-"}",
                Badge = "مصروفات",
                Route = $"/management/finance?search={Uri.EscapeDataString(e.Category)}",
                ActionLabel = "عرض المالية",
                Date = e.ExpenseDate
            }));
            financeItems.AddRange(bankMatches.Select(b => new ManagementSearchItemDto
            {
                Id = b.Id.ToString(),
                Category = "finance",
                Title = $"معاملة بنكية: {b.BankName} - {b.Description}",
                Subtitle = $"{b.Amount:N2} ج.م • اتجاه: {(b.Direction == "In" ? "وارد" : "صادر")}",
                Details = $"نوع المعاملة: {b.TransactionType} | مرجع: {b.ReferenceNumber ?? "-"} | مسجل بواسطة: {b.CreatedBy}",
                Badge = b.Direction == "In" ? "وارد بنكي" : "صادر بنكي",
                Route = $"/management/finance?search={Uri.EscapeDataString(b.Description)}",
                ActionLabel = "عرض المالية",
                Date = b.TransactionDate
            }));
            response.Finance = financeItems;

            // 5. EMPLOYEES (Users)
            var userMatches = await _context.Users.AsNoTracking()
                .Where(u =>
                    u.FullName.ToLower().Contains(s) ||
                    u.Username.ToLower().Contains(s) ||
                    u.Email.ToLower().Contains(s) ||
                    u.PhoneNumber.ToLower().Contains(s) ||
                    u.Role.ToLower().Contains(s) ||
                    u.Id.ToString().ToLower().Contains(s)
                )
                .OrderBy(u => u.FullName)
                .Take(10)
                .ToListAsync();

            response.Employees = userMatches.Select(u => new ManagementSearchItemDto
            {
                Id = u.Id.ToString(),
                Category = "employees",
                Title = u.FullName,
                Subtitle = $"{GetDepartmentForRole(u.Role)} • {u.Role}",
                Details = $"اسم المستخدم: {u.Username} | هاتف: {u.PhoneNumber} | بريد: {u.Email}",
                Badge = u.Role,
                Route = $"/management/employees/{u.Id}",
                ActionLabel = "عرض الموظف",
                Date = u.CreatedAt
            }).ToList();

            // 6. SUPPLIERS
            var allPos = await _context.PurchaseOrders.AsNoTracking().ToListAsync();
            var supplierMatches = allPos
                .Where(p => p.Supplier.ToLower().Contains(s))
                .GroupBy(p => p.Supplier.Trim(), StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();

            response.Suppliers = supplierMatches.Select(g => {
                var posList = g.ToList();
                var sTotalQty = posList.Sum(p => p.Quantity);
                var sRemQty = posList.Sum(p => p.RemainingQuantity);
                var sDelivered = Math.Max(0, sTotalQty - sRemQty);
                var sProg = sTotalQty > 0 ? Math.Round((sDelivered / sTotalQty) * 100, 1) : 0m;
                return new ManagementSearchItemDto
                {
                    Id = g.Key,
                    Category = "suppliers",
                    Title = g.Key,
                    Subtitle = $"{posList.Count} أوامر شراء • إنجاز {sProg}%",
                    Details = $"المتعاقد: {sTotalQty:N3} طن | تم توريد: {sDelivered:N3} طن | المتبقي: {sRemQty:N3} طن",
                    Badge = $"{posList.Count} POs",
                    Route = $"/management/suppliers/{Uri.EscapeDataString(g.Key)}",
                    ActionLabel = "عرض المورد",
                    Date = posList.OrderByDescending(x => x.OrderDate).FirstOrDefault()?.OrderDate
                };
            }).ToList();

            response.TotalMatches = response.PurchaseOrders.Count +
                                    response.Transportation.Count + 
                                    response.Storage.Count + 
                                    response.Finance.Count + 
                                    response.Employees.Count + 
                                    response.Suppliers.Count;

            return Ok(ApiResponse<ManagementSearchResponseDto>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing management search for: {Q}", q);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء إجراء البحث في بيانات الإدارة."));
        }
    }

    // ========================================================================
    // 3. TRANSPORTATION MONITORING: Overview, KPIs & Real Transactions
    // ========================================================================
    [HttpGet("transportation")]
    [ProducesResponseType(typeof(ApiResponse<ManagementTransportationOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransportationMonitoring(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? search,
        [FromQuery] string? po,
        [FromQuery] string? driver,
        [FromQuery] string? material,
        [FromQuery] string? destination)
    {
        try
        {
            var query = _context.Transportations.AsNoTracking();

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                query = query.Where(t => t.TransactionDate >= fDate);
            }
            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(t => t.TransactionDate <= tDate);
            }
            if (!string.IsNullOrWhiteSpace(po))
            {
                var p = po.Trim().ToLowerInvariant();
                query = query.Where(t => t.PONumber.ToLower().Contains(p));
            }
            if (!string.IsNullOrWhiteSpace(driver))
            {
                var d = driver.Trim().ToLowerInvariant();
                query = query.Where(t => t.DriverName.ToLower().Contains(d));
            }
            if (!string.IsNullOrWhiteSpace(material))
            {
                var m = material.Trim().ToLowerInvariant();
                query = query.Where(t => t.MaterialType.ToLower().Contains(m));
            }
            if (!string.IsNullOrWhiteSpace(destination))
            {
                var dest = destination.Trim().ToLowerInvariant();
                query = query.Where(t => t.Destination.ToLower().Contains(dest));
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(t => 
                    t.TransactionNumber.ToString().Contains(s) ||
                    t.PONumber.ToLower().Contains(s) ||
                    t.CarNumber.ToLower().Contains(s) ||
                    t.DriverName.ToLower().Contains(s) ||
                    t.MaterialType.ToLower().Contains(s) ||
                    t.CompanyName.ToLower().Contains(s) ||
                    t.Destination.ToLower().Contains(s) ||
                    (t.InternalWeighbridgeCode != null && t.InternalWeighbridgeCode.ToLower().Contains(s)) ||
                    (t.ExternalWeighbridgeCode != null && t.ExternalWeighbridgeCode.ToLower().Contains(s))
                );
            }

            var records = await query.OrderByDescending(t => t.TransactionDate).ToListAsync();
            var approvedRecords = records.Where(t => t.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(t.ApprovalStatus)).ToList();

            var totalTrips = approvedRecords.Count;
            var totalQty = approvedRecords.Sum(t => t.ExpectedWeight > 0 ? t.ExpectedWeight : t.NetWeight);
            var netQty = approvedRecords.Sum(t => t.NetWeight);
            var totalWaste = approvedRecords.Sum(t => t.WasteQuantity);
            var distinctCars = approvedRecords.Select(t => t.CarNumber.Trim()).Where(c => !string.IsNullOrEmpty(c)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var distinctDrivers = approvedRecords.Select(t => t.DriverName.Trim()).Where(d => !string.IsNullOrEmpty(d)).Distinct(StringComparer.OrdinalIgnoreCase).Count();

            var completedShipments = approvedRecords.Count(t => 
                (t.ReceivingStatus ?? "").Contains("Approved", StringComparison.OrdinalIgnoreCase) ||
                (t.ReceivingStatus ?? "").Contains("Accepted", StringComparison.OrdinalIgnoreCase) ||
                (t.ReceivingStatus ?? "").Contains("مقبول", StringComparison.OrdinalIgnoreCase) ||
                (t.ReceivingStatus ?? "").Contains("معتمد", StringComparison.OrdinalIgnoreCase) ||
                (t.ReceivingStatus ?? "").Contains("safe", StringComparison.OrdinalIgnoreCase)
            );
            var currentShipments = totalTrips - completedShipments;

            var withDuration = approvedRecords.Where(t => t.ShippingDurationMinutes.HasValue && t.ShippingDurationMinutes.Value > 0).ToList();
            double? avgDuration = withDuration.Count > 0 ? Math.Round(withDuration.Average(t => t.ShippingDurationMinutes!.Value), 1) : null;

            var latest = records.FirstOrDefault();

            var items = records.Take(100).Select(t => new ManagementTransportationItemDto
            {
                Id = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                CarNumber = t.CarNumber,
                DriverName = t.DriverName,
                PONumber = t.PONumber,
                CompanyName = t.CompanyName,
                MaterialType = t.MaterialType,
                NetWeight = t.NetWeight,
                WasteQuantity = t.WasteQuantity,
                ReceivingStatus = t.ReceivingStatus ?? "مستلم",
                Destination = t.Destination,
                InternalWeighbridgeCode = t.InternalWeighbridgeCode,
                ExternalWeighbridgeCode = t.ExternalWeighbridgeCode,
                InternalWeight = t.InternalWeight,
                ExternalNetWeight = t.ExternalNetWeight,
                DepartureTime = t.DepartureTime,
                ArrivalTime = t.ArrivalTime,
                ShippingDurationMinutes = t.ShippingDurationMinutes,
                TransactionStatus = t.TransactionStatus,
                HasAttachments = !string.IsNullOrEmpty(t.InvoiceAttachmentPath) || !string.IsNullOrEmpty(t.WeighbridgeAttachmentPath),
                InvoiceFileName = t.InvoiceFileName,
                WeighbridgeFileName = t.WeighbridgeFileName,
                InvoiceAttachmentPath = t.InvoiceAttachmentPath,
                WeighbridgeAttachmentPath = t.WeighbridgeAttachmentPath,
                Notes = t.Notes,
                ApprovalStatus = string.IsNullOrWhiteSpace(t.ApprovalStatus) ? "APPROVED" : t.ApprovalStatus,
                RejectionReason = t.RejectionReason
            }).ToList();

            var result = new ManagementTransportationOverviewDto
            {
                TotalTrips = totalTrips,
                TotalQuantity = totalQty,
                NetQuantity = netQty,
                TotalWaste = totalWaste,
                CarsCount = distinctCars,
                DriversCount = distinctDrivers,
                CompletedShipmentsCount = completedShipments,
                CurrentShipmentsCount = currentShipments,
                AvgShippingDurationMinutes = avgDuration,
                LastMovementNumber = latest?.TransactionNumber,
                LastMovementDate = latest?.TransactionDate,
                Items = items,
                FilterFromDate = fromDate?.ToString("yyyy-MM-dd"),
                FilterToDate = toDate?.ToString("yyyy-MM-dd"),
                FilterPo = po,
                FilterDriver = driver,
                FilterMaterial = material,
                FilterDestination = destination
            };

            return Ok(ApiResponse<ManagementTransportationOverviewDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transportation monitoring data.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تحميل بيانات مراقبة النقل."));
        }
    }

    [HttpGet("transportation/{id}")]
    [ProducesResponseType(typeof(ApiResponse<ManagementTransportationItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransportationDetail(string id)
    {
        try
        {
            Transportation? t = null;
            if (int.TryParse(id, out var num))
            {
                t = await _context.Transportations.AsNoTracking().FirstOrDefaultAsync(x => x.TransactionNumber == num || x.Id == num);
            }
            if (t == null)
            {
                t = await _context.Transportations.AsNoTracking().FirstOrDefaultAsync(x => x.PONumber == id);
            }
            if (t == null)
            {
                return NotFound(ApiResponse.Fail($"حركة النقل '{id}' غير موجودة."));
            }

            var dto = new ManagementTransportationItemDto
            {
                Id = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                CarNumber = t.CarNumber,
                DriverName = t.DriverName,
                PONumber = t.PONumber,
                CompanyName = t.CompanyName,
                MaterialType = t.MaterialType,
                NetWeight = t.NetWeight,
                WasteQuantity = t.WasteQuantity,
                ReceivingStatus = t.ReceivingStatus ?? "مستلم",
                Destination = t.Destination,
                InternalWeighbridgeCode = t.InternalWeighbridgeCode,
                ExternalWeighbridgeCode = t.ExternalWeighbridgeCode,
                InternalWeight = t.InternalWeight,
                ExternalNetWeight = t.ExternalNetWeight,
                DepartureTime = t.DepartureTime,
                ArrivalTime = t.ArrivalTime,
                ShippingDurationMinutes = t.ShippingDurationMinutes,
                TransactionStatus = t.TransactionStatus,
                HasAttachments = !string.IsNullOrEmpty(t.InvoiceAttachmentPath) || !string.IsNullOrEmpty(t.WeighbridgeAttachmentPath),
                InvoiceFileName = t.InvoiceFileName,
                WeighbridgeFileName = t.WeighbridgeFileName,
                InvoiceAttachmentPath = t.InvoiceAttachmentPath,
                WeighbridgeAttachmentPath = t.WeighbridgeAttachmentPath,
                Notes = t.Notes
            };

            return Ok(ApiResponse<ManagementTransportationItemDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transportation details for: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تحميل تفاصيل حركة النقل."));
        }
    }

    // ========================================================================
    // 4. STORAGE MONITORING: Overview, KPIs & Real Transactions
    // ========================================================================
    [HttpGet("storage")]
    [ProducesResponseType(typeof(ApiResponse<ManagementStorageOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStorageMonitoring(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? search,
        [FromQuery] string? material,
        [FromQuery] string? action,
        [FromQuery] string? po)
    {
        try
        {
            var query = _context.StorageTransactions.AsNoTracking();

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                query = query.Where(s => s.TransactionDate >= fDate);
            }
            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(s => s.TransactionDate <= tDate);
            }
            if (!string.IsNullOrWhiteSpace(material))
            {
                var m = material.Trim().ToLowerInvariant();
                query = query.Where(st => st.Material.ToLower().Contains(m));
            }
            if (!string.IsNullOrWhiteSpace(action))
            {
                var a = action.Trim().ToLowerInvariant();
                query = query.Where(st => st.Action.ToLower() == a || st.ServiceType.ToLower().Contains(a));
            }
            if (!string.IsNullOrWhiteSpace(po))
            {
                var p = po.Trim().ToLowerInvariant();
                query = query.Where(st => st.PONumber != null && st.PONumber.ToLower().Contains(p));
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(st =>
                    st.MovementNumber.ToString().Contains(s) ||
                    st.ReferenceNumber.ToString().Contains(s) ||
                    (st.PONumber != null && st.PONumber.ToLower().Contains(s)) ||
                    st.Material.ToLower().Contains(s) ||
                    st.StorageLocation.ToLower().Contains(s) ||
                    st.ReceivingEmployee.ToLower().Contains(s) ||
                    (st.InternalWeighbridgeReference != null && st.InternalWeighbridgeReference.ToLower().Contains(s)) ||
                    (st.ExternalWeighbridgeReference != null && st.ExternalWeighbridgeReference.ToLower().Contains(s))
                );
            }

            var records = await query.OrderByDescending(s => s.TransactionDate).ToListAsync();

            var approvedRecords = records.Where(s => s.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(s.ApprovalStatus)).ToList();
            var totalDeposit = approvedRecords.Sum(s => s.IncomingQuantity);
            var totalIssue = approvedRecords.Sum(s => s.OutgoingQuantity);

            var latest = approvedRecords.FirstOrDefault();
            var currentBal = latest?.ClosingBalance ?? (totalDeposit - totalIssue);

            var totalWaste = approvedRecords
                .Where(s => s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue)
                .Sum(s => Math.Abs(s.InternalNetWeight - s.ExternalNetWeight!.Value));

            var poRelatedExports = approvedRecords.Where(s => s.IsExportRelatedToPO).ToList();
            var nonPoImports = approvedRecords.Where(s => s.Action.Equals("Import", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(s.PONumber)).ToList();

            var materialGroups = approvedRecords.GroupBy(s => s.Material.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new ManagementStorageMaterialBreakdownDto
                {
                    Material = g.Key,
                    IncomingQuantity = g.Sum(x => x.IncomingQuantity),
                    OutgoingQuantity = g.Sum(x => x.OutgoingQuantity),
                    TransactionsCount = g.Count()
                }).ToList();

            var items = records.Take(100).Select(s => new ManagementStorageItemDto
            {
                Id = s.Id,
                MovementNumber = s.MovementNumber,
                ReferenceNumber = s.ReferenceNumber,
                TransactionDate = s.TransactionDate,
                Action = s.Action,
                ServiceType = s.ServiceType,
                Material = s.Material,
                PONumber = s.PONumber,
                IncomingQuantity = s.IncomingQuantity,
                OutgoingQuantity = s.OutgoingQuantity,
                ClosingBalance = s.ClosingBalance,
                StorageLocation = s.StorageLocation,
                ReceivingEmployee = s.ReceivingEmployee,
                IsExportRelatedToPO = s.IsExportRelatedToPO,
                InternalWeighbridgeReference = s.InternalWeighbridgeReference,
                ExternalWeighbridgeReference = s.ExternalWeighbridgeReference,
                InternalNetWeight = s.InternalNetWeight,
                ExternalNetWeight = s.ExternalNetWeight,
                HasAttachments = !string.IsNullOrEmpty(s.WeighbridgeAttachmentPath),
                WeighbridgeFileName = s.WeighbridgeFileName,
                WeighbridgeAttachmentPath = s.WeighbridgeAttachmentPath,
                Notes = s.Notes,
                ApprovalStatus = string.IsNullOrWhiteSpace(s.ApprovalStatus) ? "APPROVED" : s.ApprovalStatus,
                RejectionReason = s.RejectionReason
            }).ToList();

            var result = new ManagementStorageOverviewDto
            {
                TotalDeposit = totalDeposit,
                TotalIssue = totalIssue,
                CurrentBalance = currentBal,
                TotalWaste = totalWaste,
                PoRelatedExportsCount = poRelatedExports.Count,
                PoRelatedExportsQuantity = poRelatedExports.Sum(x => x.OutgoingQuantity),
                NonPoImportsCount = nonPoImports.Count,
                NonPoImportsQuantity = nonPoImports.Sum(x => x.IncomingQuantity),
                TransactionsCount = records.Count,
                MaterialBreakdown = materialGroups,
                Items = items,
                FilterFromDate = fromDate?.ToString("yyyy-MM-dd"),
                FilterToDate = toDate?.ToString("yyyy-MM-dd"),
                FilterMaterial = material,
                FilterAction = action,
                FilterPo = po
            };

            return Ok(ApiResponse<ManagementStorageOverviewDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving storage monitoring data.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تحميل بيانات مراقبة المخازن."));
        }
    }

    [HttpGet("storage/{id}")]
    [ProducesResponseType(typeof(ApiResponse<ManagementStorageItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStorageDetail(string id)
    {
        try
        {
            StorageTransaction? s = null;
            if (int.TryParse(id, out var num))
            {
                s = await _context.StorageTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.MovementNumber == num || x.ReferenceNumber == num || x.Id == num);
            }
            if (s == null)
            {
                s = await _context.StorageTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.PONumber == id);
            }
            if (s == null)
            {
                return NotFound(ApiResponse.Fail($"حركة المخزن '{id}' غير موجودة."));
            }

            var dto = new ManagementStorageItemDto
            {
                Id = s.Id,
                MovementNumber = s.MovementNumber,
                ReferenceNumber = s.ReferenceNumber,
                TransactionDate = s.TransactionDate,
                Action = s.Action,
                ServiceType = s.ServiceType,
                Material = s.Material,
                PONumber = s.PONumber,
                IncomingQuantity = s.IncomingQuantity,
                OutgoingQuantity = s.OutgoingQuantity,
                ClosingBalance = s.ClosingBalance,
                StorageLocation = s.StorageLocation,
                ReceivingEmployee = s.ReceivingEmployee,
                IsExportRelatedToPO = s.IsExportRelatedToPO,
                InternalWeighbridgeReference = s.InternalWeighbridgeReference,
                ExternalWeighbridgeReference = s.ExternalWeighbridgeReference,
                InternalNetWeight = s.InternalNetWeight,
                ExternalNetWeight = s.ExternalNetWeight,
                HasAttachments = !string.IsNullOrEmpty(s.WeighbridgeAttachmentPath),
                WeighbridgeFileName = s.WeighbridgeFileName,
                WeighbridgeAttachmentPath = s.WeighbridgeAttachmentPath,
                Notes = s.Notes
            };

            return Ok(ApiResponse<ManagementStorageItemDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving storage details for: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تحميل تفاصيل حركة المخزن."));
        }
    }

    // ========================================================================
    // 5. FINANCE MONITORING: Overview, KPIs, Expenses & PO Statuses
    // ========================================================================
    [HttpGet("finance")]
    [ProducesResponseType(typeof(ApiResponse<ManagementFinanceOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFinanceMonitoring(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? search,
        [FromQuery] string? po,
        [FromQuery] string? paymentStatus)
    {
        try
        {
            var poQuery = _context.PurchaseOrders.AsNoTracking();

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                poQuery = poQuery.Where(p => p.OrderDate >= fDate);
            }
            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                poQuery = poQuery.Where(p => p.OrderDate <= tDate);
            }
            if (!string.IsNullOrWhiteSpace(po))
            {
                var p = po.Trim().ToLowerInvariant();
                poQuery = poQuery.Where(x => x.PONumber.ToLower().Contains(p));
            }
            if (!string.IsNullOrWhiteSpace(paymentStatus))
            {
                var ps = paymentStatus.Trim().ToLowerInvariant();
                poQuery = poQuery.Where(x => x.PaymentStatus.ToLower() == ps);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                poQuery = poQuery.Where(p => 
                    p.PONumber.ToLower().Contains(s) ||
                    p.Supplier.ToLower().Contains(s) ||
                    (p.InvoiceNumber != null && p.InvoiceNumber.ToLower().Contains(s))
                );
            }

            var pos = await poQuery.OrderByDescending(p => p.OrderDate).ToListAsync();
            var trans = await _context.Transportations.AsNoTracking().ToListAsync();
            var expenses = await _context.Expenses.AsNoTracking().ToListAsync();
            var bankTxs = await _context.BankTransactions.AsNoTracking().ToListAsync();

            var totalExpenses = expenses.Sum(e => e.Amount);
            var bankBalance = bankTxs.Where(b => b.Direction.Equals("In", StringComparison.OrdinalIgnoreCase)).Sum(b => b.Amount)
                            - bankTxs.Where(b => b.Direction.Equals("Out", StringComparison.OrdinalIgnoreCase)).Sum(b => b.Amount);

            var totalPoVal = 0m;
            var totalDeliveredVal = 0m;
            var totalRemVal = 0m;
            var totalWasteVal = 0m;
            var totalDeliveredQty = 0m;
            var totalOrderedQty = pos.Sum(p => p.Quantity);

            var statusCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "غير مدفوع", 0 },
                { "مدفوع جزئياً", 0 },
                { "مدفوع بالكامل", 0 }
            };

            var items = new List<ManagementFinancePoItemDto>();

            foreach (var p in pos)
            {
                var pTrans = trans.Where(t => t.PONumber.Equals(p.PONumber, StringComparison.OrdinalIgnoreCase)).ToList();
                var dQty = pTrans.Sum(t => t.NetWeight);
                var wQty = pTrans.Sum(t => t.WasteQuantity);
                var price = p.TonPrice > 0 ? p.TonPrice : p.PricePerTon;

                var poVal = p.TotalValue > 0 ? p.TotalValue : Math.Round(p.Quantity * price, 2);
                var dVal = Math.Round(dQty * price, 2);
                var rVal = Math.Round(p.RemainingQuantity * price, 2);
                var wVal = Math.Round(wQty * price, 2);

                totalPoVal += poVal;
                totalDeliveredVal += dVal;
                totalRemVal += rVal;
                totalWasteVal += wVal;
                totalDeliveredQty += dQty;

                var st = string.IsNullOrWhiteSpace(p.PaymentStatus) ? "غير مدفوع" : p.PaymentStatus.Trim();
                if (statusCounts.ContainsKey(st))
                {
                    statusCounts[st]++;
                }
                else
                {
                    statusCounts[st] = 1;
                }

                var progPct = p.Quantity > 0 ? Math.Round((dQty / p.Quantity) * 100, 1) : 0m;

                items.Add(new ManagementFinancePoItemDto
                {
                    PoNumber = p.PONumber,
                    Supplier = p.Supplier,
                    OrderDate = p.OrderDate,
                    Quantity = p.Quantity,
                    TonPrice = price,
                    TotalValue = poVal,
                    DeliveredQuantity = dQty,
                    DeliveredValue = dVal,
                    RemainingQuantity = p.RemainingQuantity,
                    RemainingValue = rVal,
                    WasteQuantity = wQty,
                    WasteValue = wVal,
                    PaymentStatus = st,
                    InvoiceNumber = p.InvoiceNumber,
                    ProgressPercentage = progPct
                });
            }

            var perfPct = totalOrderedQty > 0 ? Math.Round((totalDeliveredQty / totalOrderedQty) * 100, 1) : 0m;

            var recentExpenses = expenses.OrderByDescending(e => e.ExpenseDate).Take(30).Select(e => new ManagementExpenseItemDto
            {
                Id = e.Id,
                Date = e.ExpenseDate,
                Category = e.Category,
                Amount = e.Amount,
                Description = $"{e.Title} (مستفيد: {e.Beneficiary})",
                SpentBy = e.CreatedBy
            }).ToList();

            var recentBank = bankTxs.OrderByDescending(b => b.TransactionDate).Take(30).Select(b => new ManagementBankTransactionItemDto
            {
                Id = b.Id,
                Date = b.TransactionDate,
                Direction = b.Direction,
                Amount = b.Amount,
                BalanceAfter = 0m,
                PaymentMethod = $"{b.BankName} - {b.TransactionType}",
                Description = b.Description,
                ReferenceNumber = b.ReferenceNumber
            }).ToList();

            var result = new ManagementFinanceOverviewDto
            {
                TotalPoValue = totalPoVal,
                DeliveredValue = totalDeliveredVal,
                RemainingValue = totalRemVal,
                WasteValue = totalWasteVal,
                ImportedQuantity = totalDeliveredQty,
                TotalExpenses = totalExpenses,
                BankBalance = bankBalance,
                PaymentStatusCounts = statusCounts,
                PerformancePercentage = perfPct,
                Items = items,
                RecentExpenses = recentExpenses,
                RecentBankTransactions = recentBank,
                FilterFromDate = fromDate?.ToString("yyyy-MM-dd"),
                FilterToDate = toDate?.ToString("yyyy-MM-dd"),
                FilterPo = po,
                FilterPaymentStatus = paymentStatus
            };

            return Ok(ApiResponse<ManagementFinanceOverviewDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving finance monitoring data.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تحميل بيانات مراقبة المالية."));
        }
    }

    [HttpGet("purchase-orders/{poNumber}")]
    [ProducesResponseType(typeof(ApiResponse<ManagementFinancePoItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPurchaseOrderDetail(string poNumber)
    {
        try
        {
            var p = await _context.PurchaseOrders.AsNoTracking()
                .FirstOrDefaultAsync(x => x.PONumber.ToLower() == poNumber.ToLower());
            if (p == null)
            {
                return NotFound(ApiResponse.Fail($"أمر الشراء '{poNumber}' غير موجود."));
            }

            var trans = await _context.Transportations.AsNoTracking()
                .Where(t => t.PONumber.ToLower() == p.PONumber.ToLower())
                .ToListAsync();

            var dQty = trans.Sum(t => t.NetWeight);
            var wQty = trans.Sum(t => t.WasteQuantity);
            var price = p.TonPrice > 0 ? p.TonPrice : p.PricePerTon;

            var poVal = p.TotalValue > 0 ? p.TotalValue : Math.Round(p.Quantity * price, 2);
            var dVal = Math.Round(dQty * price, 2);
            var rVal = Math.Round(p.RemainingQuantity * price, 2);
            var wVal = Math.Round(wQty * price, 2);
            var progPct = p.Quantity > 0 ? Math.Round((dQty / p.Quantity) * 100, 1) : 0m;

            var dto = new ManagementFinancePoItemDto
            {
                PoNumber = p.PONumber,
                Supplier = p.Supplier,
                OrderDate = p.OrderDate,
                Quantity = p.Quantity,
                TonPrice = price,
                TotalValue = poVal,
                DeliveredQuantity = dQty,
                DeliveredValue = dVal,
                RemainingQuantity = p.RemainingQuantity,
                RemainingValue = rVal,
                WasteQuantity = wQty,
                WasteValue = wVal,
                PaymentStatus = string.IsNullOrWhiteSpace(p.PaymentStatus) ? "غير مدفوع" : p.PaymentStatus.Trim(),
                InvoiceNumber = p.InvoiceNumber,
                ProgressPercentage = progPct
            };

            return Ok(ApiResponse<ManagementFinancePoItemDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PO details for: {Po}", poNumber);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تحميل تفاصيل أمر الشراء."));
        }
    }

    // ========================================================================
    // 6. EMPLOYEES MONITORING: List, Rates, Salaries, Lendings, Attendance
    // ========================================================================
    [HttpGet("employees")]
    [ProducesResponseType(typeof(ApiResponse<List<ManagementEmployeeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] string? search,
        [FromQuery] string? department,
        [FromQuery] string? position)
    {
        try
        {
            var userQuery = _context.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                userQuery = userQuery.Where(u =>
                    u.FullName.ToLower().Contains(s) ||
                    u.Username.ToLower().Contains(s) ||
                    u.Email.ToLower().Contains(s) ||
                    u.PhoneNumber.ToLower().Contains(s) ||
                    u.Role.ToLower().Contains(s)
                );
            }

            var users = await userQuery.OrderBy(u => u.FullName).ToListAsync();

            if (!string.IsNullOrWhiteSpace(department))
            {
                var dept = department.Trim().ToLowerInvariant();
                users = users.Where(u => GetDepartmentForRole(u.Role).ToLower().Contains(dept)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(position))
            {
                var pos = position.Trim().ToLowerInvariant();
                users = users.Where(u => GetPositionForRole(u.Role).ToLower().Contains(pos)).ToList();
            }

            var finRecords = await _context.EmployeeFinancialRecords.AsNoTracking().ToDictionaryAsync(r => r.UserId);
            var lendings = await _context.EmployeeLendings.AsNoTracking().ToListAsync();
            var attendance = await _context.AttendanceRecords.AsNoTracking().ToListAsync();

            var result = new List<ManagementEmployeeDto>();

            foreach (var u in users)
            {
                finRecords.TryGetValue(u.Id, out var fin);
                var uLendings = lendings.Where(l => l.UserId == u.Id).OrderByDescending(l => l.RequestDate).ToList();
                var uAtt = attendance.Where(a => a.UserId == u.Id).ToList();

                var totLending = uLendings.Sum(l => l.LendingAmount);
                var paidLending = uLendings.Sum(l => l.PaidAmount);
                var remLending = uLendings.Sum(l => l.RemainingAmount);
                var lastLendingDate = uLendings.FirstOrDefault()?.RequestDate;

                var daysPresent = uAtt.Count(a => a.Status.Equals("Present", StringComparison.OrdinalIgnoreCase));
                var daysAbsent = uAtt.Count(a => a.Status.Equals("Absent", StringComparison.OrdinalIgnoreCase));
                var totDays = daysPresent + daysAbsent;
                decimal? attPct = totDays > 0 ? Math.Round((decimal)daysPresent / totDays * 100, 1) : null;

                var dept = GetDepartmentForRole(u.Role);
                var posTitle = GetPositionForRole(u.Role);

                result.Add(new ManagementEmployeeDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Username = u.Username,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    Role = u.Role,
                    Department = dept,
                    Position = posTitle,
                    Rate = "التقييم غير مدرج حالياً",
                    HasRatingSystem = false,
                    BasicSalary = fin?.BasicSalary ?? 0m,
                    NetSalary = fin?.NetSalary ?? 0m,
                    HasSalaryRecord = fin != null,
                    SalaryStatusMessage = fin != null ? $"{fin.NetSalary:N2} ج.م" : "غير مسجل بالنظام المالي",
                    TotalLending = totLending,
                    PaidLending = paidLending,
                    RemainingLending = remLending,
                    LastLendingDate = lastLendingDate,
                    DaysPresent = daysPresent,
                    DaysAbsent = daysAbsent,
                    AttendancePercentage = attPct,
                    FinancialStatus = remLending > 0 ? "توجد سلفة مستحقة" : "منتظم"
                });
            }

            return Ok(ApiResponse<List<ManagementEmployeeDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving management employees.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تحميل بيانات الموظفين."));
        }
    }

    [HttpGet("employees/{id}")]
    [ProducesResponseType(typeof(ApiResponse<ManagementEmployeeDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmployeeDetail(string id)
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

            var dept = GetDepartmentForRole(user.Role);
            var posTitle = GetPositionForRole(user.Role);

            var empDto = new ManagementEmployeeDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Username = user.Username,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                Department = dept,
                Position = posTitle,
                Rate = "التقييم غير مدرج حالياً",
                HasRatingSystem = false,
                BasicSalary = fin?.BasicSalary ?? 0m,
                NetSalary = fin?.NetSalary ?? 0m,
                HasSalaryRecord = fin != null,
                SalaryStatusMessage = fin != null ? $"{fin.NetSalary:N2} ج.م" : "غير مسجل بالنظام المالي",
                TotalLending = totLending,
                PaidLending = paidLending,
                RemainingLending = remLending,
                LastLendingDate = lendings.FirstOrDefault()?.RequestDate,
                DaysPresent = daysPresent,
                DaysAbsent = daysAbsent,
                AttendancePercentage = attPct,
                FinancialStatus = remLending > 0 ? "توجد سلفة مستحقة" : "منتظم"
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

            var attDtos = attendance.Select(a => new AttendanceRecordDto
            {
                Id = a.Id,
                Date = a.Date,
                Status = a.Status,
                CheckInTime = a.CheckInTime,
                CheckOutTime = a.CheckOutTime,
                Notes = a.Notes
            }).ToList();

            return Ok(ApiResponse<ManagementEmployeeDetailDto>.Ok(new ManagementEmployeeDetailDto
            {
                Employee = empDto,
                Lendings = lendingDtos,
                RecentAttendance = attDtos
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employee detail for: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تحميل تفاصيل الموظف."));
        }
    }

    // ========================================================================
    // 7. SUPPLIERS MONITORING: Supply Process, Quantities, Pricing, Progress
    // ========================================================================
    [HttpGet("suppliers")]
    [ProducesResponseType(typeof(ApiResponse<List<ManagementSupplierDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] string? search,
        [FromQuery] string? po,
        [FromQuery] string? material)
    {
        try
        {
            var pos = await _context.PurchaseOrders.AsNoTracking().ToListAsync();
            var trans = await _context.Transportations.AsNoTracking().ToListAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                pos = pos.Where(p => p.Supplier.ToLower().Contains(s) || p.PONumber.ToLower().Contains(s)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(po))
            {
                var pFilter = po.Trim().ToLowerInvariant();
                pos = pos.Where(p => p.PONumber.ToLower().Contains(pFilter)).ToList();
            }

            var supplierGroups = pos.GroupBy(p => p.Supplier.Trim(), StringComparer.OrdinalIgnoreCase).ToList();
            var result = new List<ManagementSupplierDto>();

            foreach (var g in supplierGroups)
            {
                var sName = g.Key;
                var sPos = g.ToList();
                var sPoNumbers = sPos.Select(p => p.PONumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

                var sTrans = trans.Where(t => sPoNumbers.Contains(t.PONumber)).ToList();

                if (!string.IsNullOrWhiteSpace(material))
                {
                    var mFilter = material.Trim().ToLowerInvariant();
                    sTrans = sTrans.Where(t => t.MaterialType.ToLower().Contains(mFilter)).ToList();
                    if (!sTrans.Any()) continue;
                }

                var origQty = sPos.Sum(p => p.Quantity);
                var deliveredQty = sTrans.Sum(t => t.NetWeight);
                var remQty = sPos.Sum(p => p.RemainingQuantity);
                var wasteQty = sTrans.Sum(t => t.WasteQuantity);

                var expectedVal = sPos.Sum(p => p.TotalValue > 0 ? p.TotalValue : (p.Quantity * (p.TonPrice > 0 ? p.TonPrice : p.PricePerTon)));
                var avgTonPrice = origQty > 0 ? Math.Round(expectedVal / origQty, 2) : 0m;

                var deliveredVal = Math.Round(deliveredQty * avgTonPrice, 2);
                var remVal = Math.Round(remQty * avgTonPrice, 2);

                var progressPct = origQty > 0 ? Math.Round((deliveredQty / origQty) * 100, 1) : 0m;

                var materials = sTrans.Select(t => t.MaterialType).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
                if (!materials.Any()) materials.Add("مواد خرسانية / ركام");

                var activeCount = sPos.Count(p => p.RemainingQuantity > 0);
                var completedCount = sPos.Count(p => p.RemainingQuantity <= 0);

                result.Add(new ManagementSupplierDto
                {
                    SupplierName = sName,
                    PosCount = sPos.Count,
                    OriginalQuantity = origQty,
                    DeliveredQuantity = deliveredQty,
                    RemainingQuantity = remQty,
                    WastedQuantity = wasteQty,
                    TonPrice = avgTonPrice,
                    ExpectedValue = expectedVal,
                    DeliveredValue = deliveredVal,
                    RemainingValue = remVal,
                    ProgressPercentage = progressPct,
                    Materials = string.Join(", ", materials),
                    ActivePosCount = activeCount,
                    CompletedPosCount = completedCount
                });
            }

            result = result.OrderByDescending(s => s.OriginalQuantity).ToList();
            return Ok(ApiResponse<List<ManagementSupplierDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving management suppliers.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تحميل بيانات الموردين."));
        }
    }

    [HttpGet("suppliers/{id}")]
    [ProducesResponseType(typeof(ApiResponse<ManagementSupplierDetailDto>), StatusCodes.Status200OK)]
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
                return NotFound(ApiResponse.Fail($"المورد '{sName}' غير موجود."));
            }

            var actualSupplierName = pos.First().Supplier;
            var poNumbers = pos.Select(p => p.PONumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var trans = await _context.Transportations.AsNoTracking()
                .Where(t => poNumbers.Contains(t.PONumber))
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            var storage = await _context.StorageTransactions.AsNoTracking()
                .Where(st => !string.IsNullOrEmpty(st.PONumber) && poNumbers.Contains(st.PONumber))
                .OrderByDescending(st => st.TransactionDate)
                .ToListAsync();

            var origQty = pos.Sum(p => p.Quantity);
            var deliveredQty = trans.Sum(t => t.NetWeight);
            var remQty = pos.Sum(p => p.RemainingQuantity);
            var wasteQty = trans.Sum(t => t.WasteQuantity);

            var expectedVal = pos.Sum(p => p.TotalValue > 0 ? p.TotalValue : (p.Quantity * (p.TonPrice > 0 ? p.TonPrice : p.PricePerTon)));
            var avgTonPrice = origQty > 0 ? Math.Round(expectedVal / origQty, 2) : 0m;

            var deliveredVal = Math.Round(deliveredQty * avgTonPrice, 2);
            var remVal = Math.Round(remQty * avgTonPrice, 2);
            var progressPct = origQty > 0 ? Math.Round((deliveredQty / origQty) * 100, 1) : 0m;

            var materials = trans.Select(t => t.MaterialType).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
            if (!materials.Any()) materials.Add("مواد خرسانية / ركام");

            var summary = new ManagementSupplierDto
            {
                SupplierName = actualSupplierName,
                PosCount = pos.Count,
                OriginalQuantity = origQty,
                DeliveredQuantity = deliveredQty,
                RemainingQuantity = remQty,
                WastedQuantity = wasteQty,
                TonPrice = avgTonPrice,
                ExpectedValue = expectedVal,
                DeliveredValue = deliveredVal,
                RemainingValue = remVal,
                ProgressPercentage = progressPct,
                Materials = string.Join(", ", materials),
                ActivePosCount = pos.Count(p => p.RemainingQuantity > 0),
                CompletedPosCount = pos.Count(p => p.RemainingQuantity <= 0)
            };

            var poDtos = pos.Select(p => {
                var pTrans = trans.Where(t => t.PONumber.Equals(p.PONumber, StringComparison.OrdinalIgnoreCase)).ToList();
                var pDelivered = pTrans.Sum(t => t.NetWeight);
                var pWaste = pTrans.Sum(t => t.WasteQuantity);
                var price = p.TonPrice > 0 ? p.TonPrice : p.PricePerTon;
                var pVal = p.TotalValue > 0 ? p.TotalValue : Math.Round(p.Quantity * price, 2);
                var pDelVal = Math.Round(pDelivered * price, 2);
                var pRemVal = Math.Round(p.RemainingQuantity * price, 2);
                var pWasteVal = Math.Round(pWaste * price, 2);
                var pProg = p.Quantity > 0 ? Math.Round((pDelivered / p.Quantity) * 100, 1) : 0m;

                return new ManagementFinancePoItemDto
                {
                    PoNumber = p.PONumber,
                    Supplier = p.Supplier,
                    OrderDate = p.OrderDate,
                    Quantity = p.Quantity,
                    TonPrice = price,
                    TotalValue = pVal,
                    DeliveredQuantity = pDelivered,
                    DeliveredValue = pDelVal,
                    RemainingQuantity = p.RemainingQuantity,
                    RemainingValue = pRemVal,
                    WasteQuantity = pWaste,
                    WasteValue = pWasteVal,
                    PaymentStatus = p.PaymentStatus,
                    InvoiceNumber = p.InvoiceNumber,
                    ProgressPercentage = pProg
                };
            }).ToList();

            var transDtos = trans.Select(t => new ManagementTransportationItemDto
            {
                Id = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                CarNumber = t.CarNumber,
                DriverName = t.DriverName,
                PONumber = t.PONumber,
                CompanyName = t.CompanyName,
                MaterialType = t.MaterialType,
                NetWeight = t.NetWeight,
                WasteQuantity = t.WasteQuantity,
                ReceivingStatus = t.ReceivingStatus ?? "مستلم",
                Destination = t.Destination,
                ShippingDurationMinutes = t.ShippingDurationMinutes,
                Notes = t.Notes
            }).ToList();

            var storageDtos = storage.Select(s => new ManagementStorageItemDto
            {
                Id = s.Id,
                MovementNumber = s.MovementNumber,
                ReferenceNumber = s.ReferenceNumber,
                TransactionDate = s.TransactionDate,
                Action = s.Action,
                ServiceType = s.ServiceType,
                Material = s.Material,
                PONumber = s.PONumber,
                IncomingQuantity = s.IncomingQuantity,
                OutgoingQuantity = s.OutgoingQuantity,
                ClosingBalance = s.ClosingBalance,
                StorageLocation = s.StorageLocation,
                ReceivingEmployee = s.ReceivingEmployee,
                IsExportRelatedToPO = s.IsExportRelatedToPO,
                Notes = s.Notes
            }).ToList();

            return Ok(ApiResponse<ManagementSupplierDetailDto>.Ok(new ManagementSupplierDetailDto
            {
                Summary = summary,
                PurchaseOrders = poDtos,
                TransportationTransactions = transDtos,
                StorageTransactions = storageDtos
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving supplier details for: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تحميل تفاصيل المورد."));
        }
    }

    // ========================================================================
    // 8. LEGACY ENDPOINTS: Backward Compatibility (Preserved)
    // ========================================================================
    [HttpGet("data")]
    [ProducesResponseType(typeof(ApiResponse<ManagementPageResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetManagementData([FromQuery] ManagementQueryParameters query)
    {
        try
        {
            var transRecords = await _context.Transportations.AsNoTracking().OrderByDescending(t => t.TransactionDate).ToListAsync();
            var standaloneMgmt = await _context.Management.AsNoTracking().ToListAsync();

            var aggregatedRecords = new List<ManagementRecordDto>();
            var groupedByPo = transRecords.GroupBy(t => string.IsNullOrWhiteSpace(t.PONumber) ? $"TX_{t.TransactionNumber}" : t.PONumber.Trim()).ToList();

            foreach (var group in groupedByPo)
            {
                var sortedItems = group.OrderByDescending(x => x.TransactionDate).ToList();
                var latest = sortedItems.First();

                var carMovements = sortedItems.Select(x => new ManagementCarMovementDto
                {
                    CarNumber = x.CarNumber,
                    TransactionNumber = x.TransactionNumber,
                    NetWeight = x.NetWeight,
                    Date = x.TransactionDate,
                    Status = x.ReceivingStatus
                }).ToList();

                var carPlatesFormatted = string.Join(" \n ", sortedItems.Select(x => $"{x.CarNumber} - حركة {x.TransactionNumber}"));
                var totalReceived = sortedItems.Sum(x => x.NetWeight);
                var accepted = sortedItems.Where(x => x.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase)).Sum(x => x.NetWeight);
                var unaccepted = sortedItems.Where(x => !x.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase)).Sum(x => x.NetWeight);

                string overallStatus = sortedItems.All(x => x.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase)) 
                    ? "Approved / Accepted" 
                    : latest.ReceivingStatus;

                aggregatedRecords.Add(new ManagementRecordDto
                {
                    TransactionNumber = latest.TransactionNumber.ToString(),
                    Date = latest.TransactionDate,
                    PONumber = string.IsNullOrWhiteSpace(latest.PONumber) ? "-" : latest.PONumber.Trim(),
                    MaterialType = latest.MaterialType,
                    NumberOfTransactions = sortedItems.Count,
                    TotalReceivedAmount = totalReceived,
                    CarPlatesNumber = carPlatesFormatted,
                    CarMovements = carMovements,
                    AcceptedQuantity = accepted,
                    UnacceptedPendingQuantity = unaccepted,
                    DeliveryStatus = overallStatus,
                    Destination = latest.Destination,
                    Notes = latest.Notes
                });
            }

            var filterOptions = new ManagementFilterOptionsDto
            {
                MaterialTypes = aggregatedRecords.Select(r => r.MaterialType).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList(),
                ReceivingStatuses = aggregatedRecords.Select(r => r.DeliveryStatus).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList(),
                Destinations = aggregatedRecords.Select(r => r.Destination).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList()
            };

            var filtered = aggregatedRecords.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                filtered = filtered.Where(r =>
                    r.TransactionNumber.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (r.PONumber != null && r.PONumber.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    r.MaterialType.Contains(s, StringComparison.OrdinalIgnoreCase)
                );
            }

            var filteredList = filtered.ToList();
            var summary = new ManagementSummaryDto
            {
                TotalMovements = filteredList.Sum(r => r.NumberOfTransactions),
                TotalReceivedAmount = filteredList.Sum(r => r.TotalReceivedAmount),
                TotalAcceptedQuantity = filteredList.Sum(r => r.AcceptedQuantity),
                TotalUnacceptedPendingQuantity = filteredList.Sum(r => r.UnacceptedPendingQuantity)
            };

            var pageSize = query.PageSize > 0 ? Math.Min(query.PageSize, 100) : 10;
            var page = Math.Max(query.Page, 1);
            var totalCount = filteredList.Count;
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            if (totalPages < 1) totalPages = 1;

            var pagedItems = filteredList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var response = new ManagementPageResponseDto
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                Summary = summary,
                FilterOptions = filterOptions
            };

            return Ok(ApiResponse<ManagementPageResponseDto>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving legacy management data.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تحميل السجلات."));
        }
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<ManagementSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetManagementSummary()
    {
        try
        {
            var transRecords = await _context.Transportations.AsNoTracking().ToListAsync();
            var summary = new ManagementSummaryDto
            {
                TotalMovements = transRecords.Count,
                TotalReceivedAmount = transRecords.Sum(t => t.NetWeight),
                TotalAcceptedQuantity = transRecords.Where(t => t.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase)).Sum(t => t.NetWeight),
                TotalUnacceptedPendingQuantity = transRecords.Where(t => !t.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase)).Sum(t => t.NetWeight)
            };
            return Ok(ApiResponse<ManagementSummaryDto>.Ok(summary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating management summary.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("فشل حساب ملخص الإدارة."));
        }
    }

    [HttpPost("sync")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SyncManagementTable()
    {
        return Ok(ApiResponse.Ok("تمت المزامنة بنجاح."));
    }

    // ========================================================================
    // DATABASE MAINTENANCE & DIAGNOSTICS (CEO & Management only)
    // ========================================================================
    [HttpGet("db/status")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDatabaseStatus()
    {
        try
        {
            var conn = _context.Database.GetDbConnection();
            var dbName = conn.Database;
            var dataSource = conn.DataSource;

            var tables = new List<string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME";
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
            }

            var appliedMigrations = (await _context.Database.GetAppliedMigrationsAsync()).ToList();
            var pendingMigrations = (await _context.Database.GetPendingMigrationsAsync()).ToList();

            return Ok(ApiResponse<object>.Ok(new
            {
                database = dbName,
                server = dataSource,
                tableCount = tables.Count,
                tables = tables,
                appliedMigrationsCount = appliedMigrations.Count,
                appliedMigrations = appliedMigrations,
                pendingMigrationsCount = pendingMigrations.Count,
                pendingMigrations = pendingMigrations,
                isSynchronized = !pendingMigrations.Any()
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get database status.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("فشل فحص حالة قاعدة البيانات: " + ex.Message));
        }
    }

    [HttpPost("db/migrate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunMigrations()
    {
        try
        {
            var pending = (await _context.Database.GetPendingMigrationsAsync()).ToList();
            if (!pending.Any())
            {
                return Ok(ApiResponse<object>.Ok(new { message = "قاعدة البيانات محدثة بالفعل ولا توجد هجرات معلقة.", appliedCount = 0 }));
            }

            await _context.Database.MigrateAsync();

            var tables = new List<string>();
            var conn = _context.Database.GetDbConnection();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME";
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
            }

            return Ok(ApiResponse<object>.Ok(new
            {
                message = "تم تطبيق الهجرات بنجاح.",
                appliedMigrations = pending,
                appliedCount = pending.Count,
                newTableCount = tables.Count,
                tables = tables
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute database migrations.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("فشل تطبيق الهجرات: " + ex.Message));
        }
    }

    [HttpPost("db/seed-test-data")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedTestData()
    {
        try
        {
            var result = await DbInitializer.SeedSafeTestDataAsync(_context, _logger);
            return Ok(ApiResponse<object>.Ok(result, "تم إدخال البيانات التجريبية الآمنة بنجاح (TEST-)."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed safe test data.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("فشل إدخال البيانات التجريبية: " + ex.Message));
        }
    }

    [HttpPost("db/cleanup-test-data")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CleanupTestData()
    {
        try
        {
            var result = await DbInitializer.CleanupSafeTestDataAsync(_context, _logger);
            return Ok(ApiResponse<object>.Ok(result, "تم حذف كافة البيانات التجريبية (TEST-) بنجاح وبقاء البيانات الأصلية كما هي."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup safe test data.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("فشل تنظيف البيانات التجريبية: " + ex.Message));
        }
    }

    // Helper functions
    private static string GetDepartmentForRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return "شؤون العمليات والمواقع";
        var r = role.Trim().ToUpperInvariant();
        if (r.Contains("TRANSPORT")) return "إدارة النقل واللوجستيات";
        if (r.Contains("STORAGE")) return "إدارة المخازن والمستودعات";
        if (r.Contains("FACTORY")) return "إدارة المصنع والتشغيل";
        if (r.Contains("FINANCE") || r.Contains("ACCOUNT")) return "الإدارة المالية والمحاسبة";
        if (r.Contains("CEO")) return "الإدارة العليا";
        if (r.Contains("IT")) return "تكنولوجيا المعلومات";
        if (r.Contains("ENGINEER")) return "الإدارة الهندسية والفنية";
        if (r.Contains("CONTRACTOR")) return "المقاولون والتنفيذ الميداني";
        return "شؤون العمليات والمواقع";
    }

    private static string GetPositionForRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return "موظف";
        var r = role.Trim().ToUpperInvariant();
        if (r == "CEO") return "الرئيس التنفيذي";
        if (r == "IT") return "مسؤول الدعم الفني وتقنية المعلومات";
        if (r.Contains("TRANSPORTATION_MANAGER") || r.Contains("TRANSPORTATION MANAGER")) return "مدير إدارة النقل واللوجستيات";
        if (r.Contains("STORAGE_MANAGER") || r.Contains("STORAGE MANAGER")) return "مدير إدارة المخازن والمستودعات";
        if (r.Contains("FACTORY_MANAGER") || r.Contains("FACTORY MANAGER")) return "مدير عام المصنع والتشغيل";
        if (r.Contains("FINANCE_DIRECTOR") || r.Contains("FINANCE DIRECTOR")) return "مدير الإدارة المالية";
        if (r.Contains("ACCOUNTANT")) return "محاسب مالي";
        if (r.Contains("SENIOR STRUCTURAL ENGINEER")) return "كبير مهندسي الإنشاءات";
        if (r.Contains("CONTRACTOR")) return "مقاول معتمد";
        return "موظف عمليات";
    }
}
