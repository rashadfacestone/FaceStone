using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models.DTOs;

// ========================================================================
// 1. Existing Legacy DTOs (Preserved for backward compatibility)
// ========================================================================

public class ManagementCarMovementDto
{
    public string CarNumber { get; set; } = string.Empty;
    public int TransactionNumber { get; set; }
    public decimal NetWeight { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FormattedMovement => $"{CarNumber} - حركة {TransactionNumber}";
}

public class ManagementRecordDto
{
    public string TransactionNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? PONumber { get; set; }
    public string MaterialType { get; set; } = string.Empty;
    public int NumberOfTransactions { get; set; }
    public decimal TotalReceivedAmount { get; set; }
    public string CarPlatesNumber { get; set; } = string.Empty;
    public List<ManagementCarMovementDto> CarMovements { get; set; } = new();
    public decimal AcceptedQuantity { get; set; }
    public decimal UnacceptedPendingQuantity { get; set; }
    public string DeliveryStatus { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ManagementSummaryDto
{
    public int TotalMovements { get; set; }
    public decimal TotalReceivedAmount { get; set; }
    public decimal TotalAcceptedQuantity { get; set; }
    public decimal TotalUnacceptedPendingQuantity { get; set; }
}

public class ManagementFilterOptionsDto
{
    public List<string> MaterialTypes { get; set; } = new();
    public List<string> ReceivingStatuses { get; set; } = new();
    public List<string> Destinations { get; set; } = new();
}

public class ManagementPageResponseDto
{
    public List<ManagementRecordDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public ManagementSummaryDto Summary { get; set; } = new();
    public ManagementFilterOptionsDto FilterOptions { get; set; } = new();
}

public class ManagementQueryParameters
{
    public string? Search { get; set; }
    public string? MaterialType { get; set; }
    public string? ReceivingStatus { get; set; }
    public string? Destination { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SortBy { get; set; } = "date";
    public string? SortOrder { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

// ========================================================================
// 2. Management Department Landing Page Overview (Cards Real-time Summary)
// ========================================================================

public class ManagementOverviewCardTransportationDto
{
    public int TripsCount { get; set; }
    public decimal TotalReceivedQuantity { get; set; }
    public decimal TotalWasteQuantity { get; set; }
}

public class ManagementOverviewCardStorageDto
{
    public decimal TotalDeposit { get; set; }
    public decimal TotalIssue { get; set; }
    public decimal CurrentBalance { get; set; }
}

public class ManagementOverviewCardFinanceDto
{
    public decimal TotalExpenses { get; set; }
    public decimal TotalPoValue { get; set; }
    public decimal DeliveredValue { get; set; }
    public decimal PerformancePercentage { get; set; }
}

public class ManagementOverviewCardEmployeesDto
{
    public int TotalEmployees { get; set; }
    public int SalariedEmployeesCount { get; set; }
    public decimal TotalSalariesRecorded { get; set; }
    public decimal TotalRemainingLending { get; set; }
    public int ActiveLendingsCount { get; set; }
}

public class ManagementOverviewCardSuppliersDto
{
    public int TotalSuppliers { get; set; }
    public int TotalPos { get; set; }
    public decimal TotalRemainingQuantity { get; set; }
}

public class ManagementOverviewDto
{
    public ManagementOverviewCardTransportationDto Transportation { get; set; } = new();
    public ManagementOverviewCardStorageDto Storage { get; set; } = new();
    public ManagementOverviewCardFinanceDto Finance { get; set; } = new();
    public ManagementOverviewCardEmployeesDto Employees { get; set; } = new();
    public ManagementOverviewCardSuppliersDto Suppliers { get; set; } = new();
}

// ========================================================================
// 3. Global Search DTOs (Categorized Real-time Search)
// ========================================================================

public class ManagementSearchItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // purchaseOrders | transportation | storage | finance | employees | suppliers
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Badge { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = "عرض التفاصيل";
    public DateTime? Date { get; set; }
}

public class ManagementSearchResponseDto
{
    public string Query { get; set; } = string.Empty;
    public int TotalMatches { get; set; }
    public List<ManagementSearchItemDto> PurchaseOrders { get; set; } = new();
    public List<ManagementSearchItemDto> Transportation { get; set; } = new();
    public List<ManagementSearchItemDto> Storage { get; set; } = new();
    public List<ManagementSearchItemDto> Finance { get; set; } = new();
    public List<ManagementSearchItemDto> Employees { get; set; } = new();
    public List<ManagementSearchItemDto> Suppliers { get; set; } = new();
}

// ========================================================================
// 4. Transportation Monitoring Overview DTOs
// ========================================================================

public class ManagementTransportationItemDto
{
    public int Id { get; set; }
    public int TransactionNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public string CarNumber { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string PONumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public decimal NetWeight { get; set; }
    public decimal WasteQuantity { get; set; }
    public string ReceivingStatus { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string? InternalWeighbridgeCode { get; set; }
    public string? ExternalWeighbridgeCode { get; set; }
    public decimal InternalWeight { get; set; }
    public decimal ExternalNetWeight { get; set; }
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public int? ShippingDurationMinutes { get; set; }
    public string TransactionStatus { get; set; } = string.Empty;
    public bool HasAttachments { get; set; }
    public string? InvoiceFileName { get; set; }
    public string? WeighbridgeFileName { get; set; }
    public string? InvoiceAttachmentPath { get; set; }
    public string? WeighbridgeAttachmentPath { get; set; }
    public string? Notes { get; set; }
    public string ApprovalStatus { get; set; } = "APPROVED";
    public string? RejectionReason { get; set; }
}

public class ManagementTransportationOverviewDto
{
    public int TotalTrips { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal NetQuantity { get; set; }
    public decimal TotalWaste { get; set; }
    public int CarsCount { get; set; }
    public int DriversCount { get; set; }
    public int CompletedShipmentsCount { get; set; }
    public int CurrentShipmentsCount { get; set; }
    public double? AvgShippingDurationMinutes { get; set; }
    public int? LastMovementNumber { get; set; }
    public DateTime? LastMovementDate { get; set; }
    public List<ManagementTransportationItemDto> Items { get; set; } = new();
    public string? FilterFromDate { get; set; }
    public string? FilterToDate { get; set; }
    public string? FilterPo { get; set; }
    public string? FilterDriver { get; set; }
    public string? FilterMaterial { get; set; }
    public string? FilterDestination { get; set; }
}

// ========================================================================
// 5. Storage Monitoring Overview DTOs
// ========================================================================

public class ManagementStorageMaterialBreakdownDto
{
    public string Material { get; set; } = string.Empty;
    public decimal IncomingQuantity { get; set; }
    public decimal OutgoingQuantity { get; set; }
    public int TransactionsCount { get; set; }
}

public class ManagementStorageItemDto
{
    public int Id { get; set; }
    public int MovementNumber { get; set; }
    public int ReferenceNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Action { get; set; } = string.Empty; // Import / Export
    public string ServiceType { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public string? PONumber { get; set; }
    public decimal IncomingQuantity { get; set; }
    public decimal OutgoingQuantity { get; set; }
    public decimal ClosingBalance { get; set; }
    public string StorageLocation { get; set; } = string.Empty;
    public string ReceivingEmployee { get; set; } = string.Empty;
    public bool IsExportRelatedToPO { get; set; }
    public string? InternalWeighbridgeReference { get; set; }
    public string? ExternalWeighbridgeReference { get; set; }
    public decimal InternalNetWeight { get; set; }
    public decimal? ExternalNetWeight { get; set; }
    public bool HasAttachments { get; set; }
    public string? WeighbridgeFileName { get; set; }
    public string? WeighbridgeAttachmentPath { get; set; }
    public string? Notes { get; set; }
    public string ApprovalStatus { get; set; } = "APPROVED";
    public string? RejectionReason { get; set; }
}

public class ManagementStorageOverviewDto
{
    public decimal TotalDeposit { get; set; }
    public decimal TotalIssue { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal TotalWaste { get; set; }
    public int PoRelatedExportsCount { get; set; }
    public decimal PoRelatedExportsQuantity { get; set; }
    public int NonPoImportsCount { get; set; }
    public decimal NonPoImportsQuantity { get; set; }
    public int TransactionsCount { get; set; }
    public List<ManagementStorageMaterialBreakdownDto> MaterialBreakdown { get; set; } = new();
    public List<ManagementStorageItemDto> Items { get; set; } = new();
    public string? FilterFromDate { get; set; }
    public string? FilterToDate { get; set; }
    public string? FilterMaterial { get; set; }
    public string? FilterAction { get; set; }
    public string? FilterPo { get; set; }
}

// ========================================================================
// 6. Finance Monitoring Overview DTOs
// ========================================================================

public class ManagementExpenseItemDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string SpentBy { get; set; } = string.Empty;
}

public class ManagementBankTransactionItemDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Direction { get; set; } = string.Empty; // In / Out
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
}

public class ManagementFinancePoItemDto
{
    public string PoNumber { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal TonPrice { get; set; }
    public decimal TotalValue { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal DeliveredValue { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal RemainingValue { get; set; }
    public decimal WasteQuantity { get; set; }
    public decimal WasteValue { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public decimal ProgressPercentage { get; set; }
}

public class ManagementFinanceOverviewDto
{
    public decimal TotalPoValue { get; set; }
    public decimal DeliveredValue { get; set; }
    public decimal RemainingValue { get; set; }
    public decimal WasteValue { get; set; }
    public decimal ImportedQuantity { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal BankBalance { get; set; }
    public Dictionary<string, int> PaymentStatusCounts { get; set; } = new();
    public decimal PerformancePercentage { get; set; }
    public List<ManagementFinancePoItemDto> Items { get; set; } = new();
    public List<ManagementExpenseItemDto> RecentExpenses { get; set; } = new();
    public List<ManagementBankTransactionItemDto> RecentBankTransactions { get; set; } = new();
    public string? FilterFromDate { get; set; }
    public string? FilterToDate { get; set; }
    public string? FilterPo { get; set; }
    public string? FilterPaymentStatus { get; set; }
}

// ========================================================================
// 7. Employees Monitoring DTOs
// ========================================================================

public class ManagementEmployeeDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Rate { get; set; } = "التقييم غير مدرج حالياً";
    public bool HasRatingSystem { get; set; } = false;
    public decimal BasicSalary { get; set; }
    public decimal NetSalary { get; set; }
    public bool HasSalaryRecord { get; set; }
    public string SalaryStatusMessage { get; set; } = "غير مسجل بالنظام المالي";
    public decimal TotalLending { get; set; }
    public decimal PaidLending { get; set; }
    public decimal RemainingLending { get; set; }
    public DateTime? LastLendingDate { get; set; }
    public int DaysPresent { get; set; }
    public int DaysAbsent { get; set; }
    public decimal? AttendancePercentage { get; set; }
    public string FinancialStatus { get; set; } = "منتظم";
}

public class ManagementEmployeeDetailDto
{
    public ManagementEmployeeDto Employee { get; set; } = new();
    public List<EmployeeLendingDto> Lendings { get; set; } = new();
    public List<AttendanceRecordDto> RecentAttendance { get; set; } = new();
}

// ========================================================================
// 8. Suppliers Monitoring DTOs
// ========================================================================

public class ManagementSupplierDto
{
    public string SupplierName { get; set; } = string.Empty;
    public int PosCount { get; set; }
    public decimal OriginalQuantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal WastedQuantity { get; set; }
    public decimal TonPrice { get; set; }
    public decimal ExpectedValue { get; set; }
    public decimal DeliveredValue { get; set; }
    public decimal RemainingValue { get; set; }
    public decimal ProgressPercentage { get; set; }
    public string Materials { get; set; } = string.Empty;
    public int ActivePosCount { get; set; }
    public int CompletedPosCount { get; set; }
}

public class ManagementSupplierDetailDto
{
    public ManagementSupplierDto Summary { get; set; } = new();
    public List<ManagementFinancePoItemDto> PurchaseOrders { get; set; } = new();
    public List<ManagementTransportationItemDto> TransportationTransactions { get; set; } = new();
    public List<ManagementStorageItemDto> StorageTransactions { get; set; } = new();
}
