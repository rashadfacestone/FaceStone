using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models;

/// <summary>
/// صنف خامة في المستودع - Raw Material entity stored in SQL Server
/// </summary>
public class RawMaterial
{
    public int Id { get; set; }

    /// <summary>
    /// اسم الصنف - Unique Raw Material Name in Arabic
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// الكمية الافتتاحية المسجلة عند إضافة الصنف
    /// </summary>
    public decimal InitialQuantity { get; set; }

    /// <summary>
    /// تاريخ إضافة الصنف للمستودع
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// اسم المستخدم الذي قام بإضافة الصنف
    /// </summary>
    public string? CreatedBy { get; set; }

    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// نوع وتصنيف الخامة: MECHANICAL (خامات ميكانيكية) | CHEMICAL (خامات كيميائية)
    /// </summary>
    public string MaterialType { get; set; } = MaterialProcessType.Mechanical;

    /// <summary>
    /// حركات كارت الصنف المرتبطة
    /// </summary>
    public ICollection<StorageItemTransaction> Transactions { get; set; } = new List<StorageItemTransaction>();
}

public static class MaterialProcessType
{
    public const string Mechanical = "MECHANICAL";
    public const string Chemical = "CHEMICAL";
    public const string RecoverableWaste = "RECOVERABLE_WASTE";
    public const string FinalProduct = "FINAL_PRODUCT";

    public static string ToArabic(string? type)
    {
        return (type ?? string.Empty).ToUpperInvariant() switch
        {
            Mechanical => "خامات ميكانيكية",
            Chemical => "خامات كيميائية",
            RecoverableWaste => "مواد مستردة / إعادة تدوير",
            FinalProduct => "منتج نهائي",
            _ => "خامات عامة"
        };
    }
}
