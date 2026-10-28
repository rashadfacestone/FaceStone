using System;

namespace FaceStone.Web.Models;

/// <summary>
/// حركة في كارت الصنف - Storage Item Card Transaction
/// </summary>
public class StorageItemTransaction
{
    public int Id { get; set; }

    /// <summary>
    /// معرف الصنف
    /// </summary>
    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    /// <summary>
    /// تاريخ ووقت الحركة
    /// </summary>
    public DateTime TransactionDate { get; set; }

    /// <summary>
    /// نوع الحركة: Incoming (وارد) | Outgoing (منصرف) | OpeningStock (رصيد افتتاحي)
    /// </summary>
    public string TransactionType { get; set; } = "Incoming";

    /// <summary>
    /// البيان: علاء | اسلام | محمود (أو رصيد افتتاحي)
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// الكمية
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// سعر الوحدة
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// الإجمالي = الكمية × سعر الوحدة
    /// </summary>
    public decimal TotalValue { get; set; }

    /// <summary>
    /// نوع الصرف (للحركات الصادرة): داخلي (Internal) | خارجي (External)
    /// </summary>
    public string? DestinationType { get; set; }

    /// <summary>
    /// جهة الصرف: المصنع (في حالة الداخلي) أو الجهة الخارجية المحددة (في حالة الخارجي)
    /// </summary>
    public string? Destination { get; set; }

    /// <summary>
    /// معرف حركة النقل المربوطة من قسم النقل (في حالة التوريد المعتمد)
    /// </summary>
    public int? SourceTransportationId { get; set; }
    public Transportation? SourceTransportation { get; set; }

    /// <summary>
    /// رقم حركة النقل المعتمدة
    /// </summary>
    public int? SourceTransportationNumber { get; set; }

    /// <summary>
    /// رصيد الكمية المحسوب بعد هذه الحركة
    /// </summary>
    public decimal RunningQuantityBalance { get; set; }

    /// <summary>
    /// رصيد القيمة المحسوب بعد هذه الحركة
    /// </summary>
    public decimal RunningValueBalance { get; set; }

    /// <summary>
    /// ملاحظات إضافية
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// تاريخ تسجيل السجل
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// المستخدم الذي سجل الحركة
    /// </summary>
    public string? CreatedBy { get; set; }

    public Guid? CreatedByUserId { get; set; }
}
