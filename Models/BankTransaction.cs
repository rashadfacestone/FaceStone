using System;

namespace FaceStone.Web.Models;

/// <summary>
/// Model for manual Bank Transactions entered by the Finance Department.
/// Represents deposits, transfers, supplier settlements, and collections.
/// </summary>
public class BankTransaction
{
    public int Id { get; set; }

    /// <summary>
    /// تاريخ العملية الفعلي
    /// </summary>
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// نوع المعاملة البنكية: إيداع / تحويل بنكي / شيك / سداد مورد / تحصيل
    /// </summary>
    public string TransactionType { get; set; } = "تحويل بنكي";

    /// <summary>
    /// الاتجاه: In (إيداع / Received) أو Out (صرف / Sent)
    /// </summary>
    public string Direction { get; set; } = "In";

    /// <summary>
    /// قيمة المعاملة بالجنيه المصري (قيمة عشرية موجبة)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// اسم البنك (مثل: البنك التجاري الدولي CIB، البنك الأهلي المصري، بنك مصر، QNB)
    /// </summary>
    public string BankName { get; set; } = "CIB";

    /// <summary>
    /// رقم المرجع أو رقم الإيصال / الشيك البنكي
    /// </summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>
    /// البيان / الوصف المختصر للمعاملة
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// اسم المستخدم أو المسؤول الذي سجل العملية
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
