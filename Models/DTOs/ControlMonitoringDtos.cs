using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models.DTOs;

public class ControlMonitoringDashboardStatsDto
{
    public int PendingReviewsCount { get; set; }
    public int TransportationPendingCount { get; set; }
    public int StoragePendingCount { get; set; }
    public int ApprovedTodayCount { get; set; }
    public int RejectedTodayCount { get; set; }
}

public class FormAttachmentDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? FileType { get; set; }
    public long FileSize { get; set; }
    public string ViewUrl { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string? UploadedBy { get; set; }
    public DateTime? UploadDate { get; set; }
}

public class ControlMonitoringReviewItemDto
{
    public int Id { get; set; }
    public string FormType { get; set; } = string.Empty; // "Transportation" | "Storage"
    public int FormId { get; set; }
    public string? PONumber { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string SubmittedBy { get; set; } = string.Empty;
    public Guid? SubmittedByUserId { get; set; }
    public DateTime SubmissionDate { get; set; }
    public string Status { get; set; } = "PENDING"; // "PENDING" | "APPROVED" | "REJECTED"
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }

    public int AttachmentCount { get; set; }
    public List<FormAttachmentDto> Attachments { get; set; } = new();

    // Audit Trail
    public List<FormApprovalHistoryDto> AuditTrail { get; set; } = new();

    // =========================================================================
    // Transportation specific fields
    // =========================================================================
    public string? CarNumber { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string? Source { get; set; }
    public string? Destination { get; set; }
    public string? CompanyName { get; set; }
    public string? MaterialType { get; set; }
    public decimal? ExpectedWeight { get; set; }
    public decimal? WeightBeforeLoading { get; set; }
    public decimal? WeightWhenEnteringFactory { get; set; }
    public decimal? WeightWhenExitingFactory { get; set; }
    public decimal? InternalWeight { get; set; }
    public decimal? ExternalLoadedWeight { get; set; }
    public decimal? ExternalEmptyWeight { get; set; }
    public decimal? ExternalNetWeight { get; set; }
    public decimal? NetWeight { get; set; }
    public decimal? WasteQuantity { get; set; }
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public int? ShippingDurationMinutes { get; set; }
    public string? ShippingSupervisor { get; set; }
    public string? DepartmentSupervisor { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientTitle { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public string? InternalWeighbridgeCode { get; set; }
    public string? ExternalWeighbridgeCode { get; set; }
    public DateTime? InternalEmptyWeightTime { get; set; }
    public DateTime? InternalLoadedWeightTime { get; set; }
    public DateTime? ExternalEmptyWeightTime { get; set; }
    public DateTime? ExternalLoadedWeightTime { get; set; }
    public decimal? Ikramiya { get; set; }
    public decimal? CustodyAmount { get; set; }
    public string? ReceivingStatus { get; set; }
    public string? TransactionStatus { get; set; }

    // =========================================================================
    // Storage specific fields
    // =========================================================================
    public int? ReferenceNumber { get; set; }
    public bool? IsExportRelatedToPO { get; set; }
    public string? Material { get; set; }
    public string? Action { get; set; }
    public string? ServiceType { get; set; }
    public decimal? IncomingQuantity { get; set; }
    public decimal? OutgoingQuantity { get; set; }
    public decimal? OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }
    public string? StorageLocation { get; set; }
    public string? DestinationStorageLocation { get; set; }
    public string? InternalWeighbridgeReference { get; set; }
    public decimal? StorageInternalLoadedWeight { get; set; }
    public decimal? StorageInternalEmptyWeight { get; set; }
    public decimal? StorageInternalNetWeight { get; set; }
    public DateTime? StorageInternalEmptyWeightTime { get; set; }
    public DateTime? StorageInternalLoadedWeightTime { get; set; }
    public bool? HasExternalWeighbridge { get; set; }
    public string? ExternalWeighbridgeReference { get; set; }
    public decimal? StorageExternalLoadedWeight { get; set; }
    public decimal? StorageExternalEmptyWeight { get; set; }
    public decimal? StorageExternalNetWeight { get; set; }
    public DateTime? StorageExternalEmptyWeightTime { get; set; }
    public DateTime? StorageExternalLoadedWeightTime { get; set; }
    public string? ResponsibleEmployee { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }

    // Notes
    public string? Notes { get; set; }
}

public class FormApprovalHistoryDto
{
    public int Id { get; set; }
    public string FormType { get; set; } = string.Empty;
    public int FormId { get; set; }
    public string? PONumber { get; set; }
    public string? TransactionOrMovementNumber { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? ActorName { get; set; }
    public string? ActorRole { get; set; }
    public string? Comment { get; set; }
    public DateTime Timestamp { get; set; }
}

public class RejectDecisionDto
{
    public string? RejectionReason { get; set; }
    public string? Reason { get; set; }

    public string? GetReason() => !string.IsNullOrWhiteSpace(RejectionReason) ? RejectionReason : Reason;
}

public class ResubmitDecisionDto
{
    public string? Notes { get; set; }
    public string? Comment { get; set; }

    public string? GetComment() => !string.IsNullOrWhiteSpace(Comment) ? Comment : Notes;
}
