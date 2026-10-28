using System;

namespace FaceStone.Web.Models;

/// <summary>
/// تقرير نتائج تحليل المنتج أثناء التشغيل (Card 3 in Laboratory)
/// Inspects material or product during production stages under the shared production reference number.
/// </summary>
public class LaboratoryInProcessReport
{
    public int Id { get; set; }

    /// <summary>
    /// الرقم المرجعي التتبعي للإنتاج المشترك
    /// </summary>
    public int ProductionReferenceNumber { get; set; }

    public int? RawMaterialReportId { get; set; }
    public LaboratoryRawMaterialReport? RawMaterialReport { get; set; }

    /// <summary>
    /// رقم تشغيلة الإنتاج / Batch Tracking Number
    /// </summary>
    public string ProductionBatchNumber { get; set; } = string.Empty;

    public int? ProductionWorkInProcessId { get; set; }
    public ProductionWorkInProcess? ProductionWorkInProcess { get; set; }

    /// <summary>
    /// قسم المعمل: PHYSICAL | CHEMICAL
    /// </summary>
    public string LaboratorySection { get; set; } = "PHYSICAL";

    /// <summary>
    /// اسم المنتج أو الخامة قيد التشغيل
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// تاريخ ووقت الفحص
    /// </summary>
    public DateTime TestDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// مرحلة الإنتاج المحددة (مثال: الخلط الأولي، الطحن، التشكيل، المعالجة، التبريد)
    /// </summary>
    public string ProductionStage { get; set; } = string.Empty;

    /// <summary>
    /// النتائج الفعلية للاختبارات (JSON)
    /// </summary>
    public string? AnalysisResultsJson { get; set; }

    /// <summary>
    /// المعايير أو حدود القبول والرفض للمرحلة
    /// </summary>
    public string? SpecificationCriteria { get; set; }

    /// <summary>
    /// قرار الفحص:
    /// MEETS_REQUIREMENTS (مطابق للاشتراطات الحالية)
    /// DOES_NOT_MEET (غير مطابق للاشتراطات)
    /// REQUIRES_FURTHER_TESTING (يحتاج إلى فحوصات إضافية)
    /// </summary>
    public string InspectionDecision { get; set; } = "MEETS_REQUIREMENTS";

    public string? Notes { get; set; }

    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFileType { get; set; }
    public long AttachmentFileSize { get; set; }

    public string ResponsibleEmployee { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
