using System;

namespace FaceStone.Web.Models;

/// <summary>
/// Employee attendance record model (حضور وانصراف الموظفين).
/// </summary>
public class AttendanceRecord
{
    public int Id { get; set; }

    /// <summary>
    /// المعرف الفريد للموظف (اختياري لربط المستخدمين في جدول Users مستقبلاً)
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// اسم الموظف
    /// </summary>
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>
    /// الدور الوظيفي في النظام
    /// </summary>
    public string EmployeeRole { get; set; } = string.Empty;

    /// <summary>
    /// علامة تمييز البيانات التجريبية
    /// </summary>
    public bool IsTestData { get; set; } = true;

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

    /// <summary>
    /// معرف إرسالية الحضور المرتبطة (Batch Submission)
    /// </summary>
    public int? AttendanceSubmissionId { get; set; }

    /// <summary>
    /// حالة الاعتماد: PENDING / APPROVED / REJECTED
    /// </summary>
    public string ApprovalStatus { get; set; } = "PENDING";

    /// <summary>
    /// سبب الرفض في حال رفض هذا السجل الفردي من قبل قسم المراقبة والتحكم
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// معرف مراجع السجل في قسم المراقبة والتحكم
    /// </summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>
    /// اسم مراجع السجل في قسم المراقبة والتحكم
    /// </summary>
    public string? ReviewedBy { get; set; }

    /// <summary>
    /// تاريخ ووقت اتخاذ القرار للمراجعة الفردية
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// تاريخ إنشاء السجل
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserAccount? User { get; set; }
    public AttendanceSubmission? AttendanceSubmission { get; set; }
}
