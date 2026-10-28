using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models;

/// <summary>
/// Storage Operation Header representing Import (إذن إضافة), Export (إذن صرف), or Transfer (تحويل للإنتاج)
/// </summary>
public class StorageOperation
{
    public int Id { get; set; }

    /// <summary>
    /// IMPORT (إذن إضافة) | EXPORT (إذن صرف) | TRANSFER (تحويل للإنتاج)
    /// </summary>
    public string OperationType { get; set; } = "IMPORT";

    /// <summary>
    /// رقم العملية التسلسلي المنفصل لكل نوع حركة (يبدأ من 1)
    /// </summary>
    public int TransactionNumber { get; set; }

    /// <summary>
    /// تاريخ ووقت العملية
    /// </summary>
    public DateTime TransactionDate { get; set; }

    /// <summary>
    /// مكان التخزين المحدد
    /// </summary>
    public int StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }
    public string StorageLocationName { get; set; } = string.Empty;

    /// <summary>
    /// نوع وتصنيف الخامة: MECHANICAL (خامات ميكانيكية) | CHEMICAL (خامات كيميائية)
    /// </summary>
    public string? MaterialType { get; set; }

    // =========================================================================
    // INTERNAL & EXTERNAL WEIGHING (الميزان الداخلي ومقارنة الأوزان)
    // =========================================================================
    /// <summary>
    /// الوزن المصرح / المسجل من المصدر أو العميل (بالطن)
    /// </summary>
    public decimal? ExternalReportedWeight { get; set; }

    /// <summary>
    /// الوزن الداخلي بالمصنع - شاحنة محملة (قائم) بالطن
    /// </summary>
    public decimal? InternalLoadedWeight { get; set; }

    /// <summary>
    /// الوزن الداخلي بالمصنع - شاحنة فارغة (فارغ) بالطن
    /// </summary>
    public decimal? InternalEmptyWeight { get; set; }

    /// <summary>
    /// الوزن الصافي الداخلي = قائم - فارغ (أو الوزن المقاس داخلياً) بالطن
    /// </summary>
    public decimal? InternalNetWeight { get; set; }

    /// <summary>
    /// تاريخ ووقت قياس الميزان الداخلي
    /// </summary>
    public DateTime? InternalWeighingTime { get; set; }

    /// <summary>
    /// رقم إيصال / مرجع الميزان
    /// </summary>
    public string? WeighbridgeReference { get; set; }

    /// <summary>
    /// الموظف المسؤول عن قياس الميزان الداخلي
    /// </summary>
    public string? WeighingResponsibleEmployee { get; set; }

    /// <summary>
    /// فارق الوزن = |الوزن الصافي الداخلي - الوزن المصرح| بالطن
    /// </summary>
    public decimal? WeightDifference { get; set; }

    /// <summary>
    /// نسبة فارق الوزن المئوية %
    /// </summary>
    public decimal? WeightDifferencePercentage { get; set; }

    /// <summary>
    /// هل تم تحذير العملية بسبب تجاوز نسبة التسامح المسموح بها (0.5%)؟
    /// </summary>
    public bool IsDiscrepancyFlagged { get; set; }

    /// <summary>
    /// سبب وتفسير فارق الوزن
    /// </summary>
    public string? DiscrepancyExplanation { get; set; }

    /// <summary>
    /// ربط بحركة النقل (Transportation) إن وجدت
    /// </summary>
    public int? TransportationId { get; set; }
    public Transportation? Transportation { get; set; }

    /// <summary>
    /// الرقم المرجعي التتبعي للإنتاج المشترك الصادر من قسم المعمل
    /// </summary>
    public int? ProductionReferenceNumber { get; set; }

    // =========================================================================
    // IMPORT SPECIFICS (المورد / المصدر)
    // =========================================================================
    /// <summary>
    /// المورد / المصدر: علاء | اسلام | محمود
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// رقم هاتف المورد / المصدر
    /// </summary>
    public string? SourcePhone { get; set; }

    // =========================================================================
    // EXPORT SPECIFICS (العميل والوجهة)
    // =========================================================================
    public int? ClientId { get; set; }
    public StorageClient? Client { get; set; }
    public string? ClientName { get; set; }

    public int? DestinationId { get; set; }
    public StorageClientDestination? Destination { get; set; }
    public string? DestinationName { get; set; }

    public string? ClientPhone { get; set; }

    // =========================================================================
    // TRANSFER SPECIFICS (التحويل للإنتاج)
    // =========================================================================
    public string? DestinationDepartment { get; set; } = "الإنتاج";

    // =========================================================================
    // ATTACHMENTS (المرفقات والمستندات الثبوتية)
    // =========================================================================
    /// <summary>
    /// مرفق إيصال الميزان (Weighbridge Attachment)
    /// </summary>
    public string? WeighbridgeAttachmentPath { get; set; }
    public string? WeighbridgeFileName { get; set; }
    public string? WeighbridgeFileType { get; set; }
    public long WeighbridgeFileSize { get; set; }

    /// <summary>
    /// مرفق تقرير QOC (QOC Report Attachment)
    /// </summary>
    public string? QocAttachmentPath { get; set; }
    public string? QocFileName { get; set; }
    public string? QocFileType { get; set; }
    public long QocFileSize { get; set; }

    /// <summary>
    /// المستند الورقي الممسوح ضوئياً أو المصور (Written Document Attachment)
    /// </summary>
    public string? DocumentAttachmentPath { get; set; }
    public string? DocumentFileName { get; set; }
    public string? DocumentFileType { get; set; }
    public long DocumentFileSize { get; set; }

    // =========================================================================
    // RESPONSIBLE EMPLOYEE & AUDIT
    // =========================================================================
    /// <summary>
    /// الموظف المسؤول (Derived from authenticated server user)
    /// </summary>
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// حالة الاعتماد والرقابة: PENDING | APPROVED | REJECTED
    /// </summary>
    public string ApprovalStatus { get; set; } = "PENDING";

    public string? ReviewedBy { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>
    /// هل تم ترحيل الكميات لرصيد المخزن الفعلي المعتمد؟
    /// </summary>
    public bool IsInventoryPosted { get; set; }
    public DateTime? InventoryPostedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// بنود الأصناف والكميات المرتبطة بالعملية
    /// </summary>
    public ICollection<StorageOperationItem> Items { get; set; } = new List<StorageOperationItem>();

    /// <summary>
    /// حركات دفتر الأستاذ المخزني المرتبطة
    /// </summary>
    public ICollection<StorageInventoryLedger> LedgerMovements { get; set; } = new List<StorageInventoryLedger>();

    /// <summary>
    /// سجلات أوامر التشغيل والإنتاج المرتبطة بالتحويل
    /// </summary>
    public ICollection<ProductionWorkInProcess> ProductionBatches { get; set; } = new List<ProductionWorkInProcess>();
}
