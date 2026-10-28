using System;

namespace FaceStone.Web.Models;

/// <summary>
/// Concurrency-safe atomic sequence tracker for per-user Laboratory Sample Numbers (starts at 1 for each user).
/// Each authenticated laboratory employee has an independent sequential sample number (1, 2, 3...).
/// </summary>
public class LaboratoryUserSampleSequence
{
    /// <summary>
    /// Authenticated User ID (UserAccount.Id)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Next sequential sample number to assign to this user (starts at 1)
    /// </summary>
    public int NextSampleNumber { get; set; } = 1;

    /// <summary>
    /// Last update timestamp in UTC
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
