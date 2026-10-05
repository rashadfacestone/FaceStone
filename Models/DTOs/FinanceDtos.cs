using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models.DTOs;

public class FinanceRecordDto
{
    /// <summary>
    /// 1. PO - رقم أمر الشراء
    /// </summary>
    public string PoNumber { get; set; } = string.Empty;

    /// <summary>
    /// 2. المورد - Supplier Name
    /// </summary>
    public string Supplier { get; set; } = string.Empty;

    /// <summary>
    /// 3. تاريخ أمر الشراء - PO Date
    /// </summary>
    public DateTime OrderDate { get; set; }

    /// <summary>
    /// 4. كمية أمر الشراء (بالطن) - PO Quantity
    /// </summary>
    public decimal PoQuantity { get; set; }

    /// <summary>
    /// 5. قيمة أمر الشراء (ج.م) = كمية أمر الشراء × سعر الطن
    /// </summary>
    public decimal PoValue { get; set; }

    /// <summary>
    /// 6. سعر الطن (ج.م) - Ton Price
    /// </summary>
    public decimal TonPrice { get; set; }

    /// <summary>
    /// 7. الكمية التي تم توريدها (بالطن) - Supplied Quantity
    /// </summary>
    public decimal SuppliedQuantity { get; set; }

    /// <summary>
    /// 8. قيمة الكمية التي تم توريدها (ج.م) = الكمية التي تم توريدها × سعر الطن
    /// </summary>
    public decimal SuppliedValue { get; set; }

    /// <summary>
    /// 9. الكمية المتبقية (بالطن) = كمية أمر الشراء - الكمية التي تم توريدها
    /// </summary>
    public decimal RemainingQuantity { get; set; }

    /// <summary>
    /// 10. القيمة المتبقية (ج.م) = الكمية المتبقية × سعر الطن
    /// </summary>
    public decimal RemainingValue { get; set; }

    /// <summary>
    /// 11. عدد النقلات - Total transportation movements
    /// </summary>
    public int MovementsCount { get; set; }

    /// <summary>
    /// 12. الكمية المعتمدة (بالطن) - Approved quantity
    /// </summary>
    public decimal ApprovedQuantity { get; set; }

    /// <summary>
    /// 13. الكمية الهالك / الغير معتمدة (بالطن) = الكمية التي تم توريدها - الكمية المعتمدة
    /// </summary>
    public decimal UnapprovedQuantity { get; set; }

    /// <summary>
    /// 14. المبلغ المدفوع (ج.م) - Sum of payments by CEO
    /// </summary>
    public decimal PaidAmount { get; set; }

    /// <summary>
    /// 15. المبلغ المستحق (ج.م) = قيمة الكمية التي تم توريدها - المبلغ المدفوع
    /// </summary>
    public decimal OutstandingAmount { get; set; }

    /// <summary>
    /// 16. حالة السداد - Payment Status ("غير مدفوع" | "مدفوع جزئياً" | "مدفوع بالكامل")
    /// </summary>
    public string PaymentStatus { get; set; } = "غير مدفوع";

    /// <summary>
    /// 17. رقم الفاتورة - Invoice Number
    /// </summary>
    public string? InvoiceNumber { get; set; }
}

public class FinanceKpiSummaryDto
{
    /// <summary>
    /// إجمالي قيمة أوامر الشراء
    /// </summary>
    public decimal TotalPoValue { get; set; }

    /// <summary>
    /// إجمالي الكمية التي تم توريدها
    /// </summary>
    public decimal TotalSuppliedQuantity { get; set; }

    /// <summary>
    /// إجمالي الكمية المتبقية
    /// </summary>
    public decimal TotalRemainingQuantity { get; set; }

    /// <summary>
    /// إجمالي المبلغ المدفوع
    /// </summary>
    public decimal TotalPaidAmount { get; set; }

    /// <summary>
    /// إجمالي المبلغ المستحق
    /// </summary>
    public decimal TotalOutstandingAmount { get; set; }

    /// <summary>
    /// إجمالي عدد النقلات
    /// </summary>
    public int TotalMovementsCount { get; set; }

    /// <summary>
    /// عدد أوامر الشراء الإجمالي
    /// </summary>
    public int TotalPosCount { get; set; }
}

public class FinanceFilterOptionsDto
{
    public List<string> Suppliers { get; set; } = new();
    public List<string> PaymentStatuses { get; set; } = new();
    public List<string> InvoiceNumbers { get; set; } = new();
}

