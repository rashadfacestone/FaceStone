namespace FaceStone.Web.Models;

public static class AppRoles
{
    public const string CEO = "CEO";
    public const string IT = "IT";
    public const string TransportationManager = "TRANSPORTATION_MANAGER";
    public const string TransportationManagerDisplay = "Transportation Manager";
    public const string StorageManager = "STORAGE_MANAGER";
    public const string StorageManagerCode = "StorageManager";
    public const string StorageManagerDisplay = "Storage Manager";
    public const string Accountant = "Accountant";
    public const string Employee = "Employee";
    public const string SeniorStructuralEngineer = "Senior Structural Engineer";
    public const string Contractor = "Contractor";
    public const string FactoryManager = "FACTORY_MANAGER";
    public const string FactoryManagerDisplay = "Factory Manager";
    public const string FinanceDirector = "FINANCE_DIRECTOR";
    public const string FinanceDirectorDisplay = "Finance Director";
    public const string FinanceManager = "FINANCE_MANAGER";
    public const string FinanceManagerDisplay = "Finance Manager";
    public const string CFO = "CFO";

    public static readonly string[] TransportationAllowedRoles = 
    { 
        CEO, 
        IT, 
        TransportationManager, 
        TransportationManagerDisplay 
    };

    public static readonly string[] StorageAllowedRoles = 
    { 
        CEO, 
        IT, 
        StorageManager, 
        StorageManagerCode,
        StorageManagerDisplay 
    };

    public static readonly string[] ManagementAllowedRoles = 
    { 
        CEO, 
        IT, 
        FactoryManager,
        FactoryManagerDisplay 
    };

    public static readonly string[] FinanceAllowedRoles = 
    { 
        CEO, 
        IT, 
        Accountant,
        FinanceDirector,
        FinanceDirectorDisplay,
        FinanceManager,
        FinanceManagerDisplay,
        CFO
    };

    public static readonly string[] FinanceDirectorAllowedRoles = 
    { 
        CEO, 
        IT, 
        FinanceDirector, 
        FinanceDirectorDisplay, 
        FinanceManager, 
        FinanceManagerDisplay, 
        CFO 
    };

    public static bool HasTransportationAccess(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim();
        foreach (var allowed in TransportationAllowedRoles)
        {
            if (r.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool HasStorageAccess(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim();
        foreach (var allowed in StorageAllowedRoles)
        {
            if (r.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool HasManagementAccess(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim();
        foreach (var allowed in ManagementAllowedRoles)
        {
            if (r.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool HasFinanceAccess(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim();
        foreach (var allowed in FinanceAllowedRoles)
        {
            if (r.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool IsFinanceDirector(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim();
        foreach (var allowed in FinanceDirectorAllowedRoles)
        {
            if (r.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
