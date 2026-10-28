using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models;

/// <summary>
/// Represents a batch submission of employee attendance records for a specific date,
/// submitted for Control & Monitoring review and approval.
/// </summary>
public class AttendanceSubmission
{
    public int Id { get; set; }

    /// <summary>
    /// The date for which attendance was recorded (date part only).
    /// </summary>
    public DateTime AttendanceDate { get; set; }

    /// <summary>
    /// Submitting user ID.
    /// </summary>
    public Guid? SubmittedByUserId { get; set; }

    /// <summary>
    /// Submitting user full name.
    /// </summary>
    public string SubmittedBy { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of submission.
    /// </summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Approval status: PENDING, APPROVED, REJECTED.
    /// </summary>
    public string ApprovalStatus { get; set; } = "PENDING";

    /// <summary>
    /// Reviewer user ID in Control & Monitoring.
    /// </summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>
    /// Reviewer name in Control & Monitoring.
    /// </summary>
    public string? ReviewedBy { get; set; }

    /// <summary>
    /// UTC timestamp of review decision.
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Rejection reason if rejected by Control & Monitoring.
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Optional submission notes or department comments.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Total number of employees in this sheet.
    /// </summary>
    public int TotalEmployeesCount { get; set; }

    /// <summary>
    /// Count of employees marked Present.
    /// </summary>
    public int PresentCount { get; set; }

    /// <summary>
    /// Count of employees marked Absent.
    /// </summary>
    public int AbsentCount { get; set; }

    /// <summary>
    /// Individual attendance records for each employee.
    /// </summary>
    public ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();

    /// <summary>
    /// Supporting evidence attachments (signed registers, biometric sheets, etc.).
    /// </summary>
    public ICollection<AttendanceAttachment> Attachments { get; set; } = new List<AttendanceAttachment>();
}
