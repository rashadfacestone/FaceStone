using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models.DTOs;

public class ProductionDashboardStatsDto
{
    public int ActiveBatches { get; set; }
    public int PendingRequests { get; set; }
    public int OnHoldBatches { get; set; }
    public int InProcessTestsCount { get; set; }
    public int SemiFinishedCount { get; set; }
    public int FinishedCount { get; set; }
    public int ReworkCount { get; set; }
    public decimal TotalIssuedRawMaterial { get; set; }
    public decimal TotalProducedOutput { get; set; }
    public decimal TotalMeasuredWaste { get; set; }
}

public class ProductionInventoryItemDto
{
    public int RawMaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public int StorageLocationId { get; set; }
    public string StorageLocationName { get; set; } = string.Empty;
    public decimal AvailableQuantity { get; set; }
    public string Unit { get; set; } = "طن";
    public decimal ReservedQuantity { get; set; }
    public decimal AvailableForNewOrders { get; set; }
    public int? ProductionReferenceNumber { get; set; }
    public bool IsLabEligible { get; set; }
    public string LabInspectionStatus { get; set; } = string.Empty;
}

public class EligibleRawMaterialDto
{
    public int RawMaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public int ProductionReferenceNumber { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal AvailableStock { get; set; }
    public int StorageLocationId { get; set; }
    public string StorageLocationName { get; set; } = string.Empty;
    public string LaboratorySection { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
}

public class CreateProductionOrderItemDto
{
    public int RawMaterialId { get; set; }
    public decimal RequestedQuantity { get; set; }
    public string Unit { get; set; } = "طن";
    public string? Notes { get; set; }
}

public class CreateProductionOrderRequestDto
{
    public int? ProductionReferenceNumber { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public string Unit { get; set; } = "طن";
    public int StorageLocationId { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public string? Notes { get; set; }
    public List<CreateProductionOrderItemDto> Items { get; set; } = new();
}

public class ProductionOrderItemDto
{
    public int Id { get; set; }
    public int ProductionOrderId { get; set; }
    public int RawMaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public decimal IssuedQuantity { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public string Unit { get; set; } = "طن";
    public int? StorageLocationId { get; set; }
    public string? Notes { get; set; }
}

public class ProductionOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int? ProductionReferenceNumber { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public string Unit { get; set; } = "طن";
    public int StorageLocationId { get; set; }
    public string StorageLocationName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Notes { get; set; }
    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public bool IsStorageApproved { get; set; }
    public DateTime? StorageApprovedAt { get; set; }
    public string? StorageApprovedBy { get; set; }
    public int? StorageOperationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public List<ProductionOrderItemDto> Items { get; set; } = new();
    public List<ProductionBatchSummaryDto> Batches { get; set; } = new();
    public List<ProductionOutputSummaryDto> Outputs { get; set; } = new();
}

public class ProductionBatchSummaryDto
{
    public int Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool QualityHold { get; set; }
    public string? QualityHoldReason { get; set; }
    public decimal TotalInputQuantity { get; set; }
    public decimal TotalOutputQuantity { get; set; }
    public DateTime StartedAt { get; set; }
}

public class ProductionBatchDto
{
    public int Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int ProductionOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int? ProductionReferenceNumber { get; set; }
    public string CurrentStage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool QualityHold { get; set; }
    public string? QualityHoldReason { get; set; }
    public int? QualityHoldReportId { get; set; }
    public DateTime? QualityHoldAt { get; set; }
    public DateTime? QualityHoldReleasedAt { get; set; }
    public string? QualityHoldReleasedBy { get; set; }
    public decimal TotalInputQuantity { get; set; }
    public decimal TotalOutputQuantity { get; set; }
    public decimal TotalWasteQuantity { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ProductionOutputSummaryDto> Outputs { get; set; } = new();
    public List<InProcessReportDto> InProcessTests { get; set; } = new();
}

public class StartProductionBatchRequestDto
{
    public int ProductionOrderId { get; set; }
    public string InitialStage { get; set; } = "الخلط الأولي";
    public string? Notes { get; set; }
}

public class AdvanceBatchStageRequestDto
{
    public string NextStage { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class CreateProductionOutputRequestDto
{
    public int ProductionBatchId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Classification { get; set; } = "FINISHED"; // SEMI_FINISHED | FINISHED | RECOVERABLE_WASTE
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "طن";
    public string ProductionStage { get; set; } = string.Empty;
    public string? NextStage { get; set; }
    public int? DestinationStorageLocationId { get; set; }
    public string? Notes { get; set; }
}

public class ProductionOutputSummaryDto
{
    public int Id { get; set; }
    public string OutputNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "طن";
    public string ProductionStage { get; set; } = string.Empty;
    public string LaboratoryConformityStatus { get; set; } = string.Empty;
    public bool IsStorageReceived { get; set; }
    public DateTime OutputDate { get; set; }
}

public class ProductionOutputDto
{
    public int Id { get; set; }
    public string OutputNumber { get; set; } = string.Empty;
    public int ProductionOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int ProductionBatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int? ProductionReferenceNumber { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "طن";
    public string ProductionStage { get; set; } = string.Empty;
    public DateTime OutputDate { get; set; }
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public string? NextStage { get; set; }
    public int? DestinationStorageLocationId { get; set; }
    public string? DestinationStorageLocationName { get; set; }
    public string LaboratoryConformityStatus { get; set; } = string.Empty;
    public int? LaboratoryCertificateId { get; set; }
    public bool IsStorageReceived { get; set; }
    public decimal StorageReceivedQuantity { get; set; }
    public DateTime? StorageReceivedAt { get; set; }
    public string? StorageReceivedBy { get; set; }
    public int? StorageOperationId { get; set; }
    public string? NonConformityAction { get; set; }
    public bool IsReworkCreated { get; set; }
    public int? ReworkOrderId { get; set; }
    public string? Notes { get; set; }
    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class CreateReworkOrderRequestDto
{
    public int ProductionOutputId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string? AdditionalMaterialNotes { get; set; }
}

public class ProductionReworkDto
{
    public int Id { get; set; }
    public string ReworkNumber { get; set; } = string.Empty;
    public int OriginalProductionOrderId { get; set; }
    public string OriginalOrderNumber { get; set; } = string.Empty;
    public int OriginalProductionBatchId { get; set; }
    public string OriginalBatchNumber { get; set; } = string.Empty;
    public int OriginalProductionOutputId { get; set; }
    public string OriginalOutputNumber { get; set; } = string.Empty;
    public int? ProductionReferenceNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? NewProductionOrderId { get; set; }
    public int? NewProductionBatchId { get; set; }
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class ReceiveOutputInStorageRequestDto
{
    public int ProductionOutputId { get; set; }
    public int StorageLocationId { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public string? Notes { get; set; }
}

// =========================================================================
// DAILY PRODUCTION SYSTEM DTOs (تسجيل الإنتاج اليومي)
// =========================================================================

public class DailyProductionItemInputDto
{
    public string ProductName { get; set; } = string.Empty;
    public int? ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "طن";
    public decimal? ProductWaste { get; set; }
}

public class DailyProductionSubmitRequestDto
{
    public DateTime ProductionDate { get; set; } = DateTime.Today;
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public decimal WasteQuantity { get; set; }
    public string? Notes { get; set; }
    public int? ProductionReferenceNumber { get; set; }
    public List<DailyProductionItemInputDto> Products { get; set; } = new();
}

public class DailyProductionDetailDto
{
    public int Id { get; set; }
    public int DailyProductionSubmissionId { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "طن";
    public decimal ProductionRatePerHour { get; set; }
    public decimal? ProductWaste { get; set; }
    public decimal? NetQuantity { get; set; }
}

public class DailyProductionSubmissionDto
{
    public int Id { get; set; }
    public string SubmissionNumber { get; set; } = string.Empty;
    public DateTime ProductionDate { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeEmail { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public decimal DurationHours { get; set; }
    public decimal TotalGrossQuantity { get; set; }
    public decimal WasteQuantity { get; set; }
    public decimal NetProductionQuantity { get; set; }
    public decimal ProductionRatePerHour { get; set; }
    public decimal WastePercentage { get; set; }
    public string? Notes { get; set; }
    public int? ProductionReferenceNumber { get; set; }
    public string Status { get; set; } = "SUBMITTED";
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public List<DailyProductionDetailDto> Details { get; set; } = new();
}

public class DailyProductAggregateDto
{
    public string ProductName { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public string Unit { get; set; } = "طن";
    public int SubmissionsCount { get; set; }
}

public class DailyProductionSummaryStatsDto
{
    public decimal TotalGrossToday { get; set; }
    public decimal TotalWasteToday { get; set; }
    public decimal NetProductionToday { get; set; }
    public decimal OverallProductionRate { get; set; }
    public decimal AverageWastePercentage { get; set; }
    public decimal TotalDurationHours { get; set; }
    public int TotalSubmissionsCount { get; set; }
    public List<DailyProductAggregateDto> ProductsBreakdown { get; set; } = new();
}

public class DailyProductionShiftInfoDto
{
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeEmail { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
    public bool IsProductionManager { get; set; }
    public bool CanSubmit { get; set; }
    public string? ErrorMessage { get; set; }
    public string Today { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
}