public class FinancePageResponseDto
{
    public FinanceKpiSummaryDto KpiSummary { get; set; } = new();
    public List<FinanceRecordDto> Records { get; set; } = new();
    public FinanceFilterOptionsDto FilterOptions { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class FinanceQueryParameters
{
    public string? Search { get; set; }
    public string? Po { get; set; }
    public string? Supplier { get; set; }
    public string? PaymentStatus { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class CreateFinancePaymentDto
{
    public string PoNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Bank Transfer";
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}

public class FinancePaymentDto
{
    public int Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? PaidBy { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FinanceStatisticsResponseDto
{
    public DateTime SelectedDate { get; set; }
    public string? SelectedPo { get; set; }
    public string FilterDate => SelectedDate.ToString("yyyy-MM-dd");
    public string? FilterPoNumber => SelectedPo;
    public FinanceDailyActivityDto DailyActivity { get; set; } = new();
    public FinancePoSummaryDto? PoSummary { get; set; }
    public FinancePoSummaryDto? CumulativePoSummary => PoSummary;
    public FactoryPerformanceDto? Performance { get; set; }
    public FactoryPerformanceDto? FactoryPerformance => Performance;
    public List<FinancePoDropdownItemDto> AvailablePos { get; set; } = new();
    public List<FinancePoSummaryDto> AllPosSummary { get; set; } = new();
}

public class FinanceDailyActivityDto
{
    public DateTime SelectedDate { get; set; }
    public int TransportationTransactionsCount { get; set; }
    public decimal TransportationNetWeight { get; set; }
    public decimal TransportationWasteQuantity { get; set; }
    public decimal TransportationWastePrice { get; set; }
    public decimal StorageImportedQuantity { get; set; }
    public decimal? StorageImportedPrice { get; set; }
    public string StorageImportedPriceNote { get; set; } = "لا يوجد مصدر تسعير للواردات المخزنية في قاعدة البيانات";
    public decimal StorageExportedQuantity { get; set; }
    public decimal StorageWasteQuantity { get; set; }
    public decimal StorageWastePrice { get; set; }
    public decimal TotalDailyWasteQuantity { get; set; }
    public decimal TotalDailyWastePrice { get; set; }
    public decimal TotalWasteQuantity => TotalDailyWasteQuantity;
    public decimal TotalWastePrice => TotalDailyWastePrice;
}

public class FinancePoSummaryDto
{
    public string Scope { get; set; } = "AllPos";
    public string PoNumber { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public decimal TonPrice { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal ExpectedQuantity { get; set; }
    public decimal ExpectedPrice { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal DeliveredPrice { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal RemainingPrice { get; set; }
    public int TransportationTransactionsCount { get; set; }
    public int TransportationMovementsCount => TransportationTransactionsCount;
    public decimal TransportationWasteQuantity { get; set; }
    public decimal TransportationWastePrice { get; set; }
    public decimal StorageWasteQuantity { get; set; }
    public decimal StorageWastePrice { get; set; }
    public decimal TotalWasteQuantity { get; set; }
    public decimal TotalWastePrice { get; set; }
    public decimal ActualQuantity { get; set; }
    public decimal ActualPrice { get; set; }
    public decimal PerformancePercentage { get; set; }
    public decimal FactoryPerformancePercentage => PerformancePercentage;
    public decimal PerformancePricePercentage { get; set; }
    public decimal TotalSuppliedNetWeight { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
}

public class FactoryPerformanceDto
{
    public decimal ExpectedQuantity { get; set; }
    public decimal ExpectedPrice { get; set; }
    public decimal ActualQuantity { get; set; }
    public decimal ActualPrice { get; set; }
    public decimal ActualDeliveredQuantity => ActualQuantity;
    public decimal ActualDeliveredPrice => ActualPrice;
    public decimal WasteQuantity { get; set; }
    public decimal WastePrice { get; set; }
    public decimal TotalWasteQuantity => WasteQuantity;
    public decimal TotalWastePrice => WastePrice;
    public decimal RemainingQuantity { get; set; }
    public decimal RemainingPrice { get; set; }
    public decimal PerformancePercentage { get; set; }
    public decimal PerformancePricePercentage { get; set; }
    public string Status { get; set; } = "High";
    public string StatusLabelArabic { get; set; } = "أداء ممتاز";
}

public class FinancePoDropdownItemDto
{
    public string PoNumber { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public decimal TonPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public DateTime OrderDate { get; set; }
}

