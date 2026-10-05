using System;

namespace FaceStone.Web.Models;

/// <summary>
/// Employee attendance record model (حضور وانصراف الموظفين).
/// </summary>
public class AttendanceRecord
{
    public int Id { get; set; }

    /// <summary>
    /// المعرف الفريد للموظف في جدول Users
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// تاريخ اليوم
    /// </summary>
    public DateTime Date { get; set; } = DateTime.UtcNow.Date;

    /// <summary>
    /// الحالة: Present (حاضر) / Absent (غائب) / Leave (إجازة رسمية / مرضية)
    /// </summary>
    public string Status { get; set; } = "Present";

    /// <summary>
    /// وقت الحضور
    /// </summary>
    public DateTime? CheckInTime { get; set; }

    /// <summary>
    /// وقت الانصراف
    /// </summary>
    public DateTime? CheckOutTime { get; set; }

    /// <summary>
    /// ملاحظات إضافية (تأخير، إذن، إلخ)
    /// </summary>
    public string? Notes { get; set; }

    public UserAccount? User { get; set; }
}
