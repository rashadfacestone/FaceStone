using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models.DTOs;

public class AttendanceEmployeeItemDto
{
    public int? RecordId { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "Present", "Absent", or ""
    public bool IsTestData { get; set; } = true;
    public DateTime? AttendanceDateTime { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? Notes { get; set; }
    public string ApprovalStatus { get; set; } = "PENDING";
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class AttendanceRecordItemDto
{
    public int? Id { get; set; }
    public Guid? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = "Present"; // "Present" or "Absent"
    public bool IsTestData { get; set; } = true;
    public DateTime? AttendanceDateTime { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? Notes { get; set; }
    public string ApprovalStatus { get; set; } = "PENDING"; // "PENDING", "APPROVED", "REJECTED"
    public string? ReviewedBy { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class SubmitAttendanceRequestDto
{
    public DateTime AttendanceDate { get; set; }
    public string? Notes { get; set; }
    public List<AttendanceRecordItemDto> Records { get; set; } = new();
}

public class AttendanceAttachmentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    public string ViewUrl { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}

public class AttendanceSubmissionDetailDto
{
    public int Id { get; set; }
    public DateTime AttendanceDate { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public string SubmittedBy { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public string ApprovalStatus { get; set; } = "PENDING";
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }
    public int TotalEmployeesCount { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public List<AttendanceRecordItemDto> Records { get; set; } = new();
    public List<AttendanceAttachmentDto> Attachments { get; set; } = new();
    public List<FormApprovalHistoryDto> AuditHistory { get; set; } = new();
}
