namespace FaceStone.Web.Models;

/// <summary>
/// Concurrency-safe atomic sequence tracker for Laboratory Shared Production Reference Number (starts at 1)
/// </summary>
public class LaboratoryProductionSequence
{
    /// <summary>
    /// Sequence identifier (e.g. "PRODUCTION_REFERENCE")
    /// </summary>
    public string SequenceName { get; set; } = "PRODUCTION_REFERENCE";

    /// <summary>
    /// The next integer reference number to assign (starts at 1)
    /// </summary>
    public int NextValue { get; set; } = 1;
}
