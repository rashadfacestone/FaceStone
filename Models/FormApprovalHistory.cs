using System;

namespace FaceStone.Web.Models;

public class FormApprovalHistory
{
    public int Id { get; set; }

    /// <summary>
    /// Type of form: "Transportation" or "Storage"
    /// </summary>
    public string FormType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the entity in Transportation or StorageTransactions table
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// Associated Purchase Order number if available
    /// </summary>
    public string? PONumber { get; set; }

    /// <summary>
    /// Sequential transaction number or storage movement number
    /// </summary>
    public string? TransactionOrMovementNumber { get; set; }

    /// <summary>
    /// Previous status before this action
    /// </summary>
    public string PreviousStatus { get; set; } = string.Empty;

    /// <summary>
    /// New status: PENDING | APPROVED | REJECTED
    /// </summary>
    public string NewStatus { get; set; } = string.Empty;

    /// <summary>
    /// Action: SUBMITTED | APPROVED | REJECTED | RESUBMITTED
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Reviewer or Submitter display name
    /// </summary>
    public string? ActorName { get; set; }

    public Guid? ActorUserId { get; set; }

    public string? ActorRole { get; set; }

    /// <summary>
    /// Rejection reason or review note
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Timestamp of this audit event
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
