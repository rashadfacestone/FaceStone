using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models;

/// <summary>
/// تسجيل الإنتاج اليومي — Daily Production Record
/// Records the cumulative total production achieved during a particular day and shift,
/// including multiple products in the same submission.
/// </summary>
public class DailyProductionSubmission
{
    public int Id { get; set; }

    /// <summary>
    /// Unique Submission Number (e.g. DPS-20261028-0001)
    /// </summary>
    public string SubmissionNumber { get; set; } = string.Empty;

    /// <summary>
    /// تاريخ الإنتاج — Production Date (the date for which production was achieved)
    /// </summary>
    public DateTime ProductionDate { get; set; }

    /// <summary>
    /// Authenticated Employee ID
    /// </summary>
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Authenticated Employee Full Name
    /// </summary>
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>
    /// Authenticated Employee Email
    /// </summary>
    public string EmployeeEmail { get; set; } = string.Empty;

    /// <summary>
    /// الوردية المحددة آلياً — Backend-determined shift based on employee account mapping
    /// (e.g. الوردية الأولى (الصباحية) | الوردية الثانية (المسائية))
    /// </summary>
    public string Shift { get; set; } = string.Empty;

    /// <summary>
    /// بداية الوردية — Shift Start Date and Time
    /// </summary>
    public DateTime ShiftStart { get; set; }

    /// <summary>
    /// نهاية الوردية — Shift End Date and Time
    /// </summary>
    public DateTime ShiftEnd { get; set; }

    /// <summary>
    /// إجمالي مدة الإنتاج (ساعات) — Total Production Duration in Hours
    /// Duration = (ShiftEnd - ShiftStart).TotalHours
    /// </summary>
    public decimal DurationHours { get; set; }

    /// <summary>
    /// إجمالي الإنتاج الكلي (طن) — Total Gross Production (Sum of all product quantities)
    /// </summary>
    public decimal TotalGrossQuantity { get; set; }

    /// <summary>
    /// كمية الهالك (طن) — Total Waste Quantity
    /// </summary>
    public decimal WasteQuantity { get; set; }

    /// <summary>
    /// صافي الإنتاج (طن) — Daily Net Production = TotalGrossQuantity - WasteQuantity
    /// </summary>
    public decimal NetProductionQuantity { get; set; }

    /// <summary>
    /// معدل الإنتاج الإجمالي (طن/ساعة) — Overall Production Rate = TotalGrossQuantity / DurationHours
    /// </summary>
    public decimal ProductionRatePerHour { get; set; }

    /// <summary>
    /// نسبة الهالك (%) — Waste Percentage = (WasteQuantity / TotalGrossQuantity) * 100
    /// </summary>
    public decimal WastePercentage { get; set; }

    /// <summary>
    /// ملاحظات الإنتاج — Production Notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Shared Lifecycle Production Reference Number (Laboratory/Storage integration)
    /// </summary>
    public int? ProductionReferenceNumber { get; set; }

    /// <summary>
    /// Submission Status: SUBMITTED | DRAFT | CANCELLED
    /// </summary>
    public string Status { get; set; } = "SUBMITTED";

    /// <summary>
    /// Submission timestamp (UTC)
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Submitting user username or identity
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Last update timestamp (UTC)
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Last updating user identity
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Related product-detail records produced during this shift
    /// </summary>
    public ICollection<DailyProductionDetail> Details { get; set; } = new List<DailyProductionDetail>();
}

/// <summary>
/// تفاصيل المنتج في تسجيل الإنتاج اليومي — Daily Production Detail
/// Links individual product outputs to the parent daily submission.
/// </summary>
public class DailyProductionDetail
{
    public int Id { get; set; }

    public int DailyProductionSubmissionId { get; set; }
    public DailyProductionSubmission? DailyProductionSubmission { get; set; }

    /// <summary>
    /// Optional product catalogue/raw material ID
    /// </summary>
    public int? ProductId { get; set; }

    /// <summary>
    /// اسم المنتج — Product Name
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// الكمية المنتجة (طن) — Produced Quantity in Tons
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// وحدة القياس — Unit of measure (default: طن)
    /// </summary>
    public string Unit { get; set; } = "طن";

    /// <summary>
    /// معدل إنتاج المنتج (طن/ساعة) — Product Production Rate = Quantity / DurationHours
    /// </summary>
    public decimal ProductionRatePerHour { get; set; }

    /// <summary>
    /// هالك مخصص للمنتج إن وجد — Product-specific waste (if allocated)
    /// </summary>
    public decimal? ProductWaste { get; set; }

    /// <summary>
    /// صافي ناتج المنتج — Net Product Output (Quantity - ProductWaste)
    /// </summary>
    public decimal? NetQuantity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
