using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models;

/// <summary>
/// Sequence tracker for Production Orders, Batches, Outputs, and Reworks
/// </summary>
public class ProductionSequence
{
    /// <summary>
    /// ORDER | BATCH | OUTPUT | REWORK
    /// </summary>
    public string SequenceType { get; set; } = string.Empty;

    public int NextValue { get; set; } = 1;
}

/// <summary>
/// Production Order / Request (أمر وطلب إنتاج)
/// Tracks requested output, planned raw materials, storage approval, and manufacturing lifecycle.
/// </summary>
public class ProductionOrder
{
    public int Id { get; set; }

    /// <summary>
    /// Unique Order Identifier (e.g. ORD-2026-0001)
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Shared Lifecycle Production Reference Number originated from Laboratory
    /// </summary>
    public int? ProductionReferenceNumber { get; set; }

    /// <summary>
    /// Requested Product Name or Manufacturing Process (e.g. خرسانة جاهزة رتبة C35)
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Requested target output quantity (TON)
    /// </summary>
    public decimal RequestedQuantity { get; set; }

    /// <summary>
    /// Unit of measure (default: طن)
    /// </summary>
    public string Unit { get; set; } = "طن";

    /// <summary>
    /// Warehouse Storage Location from which materials are requested
    /// </summary>
    public int StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }
    public string StorageLocationName { get; set; } = string.Empty;

    /// <summary>
    /// Order status:
    /// DRAFT | PENDING_STORAGE_APPROVAL | PARTIALLY_ISSUED | READY_FOR_PRODUCTION | 
    /// IN_PRODUCTION | ON_HOLD_LABORATORY | PENDING_FINAL_LABORATORY_INSPECTION | 
    /// REWORK_REQUIRED | APPROVED_FOR_STORAGE | COMPLETED | CANCELLED
    /// </summary>
    public string Status { get; set; } = "DRAFT";

    /// <summary>
    /// Current Manufacturing Stage (الخلط الأولي | الطحن والتجهيز | التشكيل والصب | المعالجة | التجفيف | المرحلة النهائية)
    /// </summary>
    public string CurrentStage { get; set; } = "الخلط الأولي";

    /// <summary>
    /// Production Manager or responsible employee
    /// </summary>
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }

    public DateTime? PlannedStartDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public string? Notes { get; set; }

    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFileType { get; set; }
    public long AttachmentFileSize { get; set; }

    /// <summary>
    /// Storage Transfer Operation fulfilling this request
    /// </summary>
    public int? StorageOperationId { get; set; }
    public StorageOperation? StorageOperation { get; set; }

    public bool IsStorageApproved { get; set; }
    public DateTime? StorageApprovedAt { get; set; }
    public string? StorageApprovedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Planned raw material requirements
    /// </summary>
    public ICollection<ProductionOrderItem> Items { get; set; } = new List<ProductionOrderItem>();

    /// <summary>
    /// Active and historical production batches under this order
    /// </summary>
    public ICollection<ProductionBatch> Batches { get; set; } = new List<ProductionBatch>();

    /// <summary>
    /// Recorded production outputs under this order
    /// </summary>
    public ICollection<ProductionOutput> Outputs { get; set; } = new List<ProductionOutput>();
}

/// <summary>
/// Planned raw material line item for a Production Order
/// </summary>
public class ProductionOrderItem
{
    public int Id { get; set; }

    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    public string MaterialName { get; set; } = string.Empty;

    /// <summary>
    /// Requested quantity (TON)
    /// </summary>
    public decimal RequestedQuantity { get; set; }

    /// <summary>
    /// Actual quantity issued by Storage (TON)
    /// </summary>
    public decimal IssuedQuantity { get; set; }

    /// <summary>
    /// Actual quantity consumed in manufacturing (TON)
    /// </summary>
    public decimal ConsumedQuantity { get; set; }

    public string Unit { get; set; } = "طن";

