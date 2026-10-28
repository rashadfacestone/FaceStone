using System.Collections.Generic;

namespace FaceStone.Web.Models;

/// <summary>
/// دليل وكتالوج المنتجات المعتمد للإنتاج — Controlled Production Product Catalogue
/// Provides standard product definitions with identifiers, categories, and units.
/// </summary>
public static class ProductionProductCatalog
{
    public record ProductItem(int Id, string Code, string Name, string Category, string Unit, string Description);

    public static readonly IReadOnlyList<ProductItem> StandardProducts = new List<ProductItem>
    {
        new(1, "PRD-A", "Product A (منتج أ)", "منتجات قياسية", "طن", "منتج قياسي عالي الجودة - اختبارات وتعيين رتبة A"),
        new(2, "PRD-B", "Product B (منتج ب)", "منتجات قياسية", "طن", "منتج قياسي عالي الجودة - اختبارات وتعيين رتبة B"),
        new(3, "PRD-C", "Product C (منتج ج)", "منتجات قياسية", "طن", "منتج قياسي عالي الجودة - اختبارات وتعيين رتبة C"),
        new(4, "PRD-STONE-BG", "حجر صناعي واجهات بيج نخب أول", "أحجار صناعية", "طن", "ألواح حجر صناعي فائق القوة لواجهات المباني بلون بيج معتمد"),
        new(5, "PRD-STONE-GR", "حجر صناعي واجهات رمادي مودرن", "أحجار صناعية", "طن", "ألواح حجر واجهات رمادي معالج هندسياً ومقاوم للرطوبة"),
        new(6, "PRD-CNC-C35", "خرسانة جاهزة عالية الإجهاد رتبة C35", "خرسانة جاهزة", "طن", "خلطة خرسانية تصميمية مسبقة الخلط والإجهاد 35 ميجاباسكال"),
        new(7, "PRD-CNC-C25", "خرسانة جاهزة رتبة C25", "خرسانة جاهزة", "طن", "خرسانة عادية إنشائية معتمدة للاستخدامات الهندسية الشائعة"),
        new(8, "PRD-QTZ-CALA", "ألواح كوارتز كلاكتا وايت (Calacatta Quartz)", "كوارتز ورخام", "طن", "ألواح كوارتز صناعي فاخر مظهر رخام كلاكتا سماكة 20 مم"),
        new(9, "PRD-GRN-60", "بلاط جرانيت 60×60 مصنع", "جرانيت وبلاط", "طن", "بلاطات جرانيت صناعي عالية الكثافة والصلابة 60x60 سم"),
        new(10, "PRD-STONE-CLS", "حجر صناعي واجهات كلاسيك نخب ممتاز", "أحجار صناعية", "طن", "واجهات حجرية بنمط كلاسيكي مقاوم للأملاح والعوامل الجوية")
    };

    public static bool IsValidProduct(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName)) return false;
        var trimmed = productName.Trim();
        foreach (var p in StandardProducts)
        {
            if (p.Name.Equals(trimmed, System.StringComparison.OrdinalIgnoreCase) ||
                p.Code.Equals(trimmed, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
