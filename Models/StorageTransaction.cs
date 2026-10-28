using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models;

public class StorageTransaction
{
    public int Id { get; set; }

    /// <summary>
    /// رقم الحركة - Storage movement / transaction number
    /// </summary>
    public int MovementNumber { get; set; }

    /// <summary>
    /// تاريخ ووقت الحركة - Date and time of storage movement
    /// </summary>
    public DateTime TransactionDate { get; set; }

    /// <summary>
    /// نوع الخامة - Material: powder | slurry
    /// </summary>
    public string Material { get; set; } = "powder";

    /// <summary>
    /// نوع الحركة - Action: Import | Export (إيداع | صرف)
    /// </summary>
    public string Action { get; set; } = "Import";

    /// <summary>
    /// نوع الخدمة - Backward compatible field: إيداع | صرف | إضافة | تحويل | تسوية
    /// </summary>
    public string ServiceType { get; set; } = "إيداع";

    /// <summary>
    /// رقم المرجع - Unique reference number preserved for legacy compatibility
    /// </summary>
    public int ReferenceNumber { get; set; }

    /// <summary>
    /// هل الصادر مربوط بأمر شراء؟
    /// </summary>
    public bool IsExportRelatedToPO { get; set; }

    /// <summary>
    /// رقم أمر الشراء (PO)
    /// </summary>
    public string? PONumber { get; set; }

    /// <summary>
    /// Foreign Key to PurchaseOrders table
    /// </summary>
    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    /// <summary>
    /// الكمية الواردة (TON) - Required when Action is Import
    /// </summary>
    public decimal IncomingQuantity { get; set; }

    /// <summary>
    /// الكمية الصادرة (TON) - Required when Action is Export
    /// </summary>
    public decimal OutgoingQuantity { get; set; }

    /// <summary>
    /// الرصيد الافتتاحي - Opening inventory balance
    /// </summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>
    /// الرصيد الختامي - Closing inventory balance
    /// </summary>
    public decimal ClosingBalance { get; set; }

    /// <summary>
    /// مكان التخزين - Storage location
    /// </summary>
    public string StorageLocation { get; set; } = "مستودع الخامات الرئيسي";

    /// <summary>
    /// مكان التخزين المحول إليه
    /// </summary>
    public string? DestinationStorageLocation { get; set; }

    // =========================================================================
    // SECTION 3: WEIGHBRIDGE (الميزان)
    // =========================================================================

    // الميزان الداخلي (Internal Weighbridge)
    /// <summary>
    /// رقم المرجع للميزان الداخلي (Manual Entry by Employee)
    /// </summary>
    public string InternalWeighbridgeReference { get; set; } = string.Empty;

    /// <summary>
    /// الوزن الداخلي قائم (Loaded Weight)
    /// </summary>
    public decimal InternalLoadedWeight { get; set; }

    /// <summary>
    /// الوزن الداخلي فارغ (Empty Weight)
    /// </summary>
    public decimal InternalEmptyWeight { get; set; }

    /// <summary>
    /// الوزن الداخلي الصافي = القائم - الفارغ (Auto-calculated)
    /// </summary>
    public decimal InternalNetWeight { get; set; }

    /// <summary>
    /// تاريخ ووقت الوزن الفارغ الداخلي
    /// </summary>
    public DateTime? InternalEmptyWeightTime { get; set; }

    /// <summary>
    /// تاريخ ووقت الوزن القائم الداخلي
    /// </summary>
    public DateTime? InternalLoadedWeightTime { get; set; }

    // الميزان الخارجي (External Weighbridge)
    /// <summary>
    /// هل يوجد وزن خارجي؟
    /// </summary>
    public bool HasExternalWeighbridge { get; set; }

    /// <summary>
    /// رقم المرجع للميزان الخارجي
    /// </summary>
    public string? ExternalWeighbridgeReference { get; set; }

    /// <summary>
    /// الوزن الخارجي قائم
    /// </summary>
    public decimal? ExternalLoadedWeight { get; set; }

    /// <summary>
    /// الوزن الخارجي فارغ
    /// </summary>
    public decimal? ExternalEmptyWeight { get; set; }

    /// <summary>
    /// الوزن الخارجي الصافي = القائم - الفارغ (Auto-calculated)
    /// </summary>
    public decimal? ExternalNetWeight { get; set; }

    /// <summary>
    /// تاريخ ووقت الوزن الفارغ الخارجي
    /// </summary>
    public DateTime? ExternalEmptyWeightTime { get; set; }

    /// <summary>
    /// تاريخ ووقت الوزن القائم الخارجي
    /// </summary>
    public DateTime? ExternalLoadedWeightTime { get; set; }

    // إيصال الميزان (Weighbridge Receipt Attachment)
    /// <summary>
    /// مسار مرفق إيصال الميزان (PDF/JPG/JPEG)
    /// </summary>
    public string? WeighbridgeAttachmentPath { get; set; }

    public string? WeighbridgeFileName { get; set; }

    public string? WeighbridgeFileType { get; set; }

    public long WeighbridgeFileSize { get; set; }

    // =========================================================================
    // SECTION 4: RESPONSIBLE EMPLOYEE (الموظف المسؤول)
    // =========================================================================
    /// <summary>
    /// اسم الموظف المسؤول (Receiving / Recording Employee)
    /// </summary>
    public string ReceivingEmployee { get; set; } = string.Empty;
    public string ResponsibleEmployee => ReceivingEmployee;

    /// <summary>
    /// ملاحظات إضافية
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// سجل بواسطة (User Name / Account)
    /// </summary>
    public string? CreatedBy { get; set; }

    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// وقت الإنشاء
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// حالة المراجعة والاعتماد لقسم التحكم والمراقبة: PENDING | APPROVED | REJECTED
    /// </summary>
    public string ApprovalStatus { get; set; } = "PENDING";

    public string? ReviewedBy { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? RejectionReason { get; set; }

    /// <summary>
    /// Relational navigation: Associated transportation movements
    /// </summary>
    public ICollection<StorageTransactionMovement> TransportationMovements { get; set; } = new List<StorageTransactionMovement>();
}
