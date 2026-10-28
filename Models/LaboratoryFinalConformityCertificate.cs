using System;

namespace FaceStone.Web.Models;

/// <summary>
/// شهادة مطابقة المنتج النهائي (Card 2 in Laboratory)
/// Evaluates finished products, calculates difference from input, logs waste classification, and manages nonconformities.
/// </summary>
public class LaboratoryFinalConformityCertificate
{
    public int Id { get; set; }

    /// <summary>
    /// كود / رقم الشهادة التسلسلي (مثال: CERT-2026-0001)
    /// </summary>
    public string CertificateNumber { get; set; } = string.Empty;

    /// <summary>
    /// الرقم المرجعي التتبعي للإنتاج المشترك
    /// </summary>
    public int ProductionReferenceNumber { get; set; }

    public int? RawMaterialReportId { get; set; }
    public LaboratoryRawMaterialReport? RawMaterialReport { get; set; }

    /// <summary>
    /// قسم المعمل: PHYSICAL | CHEMICAL
    /// </summary>
    public string LaboratorySection { get; set; } = "PHYSICAL";

    /// <summary>
    /// اسم المنتج النهائي
    /// </summary>
    public string FinalProductName { get; set; } = string.Empty;

    /// <summary>
    /// الكمية الفعلية للمنتج النهائي (بالطن)
    /// </summary>
    public decimal ActualFinalProductQuantity { get; set; }

    /// <summary>
    /// وحدة القياس (افتراضياً: طن)
    /// </summary>
    public string QuantityUnit { get; set; } = "طن";

    /// <summary>
    /// رقم تشغيلة أو حركة إنتاج المنتج
    /// </summary>
    public string ProductionBatchNumber { get; set; } = string.Empty;

    public int? ProductionWorkInProcessId { get; set; }
    public ProductionWorkInProcess? ProductionWorkInProcess { get; set; }

    /// <summary>
    /// تاريخ ووقت اكتمال الإنتاج
    /// </summary>
    public DateTime ProductionCompletionDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// كمية المدخلات الفعلية من المواد الخام الصادرة للتشغيلة (بالطن)
    /// </summary>
    public decimal ActualRawMaterialInputQuantity { get; set; }

    /// <summary>
    /// الفارق المحسوب = المدخلات الفعلية - المخرجات الفعلية (بالطن)
    /// </summary>
    public decimal CalculatedDifference { get; set; }

    /// <summary>
    /// الهالك الفعلي المقاس (إن وجد بشكل منفصل)
    /// </summary>
    public decimal? ActualMeasuredWaste { get; set; }

    /// <summary>
    /// تصنيف الهالك والمخرجات المقاسة:
    /// ORDINARY_WASTE (هالك عادي غير قابل للاستخدام)
    /// RECOVERABLE_MATERIAL (مادة مستردة / إعادة تدوير)
    /// CLASSIFIED_PRODUCT (منتج مصنف / درجة ثانية)
    /// </summary>
    public string WasteClassification { get; set; } = "ORDINARY_WASTE";

    /// <summary>
    /// قرار مطابقة المنتج النهائي:
    /// CONFORMS (مطابق للمواصفات)
    /// NON_CONFORMING (غير مطابق للمواصفات)
    /// PENDING_TESTING (يحتاج إلى فحوصات إضافية)
    /// </summary>
    public string ConformityDecision { get; set; } = "CONFORMS";

    /// <summary>
    /// الإجراء المتخذ عند عدم المطابقة:
    /// CLASSIFIED_PRODUCT (تخزين كمنتج مصنف / درجة ثانية)
    /// REWORK_REMANUFACTURE (إعادة تشغيل وتصنيع)
    /// PENDING_DECISION (معلق قيد القرار / حجر فني)
    /// </summary>
    public string? NonConformityAction { get; set; }

    /// <summary>
    /// تصنيف المنتج عند تخزينه كمنتج مصنف (منتج وسيط / درجة ثانية / مادة مستردة)
    /// </summary>
    public string? ClassifiedProductType { get; set; }

    /// <summary>
    /// تعليمات إعادة التصنيع والتشغيل
    /// </summary>
    public string? ReworkInstructions { get; set; }

    /// <summary>
    /// نتائج الفحوصات المعملية واشتراطات المطابقة (JSON)
    /// </summary>
    public string? SpecificationResultsJson { get; set; }

    public string? Observations { get; set; }

    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFileType { get; set; }
    public long AttachmentFileSize { get; set; }

    public string ResponsibleEmployee { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }

    /// <summary>
    /// حالة المراجعة والاعتماد بقسم التحكم والمراقبة: PENDING | APPROVED | REJECTED
    /// </summary>
    public string ApprovalStatus { get; set; } = "PENDING";
    public string? ReviewedBy { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>
    /// هل تم ترحيل المنتج النهائي المعتمد لمستودع البضاعة الجاهزة؟
    /// </summary>
    public bool IsStoragePosted { get; set; }
    public DateTime? StoragePostedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
