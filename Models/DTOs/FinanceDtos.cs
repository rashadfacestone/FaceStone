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
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime SelectedDate { get; set; }
    public string? SelectedPo { get; set; }
    public string FilterFromDate => FromDate.ToString("yyyy-MM-dd");
    public string FilterToDate => ToDate.ToString("yyyy-MM-dd");
    public string FilterDate => FromDate == ToDate ? FromDate.ToString("yyyy-MM-dd") : $"{FromDate:yyyy-MM-dd} إلى {ToDate:yyyy-MM-dd}";
    public string? FilterPoNumber => SelectedPo;
    public bool IsCurrentUserFinanceDirector { get; set; }
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
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
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
    public decimal Quantity => ExpectedQuantity;
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
    public string Status { get; set; } = "قيد التوريد";
    public string PaymentStatus { get; set; } = "غير مدفوع";
    public DateTime? PaymentStatusUpdatedAt { get; set; }
    public string? PaymentStatusUpdatedBy { get; set; }
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
    public string Status { get; set; } = "قيد التوريد";
    public string PaymentStatus { get; set; } = "غير مدفوع";
}

public class UpdatePaymentStatusDto
{
    public string Status { get; set; } = string.Empty;
    public string? PaymentStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? Notes { get; set; }
}

public class PaymentStatusAuditDto
{
    public int Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? Notes { get; set; }
}

