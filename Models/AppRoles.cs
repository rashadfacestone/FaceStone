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
    public const string ControlMonitoring = "CONTROL_MONITORING";
    public const string ControlMonitoringDisplay = "Control & Monitoring Employee";

    public static readonly string[] ControlMonitoringAllowedRoles =
    {
        CEO,
        IT,
        ControlMonitoring,
        ControlMonitoringDisplay
    };

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

    public static bool HasControlMonitoringAccess(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim();
        foreach (var allowed in ControlMonitoringAllowedRoles)
        {
            if (r.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public const string AttendanceEmployee = "ATTENDANCE_EMPLOYEE";
    public const string AttendanceEmployeeDisplay = "Attendance Employee";

    public static readonly string[] AttendanceAllowedRoles =
    {
        CEO,
        IT,
        AttendanceEmployee,
        AttendanceEmployeeDisplay
    };

    public static bool HasAttendanceAccess(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim();
        foreach (var allowed in AttendanceAllowedRoles)
        {
            if (r.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool CanSubmitAttendance(string? role)
    {
        return HasAttendanceAccess(role);
    }

    // =========================================================================
    // LABORATORY DEPARTMENT ROLES (المعمل الفيزيائي والمعمل الكيميائي)
    // =========================================================================
    public const string PhysicalLabEmployee = "PHYSICAL_LAB_EMPLOYEE";
    public const string PhysicalLabEmployeeDisplay = "Physical Laboratory Employee";
    public const string ChemicalLabEmployee = "CHEMICAL_LAB_EMPLOYEE";
    public const string ChemicalLabEmployeeDisplay = "Chemical Laboratory Employee";

    public static readonly string[] PhysicalLabAllowedRoles =
    {
        CEO,
        IT,
        PhysicalLabEmployee,
        PhysicalLabEmployeeDisplay
    };

    public static readonly string[] ChemicalLabAllowedRoles =
    {
        CEO,
        IT,
        ChemicalLabEmployee,
        ChemicalLabEmployeeDisplay
    };

    public static readonly string[] LaboratoryAllowedRoles =
    {
        CEO,
        IT,
        PhysicalLabEmployee,
        PhysicalLabEmployeeDisplay,
        ChemicalLabEmployee,
        ChemicalLabEmployeeDisplay
    };

    public static bool HasLaboratoryAccess(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim();
        foreach (var allowed in LaboratoryAllowedRoles)
        {
            if (r.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool HasPhysicalLabAccess(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim();
        foreach (var allowed in PhysicalLabAllowedRoles)
        {
            if (r.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool HasChemicalLabAccess(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim();
        foreach (var allowed in ChemicalLabAllowedRoles)
        {
            if (r.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static string GetDefaultLaboratorySection(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return string.Empty;
        var r = role.Trim();
        if (r.Equals(PhysicalLabEmployee, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(PhysicalLabEmployeeDisplay, StringComparison.OrdinalIgnoreCase))
            return "PHYSICAL";
        if (r.Equals(ChemicalLabEmployee, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(ChemicalLabEmployeeDisplay, StringComparison.OrdinalIgnoreCase))
            return "CHEMICAL";
        if (r.Equals(CEO, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(IT, StringComparison.OrdinalIgnoreCase))
            return "ALL";
        return string.Empty;
    }

    // =========================================================================
    // PRODUCTION DEPARTMENT ROLES (قسم الإنتاج)
    // =========================================================================
    public const string ProductionManager = "PRODUCTION_MANAGER";
    public const string ProductionManagerDisplay = "Production Manager";
    public const string ProductionEmployee = "PRODUCTION_EMPLOYEE";
    public const string ProductionEmployeeDisplay = "Production Employee";

    public static readonly string[] ProductionAllowedRoles =
    {
        CEO,
        IT,
        ProductionManager,
        ProductionManagerDisplay,
        ProductionEmployee,
        ProductionEmployeeDisplay
    };

    public static readonly string[] ProductionManagerAllowedRoles =
    {
        CEO,
        IT,
        ProductionManager,
        ProductionManagerDisplay
    };

    public static bool HasProductionAccess(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim();
        foreach (var allowed in ProductionAllowedRoles)
        {
            if (r.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool IsProductionManager(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim();
        foreach (var allowed in ProductionManagerAllowedRoles)
        {
            if (r.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