    public int? StorageLocationId { get; set; }
    public int? LaboratoryRawMaterialReportId { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Active Production Batch (تشغيلة الإنتاج)
/// Governs stage-by-stage execution, in-process laboratory quality gates, and holds.
/// </summary>
public class ProductionBatch
{
    public int Id { get; set; }

    /// <summary>
    /// Unique Batch Tracking Identifier (e.g. BATCH-2026-0001 or PRD-2026-0001-B1)
    /// </summary>
    public string BatchNumber { get; set; } = string.Empty;

    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    /// <summary>
    /// Shared Lifecycle Production Reference Number originated from Laboratory
    /// </summary>
    public int? ProductionReferenceNumber { get; set; }

    /// <summary>
    /// Link to existing ProductionWorkInProcess for full backward compatibility
    /// </summary>
    public int? ProductionWorkInProcessId { get; set; }
    public ProductionWorkInProcess? ProductionWorkInProcess { get; set; }

    /// <summary>
    /// Current Manufacturing Stage
    /// </summary>
    public string CurrentStage { get; set; } = "الخلط الأولي";

    /// <summary>
    /// Status: IN_PROGRESS | ON_HOLD_LABORATORY | READY_FOR_INSPECTION | COMPLETED | CANCELLED | REWORK
    /// </summary>
    public string Status { get; set; } = "IN_PROGRESS";

    /// <summary>
    /// Quality Gate Hold: TRUE if an in-process laboratory test has failed (DOES_NOT_MEET)
    /// </summary>
    public bool QualityHold { get; set; }

    /// <summary>
    /// Reason for Laboratory Hold
    /// </summary>
    public string? QualityHoldReason { get; set; }

    /// <summary>
    /// The failed In-Process Report ID that triggered the hold
    /// </summary>
    public int? QualityHoldReportId { get; set; }

    public DateTime? QualityHoldAt { get; set; }
    public DateTime? QualityHoldReleasedAt { get; set; }
    public string? QualityHoldReleasedBy { get; set; }

    /// <summary>
    /// Total actual raw material input issued to this batch (TON)
    /// </summary>
    public decimal TotalInputQuantity { get; set; }

    /// <summary>
    /// Total recorded output from this batch (TON)
    /// </summary>
    public decimal TotalOutputQuantity { get; set; }

    /// <summary>
    /// Total recorded waste from this batch (TON)
    /// </summary>
    public decimal TotalWasteQuantity { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public string ResponsibleEmployee { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<ProductionOutput> Outputs { get; set; } = new List<ProductionOutput>();
}

/// <summary>
/// Production Output (تسجيل ناتج الإنتاج)
/// Classifies actual manufacturing output as Semi-Finished, Finished, or Recoverable Waste.
/// </summary>
public class ProductionOutput
{
    public int Id { get; set; }

    /// <summary>
    /// Unique Output Identifier (e.g. OUT-2026-0001)
    /// </summary>
    public string OutputNumber { get; set; } = string.Empty;

    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    public int ProductionBatchId { get; set; }
    public ProductionBatch? ProductionBatch { get; set; }

    public int? ProductionReferenceNumber { get; set; }

    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Output Classification:
    /// SEMI_FINISHED (منتج نصف مصنع)
    /// FINISHED (منتج تام الصنع)
    /// RECOVERABLE_WASTE (مخلفات قابلة للاستفادة / منتج ثانوي)
    /// </summary>
    public string Classification { get; set; } = "FINISHED";

    /// <summary>
    /// Actual measured quantity produced (TON)
    /// </summary>
    public decimal Quantity { get; set; }

    public string Unit { get; set; } = "طن";

    /// <summary>
    /// Production stage where this output was produced
    /// </summary>
    public string ProductionStage { get; set; } = string.Empty;

    public DateTime OutputDate { get; set; } = DateTime.UtcNow;

    public string ResponsibleEmployee { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }

    /// <summary>
    /// For SEMI_FINISHED: next intended production stage or destination
    /// </summary>
    public string? NextStage { get; set; }

    /// <summary>
    /// Destination Storage Location for receiving
    /// </summary>
    public int? DestinationStorageLocationId { get; set; }
    public StorageLocation? DestinationStorageLocation { get; set; }
    public string? DestinationStorageLocationName { get; set; }

    /// <summary>
    /// Laboratory Conformity Status (for Finished products):
    /// PENDING_INSPECTION (بانتظار الفحص النهائي) | CONFORMS (مطابق للمواصفات) | 
    /// NON_CONFORMING (غير مطابق) | NOT_APPLICABLE (لا ينطبق)
    /// </summary>
    public string LaboratoryConformityStatus { get; set; } = "PENDING_INSPECTION";

    /// <summary>
    /// Linked Laboratory Final Product Conformity Certificate ID
    /// </summary>
    public int? LaboratoryCertificateId { get; set; }
    public LaboratoryFinalConformityCertificate? LaboratoryCertificate { get; set; }

    /// <summary>
    /// Has this output been received and posted to warehouse inventory by Storage?
    /// </summary>
    public bool IsStorageReceived { get; set; }
    public decimal StorageReceivedQuantity { get; set; }
    public DateTime? StorageReceivedAt { get; set; }
    public string? StorageReceivedBy { get; set; }
    public int? StorageOperationId { get; set; }
    public StorageOperation? StorageOperation { get; set; }

    /// <summary>
    /// If NON_CONFORMING:
    /// REWORK_REMANUFACTURE | CLASSIFIED_PRODUCT | PENDING_DECISION
    /// </summary>
    public string? NonConformityAction { get; set; }

    public bool IsReworkCreated { get; set; }
    public int? ReworkOrderId { get; set; }

    public string? Notes { get; set; }

    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFileType { get; set; }
    public long AttachmentFileSize { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Traceable Rework and Remanufacturing Order (أمر إعادة التشغيل والتصنيع)
/// Created when a finished product fails Laboratory conformity and is returned for rework.
/// </summary>
public class ProductionRework
{
    public int Id { get; set; }

    /// <summary>
    /// Unique Rework Identifier (e.g. RWK-2026-0001)
    /// </summary>
    public string ReworkNumber { get; set; } = string.Empty;

    public int OriginalProductionOrderId { get; set; }
    public int OriginalProductionBatchId { get; set; }
    public int OriginalProductionOutputId { get; set; }

    public int? ProductionReferenceNumber { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// PENDING | IN_PROGRESS | COMPLETED | CANCELLED
    /// </summary>
    public string Status { get; set; } = "PENDING";

    public int? NewProductionOrderId { get; set; }
    public int? NewProductionBatchId { get; set; }

    public string ResponsibleEmployee { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
