using System;

namespace FaceStone.Web.Models;

/// <summary>
/// Production Work-in-Process (WIP) entity establishing the foundation for tracking
/// transferred raw materials, manufacturing consumption, production batches, and waste
/// </summary>
public class ProductionWorkInProcess
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to originating Storage Operation (Transfer)
    /// </summary>
    public int StorageOperationId { get; set; }
    public StorageOperation? StorageOperation { get; set; }

    /// <summary>
    /// Foreign key to originating transfer line item
    /// </summary>
    public int StorageOperationItemId { get; set; }
    public StorageOperationItem? StorageOperationItem { get; set; }

    /// <summary>
    /// Foreign key to transferred raw material
    /// </summary>
    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    /// <summary>
    /// الرقم المرجعي التتبعي للإنتاج المشترك الصادر من قسم المعمل
    /// </summary>
    public int? ProductionReferenceNumber { get; set; }

    /// <summary>
    /// Unique production transfer / batch tracking identifier (e.g. TRF-2026-0001-L1)
    /// </summary>
    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>
    /// Raw material quantity issued to production (TON)
    /// </summary>
    public decimal MaterialQuantityIssued { get; set; }

    /// <summary>
    /// Source warehouse storage location
    /// </summary>
    public string SourceStorageLocation { get; set; } = string.Empty;

    /// <summary>
    /// Transfer timestamp
    /// </summary>
    public DateTime TransferDate { get; set; }

    /// <summary>
    /// Status: قيد التشغيل (In Progress) | بانتظار بيانات الإنتاج (Awaiting Production Data) | مكتمل (Completed)
    /// </summary>
    public string Status { get; set; } = "قيد التشغيل";

    /// <summary>
    /// Final product name when recorded (e.g. خرسانة جاهزة رتبة C35)
    /// </summary>
    public string? FinalProductName { get; set; }

    /// <summary>
    /// Final product output quantity (TON)
    /// </summary>
    public decimal? FinalProductQuantity { get; set; }

    /// <summary>
    /// Waste quantity = MaterialQuantityIssued - FinalProductQuantity (TON)
    /// </summary>
    public decimal? WasteQuantity { get; set; }

    /// <summary>
    /// Waste percentage = (WasteQuantity / MaterialQuantityIssued) * 100
    /// </summary>
    public decimal? WastePercentage { get; set; }

    // =========================================================================
    // WASTE CLASSIFICATION & RECOVERABLE PRODUCT (تسجيل الهالك كمنتج نهائي أو مادة مستردة)
    // =========================================================================
    /// <summary>
    /// تصنيف الهالك والمخرجات:
    /// ORDINARY_WASTE (هالك عادي غير قابل للاستخدام)
    /// RECOVERABLE_MATERIAL (مادة مستردة / إعادة تدوير)
    /// FINAL_PRODUCT (منتج نهائي معتمد)
    /// </summary>
    public string WasteClassification { get; set; } = "ORDINARY_WASTE";

    /// <summary>
    /// اسم المادة المستردة أو المنتج النهائي المسجل من الهالك
    /// </summary>
    public string? RecoverableProductName { get; set; }

    /// <summary>
    /// ربط بكارت الصنف المعتمد للمادة المستردة (إن وجد)
    /// </summary>
    public int? RecoverableMaterialId { get; set; }
    public RawMaterial? RecoverableMaterial { get; set; }

    /// <summary>
    /// الكمية الفعلية للمادة المستردة أو المنتج النهائي من الهالك (بالطن)
    /// </summary>
    public decimal? RecoverableQuantity { get; set; }

    /// <summary>
    /// وحدة القياس (طن)
    /// </summary>
    public string RecoverableUnit { get; set; } = "طن";

    /// <summary>
    /// مكان التخزين المستقبل للمادة المستردة
    /// </summary>
    public int? DestinationStorageLocationId { get; set; }
    public StorageLocation? DestinationStorageLocation { get; set; }
    public string? DestinationStorageLocationName { get; set; }

    /// <summary>
    /// هل تم ترحيل المادة المستردة / المنتج إلى رصيد المستودع؟
    /// </summary>
    public bool IsRecoveredStockPosted { get; set; }
    public DateTime? RecoveredStockPostedAt { get; set; }

    /// <summary>
    /// الموظف المسؤول عن تصنيف واستلام المخرجات
    /// </summary>
    public string? OutputResponsibleEmployee { get; set; }

    /// <summary>
    /// سجل تدقيق لتاريخ تغييرات التصنيف
    /// </summary>
    public string? ClassificationHistory { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }
}
