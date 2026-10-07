namespace FaceStone.Web.Models.DTOs;

public class DepartmentSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string Route { get; set; } = string.Empty;
}

public class DepartmentListResponseDto
{
    public string UserRole { get; set; } = string.Empty;
    public bool HasFinanceAccess { get; set; }
    public bool HasTransportationAccess { get; set; }
    public bool HasStorageAccess { get; set; }
    public bool HasManagementAccess { get; set; }
    public bool HasControlMonitoringAccess { get; set; }
    public List<DepartmentSummaryDto> Departments { get; set; } = new();
}
