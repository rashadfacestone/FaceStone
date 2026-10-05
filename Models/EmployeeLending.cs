using System;

namespace FaceStone.Web.Models;

/// <summary>
/// Staff loans and financial advances (سلف الموظفين).
/// </summary>
public class EmployeeLending
{
    public int Id { get; set; }

    /// <summary>
    /// المعرف الفريد للموظف في جدول Users
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// إجمالي قيمة السلفة المطلوبة
    /// </summary>
    public decimal LendingAmount { get; set; }

    /// <summary>
    /// إجمالي المسدد من السلفة حتى الآن
    /// </summary>
    public decimal PaidAmount { get; set; }

    /// <summary>
    /// المتبقي من السلفة = إجمالي السلفة − المسدد
    /// </summary>
    public decimal RemainingAmount { get; set; }

    /// <summary>
    /// تاريخ طلب السلفة أو تاريخ الصرف
    /// </summary>
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// حالة السلفة: معتمد / قيد المراجعة / مسدد بالكامل / مرفوض
    /// </summary>
    public string ApprovalStatus { get; set; } = "معتمد";

    /// <summary>
    /// اسم المسؤول المعتمد للسلفة
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// بيان أو سبب السلفة / خطة الخصم
    /// </summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserAccount? User { get; set; }
}
