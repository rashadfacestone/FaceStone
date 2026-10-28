using System;

namespace FaceStone.Web.Models;

/// <summary>
/// Supporting evidence attachment associated with an Attendance submission
/// (e.g. signed physical attendance register, exported fingerprint log, supervisor signed sheet).
/// </summary>
public class AttendanceAttachment
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to AttendanceSubmission.
    /// </summary>
    public int AttendanceSubmissionId { get; set; }

    /// <summary>
    /// Generated unique filename on disk.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Original file name uploaded by user.
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Relative file path on server.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Content / MIME type (application/pdf, image/jpeg, image/png).
    /// </summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// UTC timestamp of upload.
    /// </summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to parent submission.
    /// </summary>
    public AttendanceSubmission? AttendanceSubmission { get; set; }
}
