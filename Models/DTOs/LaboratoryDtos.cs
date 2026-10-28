using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace FaceStone.Web.Models.DTOs;

public class LaboratoryDashboardStatsDto
{
    public int PhysicalReportsCount { get; set; }
    public int ChemicalReportsCount { get; set; }
    public int InProcessTestsCount { get; set; }
    public int FinalCertificatesCount { get; set; }
    public int PendingApprovalCount { get; set; }
    public int ConformingBatchesCount { get; set; }
    public int NonConformingBatchesCount { get; set; }
    public decimal TotalAcceptedRawMaterialTons { get; set; }
    public decimal TotalFinalProductTons { get; set; }
    public int ActiveTraceableLifecyclesCount { get; set; }

    // Aliases for frontend & API compatibility
    public int ActiveProductionLifecycles => ActiveTraceableLifecyclesCount;
    public decimal TotalAcceptedRawMaterial => TotalAcceptedRawMaterialTons;
    public decimal TotalConformingFinishedProduct => TotalFinalProductTons;
}

public class LaboratoryReferenceLookupDto
{
    public int ProductionReferenceNumber { get; set; }
    public string LaboratorySection { get; set; } = string.Empty;
    public string RawMaterialDescription { get; set; } = string.Empty;
    public int? RawMaterialId { get; set; }
    public string? RawMaterialName { get; set; }
    public string? SupplierName { get; set; }
    public int? SourceTransportationId { get; set; }
    public int? TransportationTransactionNumber { get; set; }
    public string? PONumber { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string InspectionDecision { get; set; } = string.Empty;
    public DateTime AnalysisDateTime { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public bool IsStorageReceived { get; set; }
    public int InProcessTestsCount { get; set; }
    public bool HasFinalCertificate { get; set; }
    public string? FinalProductName { get; set; }
    public decimal? FinalProductQuantity { get; set; }
    public string? ConformityDecision { get; set; }
    public string Stage { get; set; } = "RAW_MATERIAL_ACCEPTED";
}

public class NextSampleNumberDto
{
    public int SampleNumber { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string LaboratorySection { get; set; } = string.Empty;
}

public class LaboratoryBatchLookupDto
{
    public int ProductionReferenceNumber { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string SampleName { get; set; } = string.Empty;
    public string SampleDescription { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string? PONumber { get; set; }
    public string LaboratorySection { get; set; } = "PHYSICAL";
    public DateTime CreatedAt { get; set; }
    public string SourceType { get; set; } = "LABORATORY_REPORT";
}

public class CreateRawMaterialReportDto
{
    public int? ProductionReferenceNumber { get; set; }
    public int? SampleNumber { get; set; }
    public string? SampleName { get; set; } = "سيليكا باودر (Micro Silica Powder)";
    public string? SampleDescription { get; set; } = "بودرة رمادية اللون";
    public string? BatchNumber { get; set; }
    public DateTime? SampleReceivedDate { get; set; }
    public DateTime? ReportIssueDate { get; set; }
    public string? LabManagerName { get; set; }
    public string? LabManagerSignature { get; set; }
    public DateTime? LabManagerDate { get; set; }
    public string? ComplyStatus { get; set; } = "COMPLY";
    public string? LaboratorySection { get; set; }
    public DateTime? AnalysisDateTime { get; set; }
    public string? RawMaterialDescription { get; set; }
    public int? RawMaterialId { get; set; }
    public string? SupplierName { get; set; }
    public int? SourceTransportationId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public string? PONumber { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal? RejectedQuantity { get; set; }
    public string? AnalysisResultsJson { get; set; }
    public string InspectionDecision { get; set; } = "ACCEPTED";
    public string? Notes { get; set; }
    public IFormFile? Attachment { get; set; }
    public string? AttachmentPath { get; set; }
}

public class RawMaterialReportDto
{
    public int Id { get; set; }
    public int ProductionReferenceNumber { get; set; }
    public int SampleNumber { get; set; }
    public string SampleName { get; set; } = string.Empty;
    public string? SampleDescription { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? SampleReceivedDate { get; set; }
    public DateTime? ReportIssueDate { get; set; }
    public string? LabManagerName { get; set; }
    public string? LabManagerSignature { get; set; }
    public DateTime? LabManagerDate { get; set; }
    public string ComplyStatus { get; set; } = "COMPLY";
    public string LaboratorySection { get; set; } = string.Empty;
    public DateTime AnalysisDateTime { get; set; }
    public string RawMaterialDescription { get; set; } = string.Empty;
    public int? RawMaterialId { get; set; }
    public string? RawMaterialName { get; set; }
    public string? SupplierName { get; set; }
    public int? SourceTransportationId { get; set; }
    public int? TransportationTransactionNumber { get; set; }
    public int? PurchaseOrderId { get; set; }
    public string? PONumber { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string? AnalysisResultsJson { get; set; }
    public string InspectionDecision { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFileType { get; set; }
    public long AttachmentFileSize { get; set; }
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public bool IsStorageReceived { get; set; }
    public DateTime? StorageReceivedAt { get; set; }
    public int? StorageOperationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class CreateInProcessReportDto
{
    public int ProductionReferenceNumber { get; set; }
    public string? ProductionBatchNumber { get; set; }
    public int? ProductionWorkInProcessId { get; set; }
    public string? LaboratorySection { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public DateTime? TestDateTime { get; set; }
    public string ProductionStage { get; set; } = string.Empty;
    public string? AnalysisResultsJson { get; set; }
    public string? SpecificationCriteria { get; set; }
    public string InspectionDecision { get; set; } = "MEETS_REQUIREMENTS";
    public string? Notes { get; set; }
    public IFormFile? Attachment { get; set; }
    public string? AttachmentPath { get; set; }
}

public class InProcessReportDto
{
    public int Id { get; set; }
    public int ProductionReferenceNumber { get; set; }
    public int? RawMaterialReportId { get; set; }
    public string ProductionBatchNumber { get; set; } = string.Empty;
    public int? ProductionWorkInProcessId { get; set; }
    public string LaboratorySection { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public DateTime TestDateTime { get; set; }
    public string ProductionStage { get; set; } = string.Empty;
    public string? AnalysisResultsJson { get; set; }
    public string? SpecificationCriteria { get; set; }
    public string InspectionDecision { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFileType { get; set; }
    public long AttachmentFileSize { get; set; }
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class CreateFinalConformityCertificateDto
{
    public int ProductionReferenceNumber { get; set; }
    public string? LaboratorySection { get; set; }
    public string FinalProductName { get; set; } = string.Empty;
    public decimal ActualFinalProductQuantity { get; set; }
    public string QuantityUnit { get; set; } = "طن";
    public string? ProductionBatchNumber { get; set; }
    public int? ProductionWorkInProcessId { get; set; }
    public DateTime? ProductionCompletionDateTime { get; set; }
    public decimal ActualRawMaterialInputQuantity { get; set; }
    public decimal? ActualMeasuredWaste { get; set; }
    public string WasteClassification { get; set; } = "ORDINARY_WASTE";
    public string ConformityDecision { get; set; } = "CONFORMS";
    public string? NonConformityAction { get; set; }
    public string? ClassifiedProductType { get; set; }
    public string? ReworkInstructions { get; set; }
    public string? SpecificationResultsJson { get; set; }
    public string? Observations { get; set; }
    public IFormFile? Attachment { get; set; }
    public string? AttachmentPath { get; set; }
}

public class FinalConformityCertificateDto
{
    public int Id { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public int ProductionReferenceNumber { get; set; }
    public int? RawMaterialReportId { get; set; }
    public string LaboratorySection { get; set; } = string.Empty;
    public string FinalProductName { get; set; } = string.Empty;
    public decimal ActualFinalProductQuantity { get; set; }
    public string QuantityUnit { get; set; } = "طن";
    public string ProductionBatchNumber { get; set; } = string.Empty;
    public int? ProductionWorkInProcessId { get; set; }
    public DateTime ProductionCompletionDateTime { get; set; }
    public decimal ActualRawMaterialInputQuantity { get; set; }
    public decimal CalculatedDifference { get; set; }
    public decimal? ActualMeasuredWaste { get; set; }
    public string WasteClassification { get; set; } = string.Empty;
    public string ConformityDecision { get; set; } = string.Empty;
    public string? NonConformityAction { get; set; }
    public string? ClassifiedProductType { get; set; }
    public string? ReworkInstructions { get; set; }
    public string? SpecificationResultsJson { get; set; }
    public string? Observations { get; set; }
    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFileType { get; set; }
    public long AttachmentFileSize { get; set; }
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public bool IsStoragePosted { get; set; }
    public DateTime? StoragePostedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class ProductionReferenceTraceabilityDto
{
    public int ProductionReferenceNumber { get; set; }
    public string LaboratorySection { get; set; } = string.Empty;
    public RawMaterialReportDto? RawMaterialReport { get; set; }
    public List<StorageOperationDto> StorageImports { get; set; } = new();
    public List<ProductionWipBatchDto> ProductionBatches { get; set; } = new();
    public List<InProcessReportDto> InProcessReports { get; set; } = new();
    public FinalConformityCertificateDto? FinalCertificate { get; set; }
    public List<FormApprovalHistoryDto> AuditTrail { get; set; } = new();
    public TransportationSummaryDto? RelatedTransportation { get; set; }
    public PurchaseOrderSummaryDto? RelatedPurchaseOrder { get; set; }

    // Aliases for compatibility
    public List<StorageOperationDto> ConnectedStorageOperations => StorageImports;
    public List<ProductionWipBatchDto> ConnectedProductionBatches => ProductionBatches;
    public FinalConformityCertificateDto? FinalConformityCertificate => FinalCertificate;
    public List<FormApprovalHistoryDto> Approvals => AuditTrail;
}

public class ProductionWipBatchDto
{
    public int Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public decimal MaterialQuantityIssued { get; set; }
    public string SourceStorageLocation { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FinalProductName { get; set; }
    public decimal? FinalProductQuantity { get; set; }
    public decimal? WasteQuantity { get; set; }
    public string WasteClassification { get; set; } = string.Empty;
    public string? RecoverableProductName { get; set; }
    public decimal? RecoverableQuantity { get; set; }
}

public class TransportationSummaryDto
{
    public int Id { get; set; }
    public int TransactionNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public string CarNumber { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string PONumber { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public decimal NetWeight { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
}

public class PurchaseOrderSummaryDto
{
    public int Id { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal PricePerTon { get; set; }
    public decimal TotalValue { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}
