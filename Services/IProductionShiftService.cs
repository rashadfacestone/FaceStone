using System.Collections.Generic;

namespace FaceStone.Web.Services;

public interface IProductionShiftService
{
    /// <summary>
    /// Gets the backend-configured shift associated with the specified employee email.
    /// Returns null if no valid mapping exists.
    /// </summary>
    string? GetShiftForEmail(string? email);

    /// <summary>
    /// Checks whether the specified employee email has an assigned production shift.
    /// </summary>
    bool HasShiftMapping(string? email);

    /// <summary>
    /// Returns all configured email-to-shift mappings.
    /// </summary>
    IReadOnlyDictionary<string, string> GetAllMappings();
}
