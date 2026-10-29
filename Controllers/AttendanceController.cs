using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FaceStone.Web.Data;
using FaceStone.Web.Hubs;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;

namespace FaceStone.Web.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly FaceStoneDbContext _db;
    private readonly ILogger<AttendanceController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IHubContext<ChatHub> _hubContext;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png"
    };

    public AttendanceController(
        FaceStoneDbContext db,
        ILogger<AttendanceController> logger,
        IWebHostEnvironment env,
        IHubContext<ChatHub> hubContext)
    {
        _db = db;
        _logger = logger;
        _env = env;
        _hubContext = hubContext;
    }

    private static TimeZoneInfo GetBusinessTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
            }
            catch
            {
                return TimeZoneInfo.Local;
            }
        }
    }

    public static DateTime GetBusinessToday()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetBusinessTimeZone()).Date;
    }

    public static DateTime ConvertToUtcFromBusiness(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc) return dt;
        if (dt.Kind == DateTimeKind.Local) return dt.ToUniversalTime();
        var tz = GetBusinessTimeZone();
        try
        {
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), tz);
        }
        catch
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }
    }

    public static string CalculateSubmissionStatus(IEnumerable<AttendanceRecord> records)
    {
        var list = records.ToList();
        if (list.Count == 0) return "PENDING";

        var approvedCount = list.Count(r => r.ApprovalStatus.Equals("APPROVED", StringComparison.OrdinalIgnoreCase));
        var rejectedCount = list.Count(r => r.ApprovalStatus.Equals("REJECTED", StringComparison.OrdinalIgnoreCase));
        var pendingCount = list.Count(r => !r.ApprovalStatus.Equals("APPROVED", StringComparison.OrdinalIgnoreCase) &&
                                           !r.ApprovalStatus.Equals("REJECTED", StringComparison.OrdinalIgnoreCase));

        if (pendingCount == list.Count)
        {
            return "PENDING";
        }
        if (pendingCount > 0)
        {
            return "PARTIALLY_REVIEWED";
        }
        if (approvedCount == list.Count)
        {
            return "APPROVED";
        }
        if (rejectedCount == list.Count)
        {
            return "REJECTED";
        }
        return "MIXED"; // Reviewed with mixed results: Some approved, some rejected, 0 pending
    }

    /// <summary>
    /// ISOLATED FAKE EMPLOYEE DATA FOR INITIAL DEMONSTRATION (Requirement 4).
    /// Mark these records clearly as test data.
    /// Do not overwrite real users. Do not insert into production Users table.
    /// Structured so that replacing with real database users is simple and decoupled.
    /// </summary>
    private static readonly List<AttendanceEmployeeItemDto> IsolatedTestEmployees = new()
    {
        new AttendanceEmployeeItemDto
        {
            UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            FullName = "Ahmed Hassan",
            Username = "ahmed.hassan",
            Role = "Accountant",
            Department = "Finance",
            IsTestData = true
        },
        new AttendanceEmployeeItemDto
        {
            UserId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            FullName = "Mohamed Ali",
            Username = "mohamed.ali",
            Role = "Transportation Manager",
            Department = "Transportation",
            IsTestData = true
        },
        new AttendanceEmployeeItemDto
        {
            UserId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            FullName = "Kareem Mahmoud",
            Username = "kareem.mahmoud",
            Role = "Storage Manager",
            Department = "Storage",
            IsTestData = true
        },
        new AttendanceEmployeeItemDto
        {
            UserId = Guid.Parse("00000000-0000-0000-0000-000000000004"),
            FullName = "Sara Mohamed",
            Username = "sara.mohamed",
            Role = "Factory Manager",
            Department = "Management",
            IsTestData = true
        },
        new AttendanceEmployeeItemDto
        {
            UserId = Guid.Parse("00000000-0000-0000-0000-000000000005"),
            FullName = "Omar Ibrahim",
            Username = "omar.ibrahim",
            Role = "Employee",
            Department = "Operations",
            IsTestData = true
        },
        new AttendanceEmployeeItemDto
        {
            UserId = Guid.Parse("00000000-0000-0000-0000-000000000006"),
            FullName = "Nour El-Din Mostafa",
            Username = "nour.mostafa",
            Role = "Senior Structural Engineer",
            Department = "Engineering",
            IsTestData = true
        },
        new AttendanceEmployeeItemDto
        {
            UserId = Guid.Parse("00000000-0000-0000-0000-000000000007"),
            FullName = "Youssef Samir",
            Username = "youssef.samir",
            Role = "Maintenance Supervisor",
            Department = "Maintenance",
            IsTestData = true
        },
        new AttendanceEmployeeItemDto
        {
            UserId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
            FullName = "Hassan El-Sayed",
            Username = "hassan.elsayed",
            Role = "Quality Control Specialist",
            Department = "Quality",
            IsTestData = true
        }
    };

    private async Task<List<AttendanceEmployeeItemDto>> GetEmployeesListAsync()
    {
        // First retrieve active system users from the database
        var dbUsers = await _db.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync();
        if (dbUsers.Any())
        {
            return dbUsers.Select(u => new AttendanceEmployeeItemDto
            {
                UserId = u.Id,
                FullName = u.FullName,
                Username = u.Username,
                Role = u.Role,
                Department = u.Role,
                IsTestData = false,
                Status = string.Empty
            }).ToList();
        }

        // Fallback for initial empty database demonstration
        return IsolatedTestEmployees.Select(e => new AttendanceEmployeeItemDto
        {
            UserId = e.UserId,
            FullName = e.FullName,
            Username = e.Username,
            Role = e.Role,
            Department = e.Department,
            IsTestData = true,
            Status = string.Empty
        }).ToList();
    }

    /// <summary>
    /// Retrieve all employees for the Attendance sheet (Requirement 4: Isolated fake test data).
    /// </summary>
    [HttpGet("employees")]
    [ProducesResponseType(typeof(ApiResponse<List<AttendanceEmployeeItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployees()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasAttendanceAccess(role) && !AppRoles.HasControlMonitoringAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("Access Denied: Attendance records are restricted."));
        }

        var employees = await GetEmployeesListAsync();
        return Ok(ApiResponse<List<AttendanceEmployeeItemDto>>.Ok(employees));
    }

    /// <summary>
    /// Retrieve daily attendance sheet for today (Requirement 3: strictly today's date).
    /// </summary>
    [HttpGet("date/{date:datetime}")]
    [HttpGet("sheet")]
    [HttpGet("today")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSheetByDate([FromRoute] DateTime? date)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasAttendanceAccess(role) && !AppRoles.HasControlMonitoringAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("Access Denied: Attendance records are restricted."));
        }

        // Strictly enforce today's date in local business timezone (Requirement 3)
        var targetDate = GetBusinessToday();

        // Fetch configured employees list
        var employees = await GetEmployeesListAsync();

        // Check if there is an existing submission for today's date (most recent first)
        var submission = await _db.AttendanceSubmissions
            .AsNoTracking()
            .Include(s => s.Records)
            .Include(s => s.Attachments)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(s => s.AttendanceDate.Date == targetDate);

        var employeeDtos = new List<AttendanceEmployeeItemDto>();
        var recordsMapByUserId = submission?.Records.Where(r => r.UserId.HasValue).ToDictionary(r => r.UserId!.Value, r => r) 
            ?? new Dictionary<Guid, AttendanceRecord>();
        var recordsMapByName = submission?.Records.Where(r => !string.IsNullOrWhiteSpace(r.EmployeeName)).ToDictionary(r => r.EmployeeName, r => r, StringComparer.OrdinalIgnoreCase) 
            ?? new Dictionary<string, AttendanceRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var emp in employees)
        {
            var dto = new AttendanceEmployeeItemDto
            {
                UserId = emp.UserId,
                FullName = emp.FullName,
                Username = emp.Username,
                Role = emp.Role,
                Department = emp.Department,
                IsTestData = emp.IsTestData,
                Status = string.Empty
            };

            AttendanceRecord? rec = null;
            if (recordsMapByUserId.TryGetValue(emp.UserId, out var r1))
            {
                rec = r1;
            }
            else if (recordsMapByName.TryGetValue(emp.FullName, out var r2))
            {
                rec = r2;
            }

            if (rec != null)
            {
                dto.RecordId = rec.Id;
                dto.Status = rec.Status;
                dto.AttendanceDateTime = rec.CheckInTime;
                dto.CheckInTime = rec.CheckInTime;
                dto.CheckOutTime = rec.CheckOutTime;
                dto.Notes = rec.Notes;
                dto.ApprovalStatus = rec.ApprovalStatus;
                dto.ReviewedBy = rec.ReviewedBy;
                dto.ReviewedAt = rec.ReviewedAt;
                dto.RejectionReason = rec.RejectionReason;
            }

            employeeDtos.Add(dto);
        }

        AttendanceSubmissionDetailDto? subDto = null;
        if (submission != null)
        {
            var audit = await _db.FormApprovalHistories
                .AsNoTracking()
                .Where(h => h.FormType.ToLower() == "attendance" && h.FormId == submission.Id)
                .OrderByDescending(h => h.Timestamp)
                .Select(h => new FormApprovalHistoryDto
                {
                    Id = h.Id,
                    FormType = h.FormType,
                    FormId = h.FormId,
                    PONumber = h.PONumber,
                    TransactionOrMovementNumber = h.TransactionOrMovementNumber,
                    PreviousStatus = h.PreviousStatus,
                    NewStatus = h.NewStatus,
                    Action = h.Action,
                    ActorName = h.ActorName,
                    ActorRole = h.ActorRole,
                    Comment = h.Comment,
                    Timestamp = h.Timestamp
                })
                .ToListAsync();

            subDto = new AttendanceSubmissionDetailDto
            {
                Id = submission.Id,
                AttendanceDate = submission.AttendanceDate,
                SubmittedByUserId = submission.SubmittedByUserId,
                SubmittedBy = submission.SubmittedBy,
                SubmittedAt = submission.SubmittedAt,
                ApprovalStatus = submission.ApprovalStatus,
                ReviewedBy = submission.ReviewedBy,
                ReviewedAt = submission.ReviewedAt,
                RejectionReason = submission.RejectionReason,
                Notes = submission.Notes,
                TotalEmployeesCount = submission.TotalEmployeesCount,
                PresentCount = submission.PresentCount,
                AbsentCount = submission.AbsentCount,
                Records = submission.Records.Select(r => new AttendanceRecordItemDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    FullName = !string.IsNullOrWhiteSpace(r.EmployeeName) ? r.EmployeeName : (employees.FirstOrDefault(e => e.UserId == r.UserId)?.FullName ?? "Unknown"),
                    Role = !string.IsNullOrWhiteSpace(r.EmployeeRole) ? r.EmployeeRole : (employees.FirstOrDefault(e => e.UserId == r.UserId)?.Role ?? "Employee"),
                    Status = r.Status,
                    AttendanceDateTime = r.CheckInTime,
                    CheckInTime = r.CheckInTime,
                    CheckOutTime = r.CheckOutTime,
                    Notes = r.Notes,
                    IsTestData = r.IsTestData,
                    ApprovalStatus = r.ApprovalStatus,
                    ReviewedBy = r.ReviewedBy,
                    ReviewedByUserId = r.ReviewedByUserId,
                    ReviewedAt = r.ReviewedAt,
                    RejectionReason = r.RejectionReason
                }).ToList(),
                Attachments = submission.Attachments.Select(a => new AttendanceAttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    OriginalFileName = a.OriginalFileName,
                    FileType = a.FileType,
                    FileSize = a.FileSize,
                    UploadedAt = a.UploadedAt,
                    ViewUrl = $"/api/control-monitoring/attachments/attendance/{a.FileName}",
                    DownloadUrl = $"/api/control-monitoring/attachments/attendance/{a.FileName}?download=true"
                }).ToList(),
                AuditHistory = audit
            };
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            date = targetDate.ToString("yyyy-MM-dd"),
            hasSubmission = submission != null,
            submissionId = submission?.Id,
            approvalStatus = submission?.ApprovalStatus ?? "DRAFT",
            submission = subDto,
            employees = employeeDtos
        }));
    }

    /// <summary>
    /// Retrieve details of a specific submission by ID.
    /// </summary>
    [HttpGet("submissions/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceSubmissionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubmissionById(int id)
    {
        var sub = await _db.AttendanceSubmissions
            .AsNoTracking()
            .Include(s => s.Records)
                .ThenInclude(r => r.User)
            .Include(s => s.Attachments)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sub == null)
        {
            return NotFound(ApiResponse.Fail($"Attendance submission #{id} not found."));
        }

        var audit = await _db.FormApprovalHistories
            .AsNoTracking()
            .Where(h => h.FormType.ToLower() == "attendance" && h.FormId == sub.Id)
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new FormApprovalHistoryDto
            {
                Id = h.Id,
                FormType = h.FormType,
                FormId = h.FormId,
                PONumber = h.PONumber,
                TransactionOrMovementNumber = h.TransactionOrMovementNumber,
                PreviousStatus = h.PreviousStatus,
                NewStatus = h.NewStatus,
                Action = h.Action,
                ActorName = h.ActorName,
                ActorRole = h.ActorRole,
                Comment = h.Comment,
                Timestamp = h.Timestamp
            })
            .ToListAsync();

        var dto = new AttendanceSubmissionDetailDto
        {
            Id = sub.Id,
            AttendanceDate = sub.AttendanceDate,
            SubmittedByUserId = sub.SubmittedByUserId,
            SubmittedBy = sub.SubmittedBy,
            SubmittedAt = sub.SubmittedAt,
            ApprovalStatus = sub.ApprovalStatus,
            ReviewedBy = sub.ReviewedBy,
            ReviewedAt = sub.ReviewedAt,
            RejectionReason = sub.RejectionReason,
            Notes = sub.Notes,
            TotalEmployeesCount = sub.TotalEmployeesCount,
            PresentCount = sub.PresentCount,
            AbsentCount = sub.AbsentCount,
            Records = sub.Records.Select(r => new AttendanceRecordItemDto
            {
                Id = r.Id,
                UserId = r.UserId,
                FullName = !string.IsNullOrWhiteSpace(r.EmployeeName) ? r.EmployeeName : (r.User?.FullName ?? "Unknown"),
                Role = !string.IsNullOrWhiteSpace(r.EmployeeRole) ? r.EmployeeRole : (r.User?.Role ?? "Employee"),
                Status = r.Status,
                AttendanceDateTime = r.CheckInTime,
                CheckInTime = r.CheckInTime,
                CheckOutTime = r.CheckOutTime,
                Notes = r.Notes,
                IsTestData = r.IsTestData,
                ApprovalStatus = r.ApprovalStatus,
                ReviewedBy = r.ReviewedBy,
                ReviewedByUserId = r.ReviewedByUserId,
                ReviewedAt = r.ReviewedAt,
                RejectionReason = r.RejectionReason
            }).ToList(),
            Attachments = sub.Attachments.Select(a => new AttendanceAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                OriginalFileName = a.OriginalFileName,
                FileType = a.FileType,
                FileSize = a.FileSize,
                UploadedAt = a.UploadedAt,
                ViewUrl = $"/api/control-monitoring/attachments/attendance/{a.FileName}",
                DownloadUrl = $"/api/control-monitoring/attachments/attendance/{a.FileName}?download=true"
            }).ToList(),
            AuditHistory = audit
        };

        return Ok(ApiResponse<AttendanceSubmissionDetailDto>.Ok(dto));
    }

    /// <summary>
    /// Submit attendance sheet for Control & Monitoring review.
    /// CRITICAL REQUIREMENT: Supporting attachment upload is mandatory.
    /// Checkboxes must be mutually exclusive.
    /// Status is set to PENDING upon submission.
    /// </summary>
    [HttpPost("submit")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceSubmissionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitAttendance(
        [FromForm] DateTime? attendanceDate,
        [FromForm] string? notes,
        [FromForm] string? recordsJson,
        [FromForm] List<IFormFile>? attachments)
    {
        var currentUserName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "مسؤول الحضور";
        Guid? currentUserId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            currentUserId = parsedGuid;
        }
        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Employee";

        // Requirement 1: Enforce role authorization on backend
        if (!AppRoles.CanSubmitAttendance(currentUserRole))
        {
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse.Fail("غير مصرح: تسجيل وإرسال كشوف الحضور اليومية مقتصر على مسؤول الحضور والانصراف (ATTENDANCE_EMPLOYEE) أو الإدارة العليا."));
        }

        // Requirement 3: Strictly enforce today's date in local business timezone
        var businessToday = GetBusinessToday();
        if (attendanceDate.HasValue && attendanceDate.Value != default && attendanceDate.Value.Date != businessToday)
        {
            return BadRequest(ApiResponse.Fail($"تاريخ الحضور غير صالح. يسمح بتسجيل الحضور لتاريخ اليوم فقط ({businessToday:yyyy-MM-dd}). لا يمكن اختيار تواريخ سابقة أو مستقبلية."));
        }
        var targetDate = businessToday;

        // Requirement 3: Do not allow the same daily attendance form to be submitted more than once for the same date unless rejected
        var existingTodaySub = await _db.AttendanceSubmissions
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(s => s.AttendanceDate.Date == targetDate);

        if (existingTodaySub != null)
        {
            if (existingTodaySub.ApprovalStatus == "APPROVED")
            {
                return BadRequest(ApiResponse.Fail($"تم اعتماد كشف حضور اليوم ({targetDate:yyyy-MM-dd}) رسمياً بالفعل ولا يمكن إعادة تقديمه."));
            }
            if (existingTodaySub.ApprovalStatus == "PENDING")
            {
                return BadRequest(ApiResponse.Fail($"كشف حضور اليوم ({targetDate:yyyy-MM-dd}) قيد المراجعة حالياً لدى قسم المراقبة والتحكم (ATT-{existingTodaySub.Id:D4})."));
            }
        }

        // 2. Parse and validate records
        if (string.IsNullOrWhiteSpace(recordsJson))
        {
            return BadRequest(ApiResponse.Fail("بيانات سجلات الحضور مطلوبة / Attendance records are required."));
        }

        List<AttendanceRecordItemDto>? records;
        try
        {
            records = JsonSerializer.Deserialize<List<AttendanceRecordItemDto>>(recordsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse recordsJson");
            return BadRequest(ApiResponse.Fail("تنسيق بيانات سجلات الحضور غير صالح."));
        }

        if (records == null || records.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("يجب تسجيل حضور موظف واحد على الأقل قبل التقديم."));
        }

        // 3. Mutual exclusivity validation and duplicate check
        var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Present", "Absent" };
        var seenUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var validEmployees = await GetEmployeesListAsync();
        var validIds = validEmployees.Select(e => e.UserId).ToHashSet();
        var validNames = validEmployees.Select(e => e.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var rec in records)
        {
            var userKey = rec.UserId.HasValue ? rec.UserId.Value.ToString() : rec.FullName;
            if (string.IsNullOrWhiteSpace(userKey) || !seenUsers.Add(userKey))
            {
                return BadRequest(ApiResponse.Fail($"تكرار غير مسموح لنفس الموظف في الكشف: {rec.FullName}"));
            }

            if (!validStatuses.Contains(rec.Status))
            {
                return BadRequest(ApiResponse.Fail($"حالة الحضور للموظف {rec.FullName} يجب أن تكون حاضر (Present) أو غائب (Absent) حصراً ولا يمكن الجمع بينهما."));
            }

            if (rec.UserId.HasValue && !validIds.Contains(rec.UserId.Value) && !validNames.Contains(rec.FullName))
            {
                return BadRequest(ApiResponse.Fail($"الموظف {rec.FullName} غير مسجل في قائمة كشف الحضور."));
            }

            // Individual date and time validation for each employee
            var rawTimestamp = rec.AttendanceDateTime ?? rec.CheckInTime;
            if (rec.Status.Equals("Present", StringComparison.OrdinalIgnoreCase))
            {
                if (!rawTimestamp.HasValue || rawTimestamp.Value == default)
                {
                    return BadRequest(ApiResponse.Fail($"تاريخ ووقت الحضور مطلوب ومحدد للموظف الحاضر: {rec.FullName}."));
                }

                var timestampUtc = ConvertToUtcFromBusiness(rawTimestamp.Value);
                if (timestampUtc > DateTime.UtcNow.AddMinutes(5))
                {
                    return BadRequest(ApiResponse.Fail($"لا يمكن تسجيل وقت حضور في المستقبل للموظف: {rec.FullName}."));
                }
            }
            else if (rec.Status.Equals("Absent", StringComparison.OrdinalIgnoreCase))
            {
                if (rawTimestamp.HasValue && rawTimestamp.Value != default)
                {
                    var timestampUtc = ConvertToUtcFromBusiness(rawTimestamp.Value);
                    if (timestampUtc > DateTime.UtcNow.AddMinutes(5))
                    {
                        return BadRequest(ApiResponse.Fail($"لا يمكن تسجيل تاريخ ووقت في المستقبل للموظف: {rec.FullName}."));
                    }
                }
            }
        }

        // 4. CRITICAL REQUIREMENT: MANDATORY ATTACHMENT UPLOAD (ONE supporting document at the end)
        if (attachments == null || attachments.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("مرفق إثبات الحضور اليومي (Attendance Evidence) مطلوب وإلزامي في نهاية النموذج قبل الإرسال."));
        }

        // Validate each attachment (extension, size, magic bytes)
        var validatedFiles = new List<(IFormFile file, string uniqueFileName, string origName, string ext, long size)>();
        foreach (var file in attachments)
        {
            if (file.Length == 0) continue;

            if (file.Length > 20 * 1024 * 1024)
            {
                return BadRequest(ApiResponse.Fail($"حجم الملف '{file.FileName}' يتجاوز الحد الأقصى المسموح (20 ميجابايت)."));
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                return BadRequest(ApiResponse.Fail($"نوع الملف '{file.FileName}' غير مدعوم. الأنواع المسموحة: PDF, JPG, PNG."));
            }

            // Magic bytes verification
            byte[] magic = new byte[8];
            using (var stream = file.OpenReadStream())
            {
                var bytesRead = await stream.ReadAsync(magic, 0, magic.Length);
                if (bytesRead < 4)
                {
                    return BadRequest(ApiResponse.Fail($"محتوى الملف '{file.FileName}' تالف أو غير صالح."));
                }

                if (ext == ".pdf")
                {
                    if (magic[0] != 0x25 || magic[1] != 0x50 || magic[2] != 0x44 || magic[3] != 0x46) // %PDF
                    {
                        return BadRequest(ApiResponse.Fail($"الملف '{file.FileName}' ليس ملف PDF صالحاً."));
                    }
                }
                else if (ext == ".jpg" || ext == ".jpeg")
                {
                    if (magic[0] != 0xFF || magic[1] != 0xD8 || magic[2] != 0xFF)
                    {
                        return BadRequest(ApiResponse.Fail($"الملف '{file.FileName}' ليس صورة JPG صالحة."));
                    }
                }
                else if (ext == ".png")
                {
                    if (magic[0] != 0x89 || magic[1] != 0x50 || magic[2] != 0x4E || magic[3] != 0x47)
                    {
                        return BadRequest(ApiResponse.Fail($"الملف '{file.FileName}' ليس صورة PNG صالحة."));
                    }
                }
            }

            var uniqueName = $"{Guid.NewGuid():N}{ext}";
            validatedFiles.Add((file, uniqueName, Path.GetFileName(file.FileName), ext, file.Length));
        }

        if (validatedFiles.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("لم يتم العثور على ملفات صالحة في المرفقات المرفوعة."));
        }

        // Save files to disk in wwwroot/uploads/attendance/
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "attendance");
        if (!Directory.Exists(uploadsDir))
        {
            Directory.CreateDirectory(uploadsDir);
        }

        foreach (var vf in validatedFiles)
        {
            var destPath = Path.Combine(uploadsDir, vf.uniqueFileName);
            using var destStream = new FileStream(destPath, FileMode.Create);
            await vf.file.CopyToAsync(destStream);
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // Create AttendanceSubmission
            var presentCount = records.Count(r => r.Status.Equals("Present", StringComparison.OrdinalIgnoreCase));
            var absentCount = records.Count(r => r.Status.Equals("Absent", StringComparison.OrdinalIgnoreCase));

            var submission = new AttendanceSubmission
            {
                AttendanceDate = targetDate,
                SubmittedByUserId = currentUserId,
                SubmittedBy = currentUserName,
                SubmittedAt = DateTime.UtcNow,
                ApprovalStatus = "PENDING",
                Notes = notes,
                TotalEmployeesCount = records.Count,
                PresentCount = presentCount,
                AbsentCount = absentCount
            };

            await _db.AttendanceSubmissions.AddAsync(submission);
            await _db.SaveChangesAsync();

            // Insert individual records (isolated test data marked with IsTestData = true)
            foreach (var r in records)
            {
                var rawTimestamp = r.AttendanceDateTime ?? r.CheckInTime;
                DateTime? timestampUtc = rawTimestamp.HasValue && rawTimestamp.Value != default
                    ? ConvertToUtcFromBusiness(rawTimestamp.Value)
                    : null;

                var record = new AttendanceRecord
                {
                    UserId = r.UserId,
                    EmployeeName = r.FullName,
                    EmployeeRole = r.Role,
                    IsTestData = r.IsTestData,
                    Date = timestampUtc?.Date ?? targetDate,
                    Status = r.Status,
                    CheckInTime = r.Status.Equals("Present", StringComparison.OrdinalIgnoreCase)
                        ? timestampUtc
                        : timestampUtc,
                    CheckOutTime = r.CheckOutTime,
                    Notes = r.Notes,
                    AttendanceSubmissionId = submission.Id,
                    ApprovalStatus = "PENDING",
                    CreatedAt = DateTime.UtcNow
                };
                await _db.AttendanceRecords.AddAsync(record);
            }

            // Create attachment records
            foreach (var vf in validatedFiles)
            {
                var att = new AttendanceAttachment
                {
                    AttendanceSubmissionId = submission.Id,
                    FileName = vf.uniqueFileName,
                    OriginalFileName = vf.origName,
                    FilePath = $"/uploads/attendance/{vf.uniqueFileName}",
                    FileType = vf.ext == ".pdf" ? "application/pdf" : (vf.ext == ".png" ? "image/png" : "image/jpeg"),
                    FileSize = vf.size,
                    UploadedAt = DateTime.UtcNow
                };
                await _db.AttendanceAttachments.AddAsync(att);
            }

            // Record in FormApprovalHistories
            var txNumber = $"ATT-{submission.Id:D4}";
            var history = new FormApprovalHistory
            {
                FormType = "Attendance",
                FormId = submission.Id,
                PONumber = null,
                TransactionOrMovementNumber = txNumber,
                PreviousStatus = "DRAFT",
                NewStatus = "PENDING",
                Action = "SUBMITTED",
                ActorName = currentUserName,
                ActorUserId = currentUserId,
                ActorRole = currentUserRole,
                Comment = "تم إرسال كشف الحضور والانصراف مع المرفقات الثبوتية للمراجعة والاعتماد.",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(history);

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Attendance submission #{Id} created for date {Date} by {User}", submission.Id, targetDate, currentUserName);

            // SignalR Broadcast to Control & Monitoring
            await NotifyControlMonitoringAsync("Attendance", submission.Id, txNumber, "PENDING", currentUserName, currentUserId);

            // Return full details
            return await GetSubmissionById(submission.Id);
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Error submitting attendance for date {Date}", targetDate);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ كشف الحضور والانصراف."));
        }
    }

    /// <summary>
    /// Edit and resubmit a rejected attendance sheet.
    /// Updates records, optionally adds replacement attachments, and transitions status to PENDING.
    /// Previous rejection history is strictly preserved in FormApprovalHistories.
    /// </summary>
    [HttpPost("submissions/{id:int}/resubmit")]
    [HttpPost("resubmit/{id:int}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceSubmissionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResubmitAttendance(
        int id,
        [FromForm] string? notes,
        [FromForm] string? recordsJson,
        [FromForm] List<IFormFile>? attachments)
    {
        var currentUserName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "مسؤول الحضور";
        Guid? currentUserId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            currentUserId = parsedGuid;
        }
        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Employee";

        // Requirement 1: Enforce role authorization
        if (!AppRoles.CanSubmitAttendance(currentUserRole))
        {
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse.Fail("غير مصرح: إعادة تقديم كشف الحضور مقتصر على مسؤول الحضور والانصراف (ATTENDANCE_EMPLOYEE) أو الإدارة العليا."));
        }

        var submission = await _db.AttendanceSubmissions
            .Include(s => s.Records)
            .Include(s => s.Attachments)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (submission == null)
        {
            return NotFound(ApiResponse.Fail($"Attendance submission #{id} not found."));
        }

        if (submission.ApprovalStatus == "APPROVED")
        {
            return BadRequest(ApiResponse.Fail("لا يمكن إعادة تقديم كشف معتمد رسمياً."));
        }

        // Parse records
        if (string.IsNullOrWhiteSpace(recordsJson))
        {
            return BadRequest(ApiResponse.Fail("بيانات سجلات الحضور مطلوبة لإعادة التقديم."));
        }

        List<AttendanceRecordItemDto>? records;
        try
        {
            records = JsonSerializer.Deserialize<List<AttendanceRecordItemDto>>(recordsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse recordsJson during resubmission");
            return BadRequest(ApiResponse.Fail("تنسيق بيانات سجلات الحضور غير صالح."));
        }

        if (records == null || records.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("يجب تسجيل حضور موظف واحد على الأقل."));
        }

        // Validate mutual exclusivity
        var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Present", "Absent" };
        var seenUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rec in records)
        {
            var userKey = rec.UserId.HasValue ? rec.UserId.Value.ToString() : rec.FullName;
            if (string.IsNullOrWhiteSpace(userKey) || !seenUsers.Add(userKey) || !validStatuses.Contains(rec.Status))
            {
                return BadRequest(ApiResponse.Fail($"بيانات الحضور غير صالحة للموظف {rec.FullName}."));
            }

            // Validate individual date and time for each employee
            var rawTimestamp = rec.AttendanceDateTime ?? rec.CheckInTime;
            if (rec.Status.Equals("Present", StringComparison.OrdinalIgnoreCase))
            {
                if (!rawTimestamp.HasValue || rawTimestamp.Value == default)
                {
                    return BadRequest(ApiResponse.Fail($"تاريخ ووقت الحضور مطلوب ومحدد للموظف الحاضر: {rec.FullName}."));
                }
                var timestampUtc = ConvertToUtcFromBusiness(rawTimestamp.Value);
                if (timestampUtc > DateTime.UtcNow.AddMinutes(5))
                {
                    return BadRequest(ApiResponse.Fail($"لا يمكن تسجيل وقت حضور في المستقبل للموظف: {rec.FullName}."));
                }
            }
            else if (rec.Status.Equals("Absent", StringComparison.OrdinalIgnoreCase))
            {
                if (rawTimestamp.HasValue && rawTimestamp.Value != default)
                {
                    var timestampUtc = ConvertToUtcFromBusiness(rawTimestamp.Value);
                    if (timestampUtc > DateTime.UtcNow.AddMinutes(5))
                    {
                        return BadRequest(ApiResponse.Fail($"لا يمكن تسجيل تاريخ ووقت في المستقبل للموظف: {rec.FullName}."));
                    }
                }
            }
        }

        // Process any newly uploaded attachments
        var validatedFiles = new List<(IFormFile file, string uniqueFileName, string origName, string ext, long size)>();
        if (attachments != null && attachments.Count > 0)
        {
            foreach (var file in attachments)
            {
                if (file.Length == 0) continue;
                if (file.Length > 20 * 1024 * 1024)
                {
                    return BadRequest(ApiResponse.Fail($"حجم الملف '{file.FileName}' يتجاوز الحد الأقصى المسموح."));
                }

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext))
                {
                    return BadRequest(ApiResponse.Fail($"نوع الملف '{file.FileName}' غير مدعوم."));
                }

                // Magic bytes
                byte[] magic = new byte[8];
                using (var stream = file.OpenReadStream())
                {
                    var bytesRead = await stream.ReadAsync(magic, 0, magic.Length);
                    if (bytesRead < 4)
                    {
                        return BadRequest(ApiResponse.Fail($"محتوى الملف '{file.FileName}' غير صالح."));
                    }

                    if (ext == ".pdf" && (magic[0] != 0x25 || magic[1] != 0x50 || magic[2] != 0x44 || magic[3] != 0x46))
                    {
                        return BadRequest(ApiResponse.Fail($"الملف '{file.FileName}' ليس PDF صالحاً."));
                    }
                    else if ((ext == ".jpg" || ext == ".jpeg") && (magic[0] != 0xFF || magic[1] != 0xD8 || magic[2] != 0xFF))
                    {
                        return BadRequest(ApiResponse.Fail($"الملف '{file.FileName}' ليس JPG صالحاً."));
                    }
                    else if (ext == ".png" && (magic[0] != 0x89 || magic[1] != 0x50 || magic[2] != 0x4E || magic[3] != 0x47))
                    {
                        return BadRequest(ApiResponse.Fail($"الملف '{file.FileName}' ليس PNG صالحاً."));
                    }
                }

                var uniqueName = $"{Guid.NewGuid():N}{ext}";
                validatedFiles.Add((file, uniqueName, Path.GetFileName(file.FileName), ext, file.Length));
            }
        }

        // Verify that there is at least one attachment (either existing or newly uploaded)
        if (submission.Attachments.Count == 0 && validatedFiles.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("مرفقات إثبات الحضور إلزامية لإعادة التقديم."));
        }

        // Save new files to disk
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "attendance");
        if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

        foreach (var vf in validatedFiles)
        {
            var destPath = Path.Combine(uploadsDir, vf.uniqueFileName);
            using var destStream = new FileStream(destPath, FileMode.Create);
            await vf.file.CopyToAsync(destStream);
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var prevStatus = submission.ApprovalStatus;
            var presentCount = records.Count(r => r.Status.Equals("Present", StringComparison.OrdinalIgnoreCase));
            var absentCount = records.Count(r => r.Status.Equals("Absent", StringComparison.OrdinalIgnoreCase));

            // Update submission state
            submission.ApprovalStatus = "PENDING";
            submission.SubmittedAt = DateTime.UtcNow;
            submission.SubmittedBy = currentUserName;
            submission.SubmittedByUserId = currentUserId;
            submission.RejectionReason = null;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                submission.Notes = notes;
            }
            submission.TotalEmployeesCount = records.Count;
            submission.PresentCount = presentCount;
            submission.AbsentCount = absentCount;

            // Update or add individual attendance records
            var existingByUserId = submission.Records.Where(r => r.UserId.HasValue).ToDictionary(r => r.UserId!.Value, r => r);
            var existingByName = submission.Records.Where(r => !string.IsNullOrWhiteSpace(r.EmployeeName))
                .ToDictionary(r => r.EmployeeName!, r => r, StringComparer.OrdinalIgnoreCase);

            foreach (var r in records)
            {
                AttendanceRecord? existingRec = null;
                if (r.UserId.HasValue && existingByUserId.TryGetValue(r.UserId.Value, out var recById))
                {
                    existingRec = recById;
                }
                else if (!string.IsNullOrWhiteSpace(r.FullName) && existingByName.TryGetValue(r.FullName, out var recByName))
                {
                    existingRec = recByName;
                }

                var rawTimestamp = r.AttendanceDateTime ?? r.CheckInTime;
                DateTime? timestampUtc = rawTimestamp.HasValue && rawTimestamp.Value != default
                    ? ConvertToUtcFromBusiness(rawTimestamp.Value)
                    : null;

                if (existingRec != null)
                {
                    existingRec.Status = r.Status;
                    // Requirement 6: Approved records remain approved when another record is rejected
                    // Only rejected records or pending records reset to PENDING
                    if (existingRec.ApprovalStatus == "REJECTED")
                    {
                        existingRec.ApprovalStatus = "PENDING";
                        existingRec.RejectionReason = null;
                        existingRec.ReviewedBy = null;
                        existingRec.ReviewedByUserId = null;
                        existingRec.ReviewedAt = null;
                    }

                    existingRec.CheckInTime = r.Status.Equals("Present", StringComparison.OrdinalIgnoreCase)
                        ? timestampUtc
                        : timestampUtc;
                    existingRec.Date = timestampUtc?.Date ?? existingRec.Date;
                    existingRec.Notes = r.Notes;
                    if (string.IsNullOrWhiteSpace(existingRec.EmployeeName) && !string.IsNullOrWhiteSpace(r.FullName))
                    {
                        existingRec.EmployeeName = r.FullName;
                    }
                    if (string.IsNullOrWhiteSpace(existingRec.EmployeeRole) && !string.IsNullOrWhiteSpace(r.Role))
                    {
                        existingRec.EmployeeRole = r.Role;
                    }
                    if (r.IsTestData)
                    {
                        existingRec.IsTestData = true;
                    }
                }
                else
                {
                    var newRec = new AttendanceRecord
                    {
                        UserId = r.UserId,
                        EmployeeName = r.FullName,
                        EmployeeRole = r.Role,
                        IsTestData = r.IsTestData || !r.UserId.HasValue,
                        Date = timestampUtc?.Date ?? submission.AttendanceDate,
                        Status = r.Status,
                        CheckInTime = r.Status.Equals("Present", StringComparison.OrdinalIgnoreCase)
                            ? timestampUtc
                            : timestampUtc,
                        Notes = r.Notes,
                        AttendanceSubmissionId = submission.Id,
                        ApprovalStatus = "PENDING",
                        CreatedAt = DateTime.UtcNow
                    };
                    await _db.AttendanceRecords.AddAsync(newRec);
                }
            }

            // Recalculate submission approval status from the records
            submission.ApprovalStatus = CalculateSubmissionStatus(submission.Records);

            // Add new attachments if uploaded
            foreach (var vf in validatedFiles)
            {
                var att = new AttendanceAttachment
                {
                    AttendanceSubmissionId = submission.Id,
                    FileName = vf.uniqueFileName,
                    OriginalFileName = vf.origName,
                    FilePath = $"/uploads/attendance/{vf.uniqueFileName}",
                    FileType = vf.ext == ".pdf" ? "application/pdf" : (vf.ext == ".png" ? "image/png" : "image/jpeg"),
                    FileSize = vf.size,
                    UploadedAt = DateTime.UtcNow
                };
                await _db.AttendanceAttachments.AddAsync(att);
            }

            // Append RESUBMITTED event to FormApprovalHistories (preserves original rejection)
            var txNumber = $"ATT-{submission.Id:D4}";
            var history = new FormApprovalHistory
            {
                FormType = "Attendance",
                FormId = submission.Id,
                PONumber = null,
                TransactionOrMovementNumber = txNumber,
                PreviousStatus = prevStatus,
                NewStatus = "PENDING",
                Action = "RESUBMITTED",
                ActorName = currentUserName,
                ActorUserId = currentUserId,
                ActorRole = currentUserRole,
                Comment = "تم تعديل وإعادة تقديم كشف الحضور والانصراف للمراجعة بعد الرفض السابق.",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(history);

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Attendance submission #{Id} resubmitted by {User}", submission.Id, currentUserName);

            // SignalR Broadcast
            await NotifyControlMonitoringAsync("Attendance", submission.Id, txNumber, "PENDING", currentUserName, currentUserId);

            return await GetSubmissionById(submission.Id);
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Error resubmitting attendance #{Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء إعادة إرسال كشف الحضور."));
        }
    }

    /// <summary>
    /// Stream / Preview an attendance attachment file for authorized users.
    /// </summary>
    [HttpGet("attachments/{fileName}")]
    [HttpGet("/api/attendance/attachments/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttachment(string fileName, [FromQuery] bool download = false)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            return BadRequest(ApiResponse.Fail("Invalid file name."));
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var filePath = Path.Combine(webRoot, "uploads", "attendance", fileName);

        if (!System.IO.File.Exists(filePath))
        {
            var fallback = Path.Combine(_env.ContentRootPath, "uploads", "attendance", fileName);
            if (System.IO.File.Exists(fallback)) filePath = fallback;
        }

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(ApiResponse.Fail("Attachment file not found."));
        }

        var att = await _db.AttendanceAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.FileName == fileName);
        var origName = att?.OriginalFileName ?? fileName;

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        string contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };

        var encodedName = Uri.EscapeDataString(origName);

        if (download)
        {
            return PhysicalFile(filePath, contentType, fileDownloadName: origName, enableRangeProcessing: true);
        }

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{encodedName}\"; filename*=UTF-8''{encodedName}";
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }

    private async Task NotifyControlMonitoringAsync(string formType, int formId, string txNumber, string status, string submitterName, Guid? submitterUserId)
    {
        try
        {
            var payload = new
            {
                department = "attendance",
                action = status,
                formType,
                formId,
                po = (string?)null,
                txNumber,
                status,
                submitterName,
                submitterUserId = submitterUserId?.ToString(),
                timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", payload);
            await _hubContext.Clients.All.SendAsync("OnFormApprovalStatusChanged", payload);
            await _hubContext.Clients.All.SendAsync("OnFormSubmittedForReview", payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast SignalR notification for Attendance #{Id}", formId);
        }
    }
}
