using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FaceStone.Web.Services;

public class ProductionShiftService : IProductionShiftService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProductionShiftService> _logger;

    // Standard Shift Constants
    public const string FirstShiftMorning = "الوردية الأولى (الصباحية)";
    public const string SecondShiftEvening = "الوردية الثانية (المسائية)";

    // Default built-in verified mapping as fallback if configuration is missing
    private static readonly Dictionary<string, string> DefaultMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "prod.manager@facestone.com", FirstShiftMorning },
        { "prod.employee@facestone.com", SecondShiftEvening }
    };

    public ProductionShiftService(IConfiguration configuration, ILogger<ProductionShiftService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string? GetShiftForEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var normalized = email.Trim().ToLowerInvariant();

        // 1. Try reading from appsettings: ProductionSettings:ShiftMappings
        var configuredSection = _configuration.GetSection("ProductionSettings:ShiftMappings");
        if (configuredSection.Exists())
        {
            foreach (var child in configuredSection.GetChildren())
            {
                if (string.Equals(child.Key, normalized, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(child.Key.Replace("_", "."), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(child.Value))
                    {
                        return child.Value.Trim();
                    }
                }
            }
        }

        // 2. Fallback to default verified mappings
        if (DefaultMappings.TryGetValue(normalized, out var shift))
        {
            return shift;
        }

        _logger.LogWarning("No shift mapping found for authenticated employee email '{Email}'", email);
        return null;
    }

    public bool HasShiftMapping(string? email)
    {
        return !string.IsNullOrEmpty(GetShiftForEmail(email));
    }

    public IReadOnlyDictionary<string, string> GetAllMappings()
    {
        var result = new Dictionary<string, string>(DefaultMappings, StringComparer.OrdinalIgnoreCase);

        var configuredSection = _configuration.GetSection("ProductionSettings:ShiftMappings");
        if (configuredSection.Exists())
        {
            foreach (var child in configuredSection.GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(child.Value))
                {
                    result[child.Key] = child.Value.Trim();
                }
            }
        }

        return result;
    }
}