public class PoTransportationItemDto
{
    public int TransactionNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public string CarNumber { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public decimal InternalWeight { get; set; }
    public decimal ExternalNetWeight { get; set; }
    public decimal NetWeight { get; set; }
    public decimal WasteQuantity { get; set; }
    public string TransactionStatus { get; set; } = string.Empty;
    public string ReceivingStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string ShippingSupervisor { get; set; } = string.Empty;
    public string DepartmentSupervisor { get; set; } = string.Empty;
}

public class PoStorageItemDto
{
    public int MovementNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public decimal IncomingQuantity { get; set; }
    public decimal OutgoingQuantity { get; set; }
    public decimal InternalNetWeight { get; set; }
    public decimal? ExternalNetWeight { get; set; }
    public decimal WasteQuantity { get; set; }
    public string StorageLocation { get; set; } = string.Empty;
    public string ReceivingEmployee { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class PoTimelineEventDto
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string BadgeType { get; set; } = "default";
}

public class PurchaseOrderDetailDto
{
    public int Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal TonPrice { get; set; }
    public decimal TotalValue { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "قيد التوريد";
    public DateTime CreatedAt { get; set; }

    public decimal DeliveredQuantity { get; set; }
    public decimal DeliveredPrice { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal RemainingPrice { get; set; }
    public int TransportationTransactionsCount { get; set; }
    public decimal TotalSuppliedNetWeight { get; set; }
    public decimal TransportationWasteQuantity { get; set; }
    public decimal TransportationWastePrice { get; set; }
    public decimal StorageWasteQuantity { get; set; }
    public decimal StorageWastePrice { get; set; }
    public decimal TotalWasteQuantity { get; set; }
    public decimal TotalWastePrice { get; set; }
    public decimal PerformancePercentage { get; set; }
    public decimal PerformancePricePercentage { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }

    // Payment Status & Permissions
    public string PaymentStatus { get; set; } = "غير مدفوع";
    public DateTime? PaymentStatusUpdatedAt { get; set; }
    public string? PaymentStatusUpdatedBy { get; set; }
    public bool IsCurrentUserFinanceDirector { get; set; }

    public List<PaymentStatusAuditDto> PaymentAudits { get; set; } = new();
    public List<FinancePaymentDto> Payments { get; set; } = new();
    public List<PoTransportationItemDto> TransportationTransactions { get; set; } = new();
    public List<PoStorageItemDto> StorageTransactions { get; set; } = new();
    public List<PoTimelineEventDto> Timeline { get; set; } = new();
}

public class FinancePerformanceDetailsDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? SelectedPo { get; set; }
    public string Scope { get; set; } = "AllPos";

    // Expected Metrics & Math Explanation
    public decimal ExpectedQuantity { get; set; }
    public decimal TonPrice { get; set; }
    public decimal ExpectedPrice { get; set; }
    public string ExpectedFormula { get; set; } = string.Empty;

    // Actual / Delivered Metrics
    public decimal DeliveredQuantity { get; set; }
    public decimal DeliveredPrice { get; set; }
    public string DeliveredFormula { get; set; } = string.Empty;

    // Waste Breakdown
    public decimal TransportationWasteQuantity { get; set; }
    public decimal TransportationWastePrice { get; set; }
    public string TransportationWasteFormula { get; set; } = string.Empty;

    public decimal StorageWasteQuantity { get; set; }
    public decimal StorageWastePrice { get; set; }
    public string StorageWasteFormula { get; set; } = string.Empty;

    public decimal TotalWasteQuantity { get; set; }
    public decimal TotalWastePrice { get; set; }
    public string TotalWasteFormula { get; set; } = string.Empty;

    // Remaining Metrics
    public decimal RemainingQuantity { get; set; }
    public decimal RemainingPrice { get; set; }
    public string RemainingFormula { get; set; } = string.Empty;

    // Performance Calculations
    public decimal QuantityPerformancePercentage { get; set; }
    public string QuantityPerformanceFormula { get; set; } = string.Empty;

    public decimal PricePerformancePercentage { get; set; }
    public string PricePerformanceFormula { get; set; } = string.Empty;

    public string Status { get; set; } = "High";
    public string StatusLabelArabic { get; set; } = "أداء ممتاز";

    // Range Activity (Within selected date range only)
    public int RangeTransportationTransactionsCount { get; set; }
    public decimal RangeTransportationNetWeight { get; set; }
    public decimal RangeTransportationWaste { get; set; }
    public decimal RangeStorageImportQuantity { get; set; }
    public decimal RangeStorageExportQuantity { get; set; }
    public decimal RangeStorageWaste { get; set; }
    public decimal RangeTotalWaste { get; set; }

    // Explanatory steps
    public List<string> StepByStepCalculations { get; set; } = new();

    // Helper Aliases for Frontend & Test Compatibility
    public decimal CumulativeExpectedQuantity => ExpectedQuantity;
    public decimal CumulativeDeliveredQuantity => DeliveredQuantity;
    public decimal CumulativeTotalWaste => TotalWasteQuantity;
    public decimal CumulativeTransportationWaste => TransportationWasteQuantity;
    public decimal CumulativeStorageWaste => StorageWasteQuantity;
    public decimal CumulativeRemainingQuantity => RemainingQuantity;
    public decimal CumulativeExpectedPrice => ExpectedPrice;
    public decimal CumulativeDeliveredPrice => DeliveredPrice;
    public decimal CumulativeRemainingPrice => RemainingPrice;
    public decimal CumulativeWasteFinancialValue => TotalWastePrice;
    public decimal OverallPerformancePercentage => QuantityPerformancePercentage;
    public decimal FinancialPerformancePercentage => PricePerformancePercentage;
    public string QuantitativeCalculationFormula => DeliveredFormula;
    public string QuantitativePerformancePercentageFormula => QuantityPerformanceFormula;
    public string FinancialPerformancePercentageFormula => PricePerformanceFormula;
    public int RangeTransportationCount => RangeTransportationTransactionsCount;
    public decimal RangeStorageImportedQuantity => RangeStorageImportQuantity;
    public string FilterFromDate => FromDate.ToString("yyyy-MM-dd");
    public string FilterToDate => ToDate.ToString("yyyy-MM-dd");
    public List<FinancePoSummaryDto> PoBreakdowns { get; set; } = new();

    // Forecasting Section
    public FinanceForecastingDto Forecasting { get; set; } = new();
}

public class FinanceForecastingDto
{
    public decimal ExpectedRemainingQuantity { get; set; }
    public decimal ExpectedRemainingValue { get; set; }
    public decimal OverallProgressPercentage { get; set; }
    public decimal AverageDailyDeliveredQuantity { get; set; }
    public int EstimatedDaysToCompletion { get; set; }
    public DateTime? ProjectedCompletionDate { get; set; }
    public decimal ExpectedFinalFinancialYield { get; set; }
    public string ForecastingSummaryArabic { get; set; } = string.Empty;
}

// ========================================================================
// 1. Finance History DTOs (/finance/history)
// ========================================================================
public class FinanceHistoryItemDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string TransactionType { get; set; } = string.Empty; // سداد مورد / دفعة / معاملة بنكية / مصروف تشغيلي / توريد / تغيير حالة سداد
    public string TransactionNumber { get; set; } = string.Empty;
    public string? PoNumber { get; set; }
    public string? Supplier { get; set; }
    public decimal Amount { get; set; }
    public string Direction { get; set; } = "Out"; // In (إيداع/وارد) أو Out (صرف/صادر)
    public string Status { get; set; } = "معتمد";
    public string User { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
}

public class FinanceHistoryResponseDto
{
    public List<FinanceHistoryItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public decimal TotalInflow { get; set; }
    public decimal TotalOutflow { get; set; }
    public string? FilterFromDate { get; set; }
    public string? FilterToDate { get; set; }
    public string? FilterTransactionType { get; set; }
    public string? FilterPo { get; set; }
    public string? FilterSupplier { get; set; }
}

// ========================================================================
// 2. Suppliers DTOs (/finance/suppliers)
// ========================================================================
public class FinanceSupplierItemDto
{
    public string SupplierName { get; set; } = string.Empty;
    public string Materials { get; set; } = string.Empty;
    public int PurchaseOrdersCount { get; set; }
    public decimal TotalContractedQuantity { get; set; }
    public decimal TotalDeliveredQuantity { get; set; }
    public decimal TotalRemainingQuantity { get; set; }
    public decimal TotalFinancialValue { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal TotalOutstandingAmount { get; set; }
    public int ActivePoCount { get; set; }
    public int CompletedPoCount { get; set; }
}

public class FinanceSupplierDetailDto
{
    public string SupplierName { get; set; } = string.Empty;
    public string Materials { get; set; } = string.Empty;
    public int PurchaseOrdersCount { get; set; }
    public decimal TotalContractedQuantity { get; set; }
    public decimal TotalDeliveredQuantity { get; set; }
    public decimal TotalRemainingQuantity { get; set; }
    public decimal TotalFinancialValue { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal TotalOutstandingAmount { get; set; }
    public List<FinancePoSummaryDto> PurchaseOrders { get; set; } = new();
    public List<PoTransportationItemDto> RecentDeliveries { get; set; } = new();
}

// ========================================================================
// 3. Bank Transactions DTOs (/finance/bank)
// ========================================================================
public class BankTransactionDto
{
    public int Id { get; set; }
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string Direction { get; set; } = "In";
    public decimal Amount { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBankTransactionDto
{
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string TransactionType { get; set; } = "تحويل بنكي";
    public string Direction { get; set; } = "In";
    public decimal Amount { get; set; }
    public string BankName { get; set; } = "CIB";
    public string? ReferenceNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class BankSummaryResponseDto
{
    public decimal CurrentBalance { get; set; }
    public decimal TotalReceived { get; set; }
    public decimal TotalSent { get; set; }
    public int TransactionsCount { get; set; }
    public List<BankTransactionDto> Transactions { get; set; } = new();
    public Dictionary<string, decimal> BankBalances { get; set; } = new();
}

// ========================================================================
// 4. Expenses DTOs (/finance/expenses)
// ========================================================================
public class ExpenseDto
{
    public int Id { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Beneficiary { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string PaymentMethod { get; set; } = "Bank Transfer";
    public string CreatedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateExpenseDto
{
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;
    public string Category { get; set; } = "أخرى";
    public decimal Amount { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Beneficiary { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string PaymentMethod { get; set; } = "Bank Transfer";
    public string? Notes { get; set; }
}

public class ExpenseCategoryBreakdownDto
{
    public string Category { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class ExpensesSummaryResponseDto
{
    public decimal TotalExpenses { get; set; }
    public decimal TodayExpenses { get; set; }
    public decimal RangeExpenses { get; set; }
    public int ExpensesCount { get; set; }
    public Dictionary<string, decimal> ExpensesByCategory { get; set; } = new();
    public List<ExpenseCategoryBreakdownDto> CategoryBreakdown { get; set; } = new();
    public List<ExpenseDto> Expenses { get; set; } = new();
}

// ========================================================================
// 5. Employees Finance DTOs (/finance/employees)
// ========================================================================
public class FinanceEmployeeSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal BasicSalary { get; set; }
    public decimal NetSalary { get; set; }
    public bool HasSalaryRecord { get; set; }
    public decimal TotalLending { get; set; }
    public decimal PaidLending { get; set; }
    public decimal RemainingLending { get; set; }
    public int DaysPresent { get; set; }
    public int DaysAbsent { get; set; }
    public decimal? AttendancePercentage { get; set; }
    public string FinancialStatus { get; set; } = "منتظم";
}

public class FinanceEmployeeDetailDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal BasicSalary { get; set; }
    public decimal Allowances { get; set; }
    public decimal InsuranceDeductions { get; set; }
    public decimal NetSalary { get; set; }
    public bool HasSalaryRecord { get; set; }
    public string PaymentMethod { get; set; } = "Bank Transfer";
    public string? BankAccountNumber { get; set; }
    public decimal TotalLending { get; set; }
    public decimal PaidLending { get; set; }
    public decimal RemainingLending { get; set; }
    public int DaysPresent { get; set; }
    public int DaysAbsent { get; set; }
    public decimal? AttendancePercentage { get; set; }
    public string FinancialStatus { get; set; } = "منتظم";
    public List<EmployeeLendingDto> Lendings { get; set; } = new();
    public List<AttendanceRecordDto> RecentAttendance { get; set; } = new();
}

public class EmployeeLendingDto
{
    public int Id { get; set; }
    public decimal LendingAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public DateTime RequestDate { get; set; }
    public string ApprovalStatus { get; set; } = "معتمد";
    public string? ApprovedBy { get; set; }
    public string? Notes { get; set; }
}

public class AttendanceRecordDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = "Present";
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? Notes { get; set; }
}



