using System;

namespace FaceStone.Web.Models;

/// <summary>
/// Architecture for CEO Dashboard Payment Records.
/// Allows multiple partial/full payments per Purchase Order with decimal precision.
/// </summary>
public class FinancePayment
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign Key to PurchaseOrder (optional if PO number is direct link)
    /// </summary>
    public int? PurchaseOrderId { get; set; }

    /// <summary>
    /// رقم أمر الشراء (PO)
    /// </summary>
    public string PONumber { get; set; } = string.Empty;

    /// <summary>
    /// المبلغ المدفوع (ج.م) - Payment amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// تاريخ السداد - Payment date
    /// </summary>
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// طريقة السداد (تحويل بنكي / شيك / نقدي) - Payment method
    /// </summary>
    public string PaymentMethod { get; set; } = "Bank Transfer";

    /// <summary>
    /// رقم الإيصال / الشيك / العملية البنكية
    /// </summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>
    /// مسجل الدفعة (مثلاً: CEO - Kareem Hassan)
    /// </summary>
    public string? PaidBy { get; set; }

    /// <summary>
    /// ملاحظات الدفع
    /// </summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PurchaseOrder? PurchaseOrder { get; set; }
}
