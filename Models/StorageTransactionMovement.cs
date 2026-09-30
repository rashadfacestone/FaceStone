using System;

namespace FaceStone.Web.Models;

/// <summary>
/// Relational junction entity linking a StorageTransaction to multiple Transportation movement numbers.
/// </summary>
public class StorageTransactionMovement
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign Key to parent StorageTransaction
    /// </summary>
    public int StorageTransactionId { get; set; }

    public StorageTransaction? StorageTransaction { get; set; }

    /// <summary>
    /// رقم النقلة - Transportation movement number (e.g., 1001, 1002, 1003)
    /// </summary>
    public int TransportationMovementNumber { get; set; }

    /// <summary>
    /// Optional foreign key to Transportation record if matching by transaction number
    /// </summary>
    public int? TransportationId { get; set; }

    public Transportation? Transportation { get; set; }
}
