using System;

namespace FaceStone.Web.Models;

/// <summary>
/// Operational Expenses Model for factory costs outside PO/Raw Materials.
/// Tracks electricity, fuel, maintenance, transportation, salaries, office expenses, equipment, etc.
/// </summary>
public class Expense
{
    public int Id { get; set; }

    /// <summary>
    /// تاريخ المصروف
    /// </summary>
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// تصنيف المصروف (كهرباء، وقود ومحروقات، صيانة ومعدات، نقل ولوجستيات، رواتب وبدلات، مصاريف إدارية، أدوات ومهمات، أخرى)
    /// </summary>
    public string Category { get; set; } = "أخرى";

    /// <summary>
    /// قيمة المصروف بالجنيه المصري
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// البيان / موضوع النفقة
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// المستفيد / الجهة المتلقية للمصروف
    /// </summary>
    public string Beneficiary { get; set; } = string.Empty;

    /// <summary>
    /// رقم المرجع أو رقم الفاتورة / الإيصال
    /// </summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>
    /// طريقة الدفع: تحويل بنكي / شيك / نقدي
    /// </summary>
    public string PaymentMethod { get; set; } = "Bank Transfer";

    /// <summary>
    /// المستخدم الذي سجل المصروف
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// معرف المستخدم
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// ملاحظات إضافية
    /// </summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
