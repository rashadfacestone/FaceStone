using System;

namespace FaceStone.Web.Models;

/// <summary>
/// Configured storage location entity (e.g. مستودع الخامات الرئيسي, صوامع الإسمنت)
/// </summary>
public class StorageLocation
{
    public int Id { get; set; }

    /// <summary>
    /// اسم مكان التخزين بالعربية
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// كود أو رمز مكان التخزين
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// وصف مكان التخزين
    /// </summary>
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
