namespace FaceStone.Web.Models;

/// <summary>
/// Detail line for a material within a Storage Operation (Import, Export, Transfer)
/// </summary>
public class StorageOperationItem
{
    public int Id { get; set; }

    public int StorageOperationId { get; set; }
    public StorageOperation? StorageOperation { get; set; }

    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    /// <summary>
    /// اسم الصنف
    /// </summary>
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>
    /// الكمية بالطن (decimal 18,3)
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// رقم تتبع التحويل / البند (Generated tracking code, e.g. TRF-2026-0001-L1)
    /// </summary>
    public string TrackingNumber { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
