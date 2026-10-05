using System;

namespace FaceStone.Web.Models;

public class PurchaseOrderPaymentAudit
{
    public int Id { get; set; }

    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public string PONumber { get; set; } = string.Empty;

    public string PreviousStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public string ChangedBy { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public string? Notes { get; set; }
}
