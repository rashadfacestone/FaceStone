namespace FaceStone.Web.Models;

/// <summary>
/// Concurrency-safe atomic sequence tracker per operation type (IMPORT, EXPORT, TRANSFER)
/// </summary>
public class StorageSequence
{
    /// <summary>
    /// IMPORT | EXPORT | TRANSFER
    /// </summary>
    public string SequenceType { get; set; } = string.Empty;

    /// <summary>
    /// The next integer serial number to assign (starts at 1)
    /// </summary>
    public int NextValue { get; set; } = 1;
}
