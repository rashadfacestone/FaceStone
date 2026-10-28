using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models;

/// <summary>
/// Client entity registered in FaceStone for Storage Export operations
/// </summary>
public class StorageClient
{
    public int Id { get; set; }

    /// <summary>
    /// اسم العميل
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// رقم الهاتف المسجل للعميل
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// الوجهات المسجلة لهذا العميل
    /// </summary>
    public ICollection<StorageClientDestination> Destinations { get; set; } = new List<StorageClientDestination>();
}

/// <summary>
/// Registered client destination / project site
/// </summary>
public class StorageClientDestination
{
    public int Id { get; set; }

    public int StorageClientId { get; set; }
    public StorageClient? StorageClient { get; set; }

    /// <summary>
    /// اسم الوجهة أو موقع المشروع
    /// </summary>
    public string DestinationName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
