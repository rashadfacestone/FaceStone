using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;

namespace FaceStone.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DepartmentController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DepartmentController> _logger;

    public DepartmentController(IWebHostEnvironment env, ILogger<DepartmentController> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Retrieve available departments for the current authenticated user.
    /// Finance is available to all authenticated employees.
    /// Transportation is strictly available only to CEO, IT, and Transportation Manager.
    /// </summary>
    [HttpGet("/api/departments")]
    [HttpGet("list")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<DepartmentListResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetDepartments()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var hasFinance = AppRoles.HasFinanceAccess(role);
        var hasTransportation = AppRoles.HasTransportationAccess(role);
        var hasStorage = AppRoles.HasStorageAccess(role);
        var hasManagement = AppRoles.HasManagementAccess(role);
        var hasControlMonitoring = AppRoles.HasControlMonitoringAccess(role);

        var departments = new List<DepartmentSummaryDto>();

        if (hasFinance)
        {
            departments.Add(new DepartmentSummaryDto
            {
                Id = "finance",
                Name = "FINANCE",
                DisplayName = "Finance Department",
                Description = "Financial statistics, PO tracking, invoices, and accounting management.",
                Icon = "finance",
                IsAvailable = true,
                Route = "/finance"
            });
        }

        if (hasTransportation)
        {
            departments.Add(new DepartmentSummaryDto
            {
                Id = "transportation",
                Name = "TRANSPORTATION",
                DisplayName = "Transportation Department",
                Description = "Logistics, Vehicles & Fleet Operations. Access restricted to authorized personnel.",
                Icon = "transportation",
                IsAvailable = true,
                Route = "/transportation"
            });
        }

        if (hasStorage)
        {
            departments.Add(new DepartmentSummaryDto
            {
                Id = "storage",
                Name = "STORAGE",
                DisplayName = "Storage Department",
                Description = "Factory input and output materials tracking, stock inventory, and warehouse logistics.",
                Icon = "storage",
                IsAvailable = true,
                Route = "/departments/storage"
            });
        }

        if (hasManagement)
        {
            departments.Add(new DepartmentSummaryDto
            {
                Id = "management",
                Name = "MANAGEMENT",
                DisplayName = "Management Department",
                Description = "Factory management, operations coordination, and production oversight.",
                Icon = "management",
                IsAvailable = true,
                Route = "/management"
            });
        }

        if (hasControlMonitoring)
        {
            departments.Add(new DepartmentSummaryDto
            {
                Id = "control-monitoring",
                Name = "CONTROL_MONITORING",
                DisplayName = "Control & Monitoring Department",
                Description = "Review, validate, approve, or reject forms submitted from Transportation and Storage.",
                Icon = "control",
                IsAvailable = true,
                Route = "/control-monitoring"
            });
        }

        _logger.LogInformation("Departments requested by user with role '{Role}'. Finance: {HasFinance}, Transportation: {HasTrans}, Storage: {HasStorage}, Management: {HasMgmt}, ControlMonitoring: {HasControl}", role, hasFinance, hasTransportation, hasStorage, hasManagement, hasControlMonitoring);

        return Ok(ApiResponse<DepartmentListResponseDto>.Ok(new DepartmentListResponseDto
        {
            UserRole = role,
            HasFinanceAccess = hasFinance,
            HasTransportationAccess = hasTransportation,
            HasStorageAccess = hasStorage,
            HasManagementAccess = hasManagement,
            HasControlMonitoringAccess = hasControlMonitoring,
            Departments = departments
        }));
    }

    /// <summary>
    /// Backend authorization guard for Transportation department.
    /// Returns 200 OK for CEO, IT, and TRANSPORTATION_MANAGER.
    /// Returns 403 Forbidden for any other authenticated role.
    /// </summary>
    [HttpGet("transportation")]
    [Authorize(Policy = "TransportationAccess")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult VerifyTransportationAccess()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
        _logger.LogInformation("Transportation department access verified for role: {Role}", role);
        return Ok(ApiResponse.Ok("Access Granted: Authorized for Transportation Department."));
    }

    /// <summary>
    /// Backend authorization guard for Storage department.
    /// Returns 200 OK for CEO, IT, and STORAGE_MANAGER.
    /// Returns 403 Forbidden for any other authenticated role.
    /// </summary>
    [HttpGet("storage")]
    [Authorize(Policy = "StorageAccess")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult VerifyStorageAccess()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
        _logger.LogInformation("Storage department access verified for role: {Role}", role);
        return Ok(ApiResponse.Ok("Access Granted: Authorized for Storage Department."));
    }

    /// <summary>
    /// Backend authorization guard for Management department.
    /// Returns 200 OK for CEO, IT, and FACTORY_MANAGER.
    /// Returns 403 Forbidden for any other authenticated role.
    /// </summary>
    [HttpGet("management")]
    [Authorize(Policy = "ManagementAccess")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult VerifyManagementAccess()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
        _logger.LogInformation("Management department access verified for role: {Role}", role);
        return Ok(ApiResponse.Ok("Access Granted: Authorized for Management Department."));
    }

    /// <summary>
    /// Backend authorization guard for Finance department (Redirection & Management).
    /// Returns 200 OK for CEO, IT, and Accountant.
    /// Returns 403 Forbidden for any other authenticated role.
    /// </summary>
    [HttpGet("finance")]
    [Authorize(Policy = "FinanceAccess")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult VerifyFinanceAccess()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
        _logger.LogInformation("Finance department access verified for role: {Role}", role);
        return Ok(ApiResponse.Ok("Access Granted: Authorized for Finance Department."));
    }

    /// <summary>
    /// Backend authorization guard for Control & Monitoring department.
    /// Returns 200 OK for CEO, IT, and CONTROL_MONITORING.
    /// Returns 403 Forbidden for any other authenticated role.
    /// </summary>
    [HttpGet("control-monitoring")]
    [Authorize(Policy = "ControlMonitoringAccess")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult VerifyControlMonitoringAccess()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
        _logger.LogInformation("Control & Monitoring department access verified for role: {Role}", role);
        return Ok(ApiResponse.Ok("Access Granted: Authorized for Control & Monitoring Department."));
    }

    /// <summary>
    /// Direct route protection for /transportation endpoint.
    /// Supports both direct API callers (returns 200 / 403 / 401) and browser navigation.
    /// </summary>
    [HttpGet("/transportation")]
    public IActionResult HandleTransportationDirect()
    {
        // 1. If this is a browser navigation request for HTML:
        if (Request.Headers.Accept.Any(a => a != null && a.Contains("text/html")))
        {
            var indexPath = Path.Combine(_env.WebRootPath, "index.html");
            if (System.IO.File.Exists(indexPath))
            {
                SetNoCacheHeaders();
                return PhysicalFile(indexPath, "text/html");
            }
        }

        // 2. If caller is not authenticated:
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized(ApiResponse.Fail("Authentication required to access Transportation."));
        }

        // 3. Check role permission:
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasTransportationAccess(role))
        {
            _logger.LogWarning("Access Denied to /transportation for user role: {Role}", role);
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse.Fail("Access Denied: Transportation department is restricted to CEO, IT, and Transportation Manager."));
        }

        return Ok(ApiResponse.Ok("Access Granted: Authorized for Transportation Department."));
    }

    /// <summary>
    /// Direct route protection for /storage and /departments/storage endpoints.
    /// Supports both direct API callers (returns 200 / 403 / 401) and browser navigation.
    /// </summary>
    [HttpGet("/storage")]
    [HttpGet("/departments/storage")]
    public IActionResult HandleStorageDirect()
    {
        // 1. If this is a browser navigation request for HTML:
        if (Request.Headers.Accept.Any(a => a != null && a.Contains("text/html")))
        {
            var indexPath = Path.Combine(_env.WebRootPath, "index.html");
            if (System.IO.File.Exists(indexPath))
            {
                SetNoCacheHeaders();
                return PhysicalFile(indexPath, "text/html");
            }
        }

        // 2. If caller is not authenticated:
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized(ApiResponse.Fail("Authentication required to access Storage."));
        }

        // 3. Check role permission:
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasStorageAccess(role))
        {
            _logger.LogWarning("Access Denied to /storage for user role: {Role}", role);
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse.Fail("Access Denied: Storage department is restricted to CEO, IT, and Storage Manager."));
        }

        return Ok(ApiResponse.Ok("Access Granted: Authorized for Storage Department."));
    }

    /// <summary>
    /// Direct route protection for /management endpoint.
    /// Supports both direct API callers (returns 200 / 403 / 401) and browser navigation.
    /// </summary>
    [HttpGet("/management")]
    public IActionResult HandleManagementDirect()
    {
        // 1. If this is a browser navigation request for HTML:
        if (Request.Headers.Accept.Any(a => a != null && a.Contains("text/html")))
        {
            var indexPath = Path.Combine(_env.WebRootPath, "index.html");
            if (System.IO.File.Exists(indexPath))
            {
                SetNoCacheHeaders();
                return PhysicalFile(indexPath, "text/html");
            }
        }

        // 2. If caller is not authenticated:
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized(ApiResponse.Fail("Authentication required to access Management."));
        }

        // 3. Check role permission:
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasManagementAccess(role))
        {
            _logger.LogWarning("Access Denied to /management for user role: {Role}", role);
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse.Fail("Access Denied: Management department is restricted to CEO, IT, and Factory Manager."));
        }

        return Ok(ApiResponse.Ok("Access Granted: Authorized for Management Department."));
    }

    /// <summary>
    /// Direct browser route helper for /departments.
    /// </summary>
    [HttpGet("/departments")]
    public IActionResult HandleDepartmentsDirect()
    {
        if (Request.Headers.Accept.Any(a => a != null && a.Contains("text/html")))
        {
            var indexPath = Path.Combine(_env.WebRootPath, "index.html");
            if (System.IO.File.Exists(indexPath))
            {
                SetNoCacheHeaders();
                return PhysicalFile(indexPath, "text/html");
            }
        }
        return Ok(ApiResponse.Ok("FaceStone Department Portal."));
    }

    /// <summary>
    /// Direct browser route helper for /finance, /finance/management, /finance/hub, /finance/performance, and /finance/purchase-orders/{poNumber}.
    /// Supports both direct API callers (returns 200 / 403 / 401) and browser navigation.
    /// </summary>
    [HttpGet("/finance")]
    [HttpGet("/finance/management")]
    [HttpGet("/finance/hub")]
    [HttpGet("/finance/performance")]
    [HttpGet("/finance/purchase-orders/{poNumber}")]
    [HttpGet("/finance/history")]
    [HttpGet("/finance/suppliers")]
    [HttpGet("/finance/suppliers/{id}")]
    [HttpGet("/finance/purchase-orders")]
    [HttpGet("/finance/bank")]
    [HttpGet("/finance/expenses")]
    [HttpGet("/finance/employees")]
    [HttpGet("/finance/employees/{id}")]
    public IActionResult HandleFinanceDirect()
    {
        // 1. If this is a browser navigation request for HTML:
        if (Request.Headers.Accept.Any(a => a != null && a.Contains("text/html")))
        {
            var indexPath = Path.Combine(_env.WebRootPath, "index.html");
            if (System.IO.File.Exists(indexPath))
            {
                SetNoCacheHeaders();
                return PhysicalFile(indexPath, "text/html");
            }
        }

        // 2. If caller is not authenticated:
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized(ApiResponse.Fail("Authentication required to access Finance."));
        }

        // 3. Check role permission:
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            _logger.LogWarning("Access Denied to /finance for user role: {Role}", role);
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse.Fail("Access Denied: Finance department is restricted to CEO, IT, and Accountants."));
        }

        return Ok(ApiResponse.Ok("FaceStone Finance Department."));
    }

    /// <summary>
    /// Direct route protection for /control-monitoring endpoint.
    /// Supports both direct API callers (returns 200 / 403 / 401) and browser navigation.
    /// </summary>
    [HttpGet("/control-monitoring")]
    [HttpGet("/control-monitoring/{*subpath}")]
    public IActionResult HandleControlMonitoringDirect()
    {
        // 1. If this is a browser navigation request for HTML:
        if (Request.Headers.Accept.Any(a => a != null && a.Contains("text/html")))
        {
            var indexPath = Path.Combine(_env.WebRootPath, "index.html");
            if (System.IO.File.Exists(indexPath))
            {
                SetNoCacheHeaders();
                return PhysicalFile(indexPath, "text/html");
            }
        }

        // 2. If caller is not authenticated:
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized(ApiResponse.Fail("Authentication required to access Control & Monitoring."));
        }

        // 3. Check role permission:
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasControlMonitoringAccess(role))
        {
            _logger.LogWarning("Access Denied to /control-monitoring for user role: {Role}", role);
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse.Fail("Access Denied: Control & Monitoring department is restricted to CEO, IT, and Control & Monitoring employees."));
        }

        return Ok(ApiResponse.Ok("FaceStone Control & Monitoring Department."));
    }

    private void SetNoCacheHeaders()
    {
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
    }
}
