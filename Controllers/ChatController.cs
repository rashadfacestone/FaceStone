using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using FaceStone.Web.Data;
using FaceStone.Web.Hubs;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;

namespace FaceStone.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly FaceStoneDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        FaceStoneDbContext context,
        IWebHostEnvironment env,
        IHubContext<ChatHub> hubContext,
        ILogger<ChatController> logger)
    {
        _context = context;
        _env = env;
        _hubContext = hubContext;
        _logger = logger;
    }

    private (Guid UserId, string FullName, string Username, string Role, bool IsAccountant) GetCurrentUser()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(idClaim, out var userId);
        var fullName = User.FindFirst(ClaimTypes.Name)?.Value ?? "User";
        var username = User.FindFirst("username")?.Value ?? "user";
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Employee";
        bool isAccountant = role.Equals("Accountant", StringComparison.OrdinalIgnoreCase) ||
                            role.Equals("CEO", StringComparison.OrdinalIgnoreCase);

        return (userId, fullName, username, role, isAccountant);
    }

    /// <summary>
    /// Send a chat message with optional file attachment.
    /// </summary>
    [HttpPost("send")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendMessage([FromForm] SendMessageDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail(string.Join("; ", errors)));
        }

        var (userId, fullName, username, role, isAccountant) = GetCurrentUser();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse.Fail("Invalid user token."));
        }

        if (string.IsNullOrWhiteSpace(request.Message) && (request.Attachment == null || request.Attachment.Length == 0))
        {
            return BadRequest(ApiResponse.Fail("Please provide a message text or attach a file."));
        }

        // Idempotency check: if ClientMessageId is provided, check if already exists for this sender
        if (!string.IsNullOrWhiteSpace(request.ClientMessageId))
        {
            var cleanClientMsgId = request.ClientMessageId.Trim();
            var existing = await _context.ChatMessages
                .Include(m => m.Attachments)
                .FirstOrDefaultAsync(m => m.SenderId == userId && m.ClientMessageId == cleanClientMsgId);

            if (existing != null)
            {
                _logger.LogInformation("Idempotent send detected: ClientMessageId {ClientMsgId} from user {UserId}. Returning existing message.",
                    cleanClientMsgId, userId);
                var existingDto = MapToDto(existing, existing.Attachments.FirstOrDefault());
                return Ok(ApiResponse<ChatMessageDto>.Ok(existingDto, "Message already processed."));
            }
        }

        // Determine recipient details
        Guid? recipientId = null;
        string recipientName;
        string? recipientUsername = null;
        string? recipientRole = null;

        if (isAccountant)
        {
            if (!request.TargetEmployeeId.HasValue || request.TargetEmployeeId.Value == Guid.Empty)
            {
                return BadRequest(ApiResponse.Fail("Please select an employee conversation before sending a message."));
            }
            var targetEmp = await _context.Users.FindAsync(request.TargetEmployeeId.Value);
            if (targetEmp == null)
            {
                return NotFound(ApiResponse.Fail("Recipient employee was not found."));
            }
            recipientId = targetEmp.Id;
            recipientName = targetEmp.FullName;
            recipientUsername = targetEmp.Username;
            recipientRole = targetEmp.Role;
        }
        else
        {
            recipientId = null;
            recipientName = "Accounting Desk";
            recipientUsername = "accounting_team";
            recipientRole = "Accountant";
        }

        // 1. Create and save ChatMessage
        var chatMsg = new ChatMessage
        {
            SenderId = userId,
            SenderName = fullName,
            SenderUsername = username,
            SenderRole = isAccountant ? "Accountant" : role,
            RecipientId = recipientId,
            RecipientName = recipientName,
            RecipientUsername = recipientUsername,
            RecipientRole = recipientRole,
            MessageText = request.Message?.Trim() ?? string.Empty,
            HasAttachment = (request.Attachment != null && request.Attachment.Length > 0),
            IsFromAccountant = isAccountant,
            IsReadByAccountant = isAccountant,
            IsReadByUser = !isAccountant,
            SentAt = DateTime.UtcNow,
            ClientMessageId = request.ClientMessageId?.Trim()
        };

        await _context.ChatMessages.AddAsync(chatMsg);
        await _context.SaveChangesAsync();

        // 2. Handle Attachment if present
        ChatAttachment? chatAttachment = null;
        if (request.Attachment != null && request.Attachment.Length > 0)
        {
            var ext = Path.GetExtension(request.Attachment.FileName).ToLowerInvariant();
            var disallowed = new[] { ".exe", ".bat", ".cmd", ".sh", ".dll", ".msi", ".vbs", ".ps1" };
            if (disallowed.Contains(ext))
            {
                return BadRequest(ApiResponse.Fail("Executable files cannot be uploaded for security reasons."));
            }

            string fileType = "document";
            if (ext == ".pdf") fileType = "pdf";
            else if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif") fileType = "image";
            else if (ext is ".doc" or ".docx") fileType = "word";
            else if (ext is ".xls" or ".xlsx" or ".csv") fileType = "excel";

            // Secure file storage outside wwwroot to prevent dotnet watch Roslyn hot reload workspace crashes
            var rootDir = _env.ContentRootPath ?? Directory.GetCurrentDirectory();
            var uploadsDir = Path.Combine(rootDir, "uploads", "chat");
            if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

            var originalFileName = Path.GetFileName(request.Attachment.FileName);
            var storedFileName = $"att_{Guid.NewGuid():N}{ext}";
            var fullDiskPath = Path.Combine(uploadsDir, storedFileName);

            using (var stream = new FileStream(fullDiskPath, FileMode.Create))
            {
                await request.Attachment.CopyToAsync(stream);
            }

            chatAttachment = new ChatAttachment
            {
                MessageId = chatMsg.Id,
                SenderName = fullName,
                RecipientName = recipientName,
                FileName = originalFileName,
                FilePath = storedFileName,
                FileType = fileType,
                FileSizeBytes = request.Attachment.Length,
                UploadedAt = DateTime.UtcNow
            };

            await _context.ChatAttachments.AddAsync(chatAttachment);
            await _context.SaveChangesAsync();
        }

        var messageDto = MapToDto(chatMsg, chatAttachment);

        // 3. Real-Time Broadcast via SignalR
        try
        {
            if (isAccountant)
            {
                await _hubContext.Clients.Group($"Employee_{recipientId}").SendAsync("OnAccountantReply", messageDto);
                await _hubContext.Clients.Group("Accountants").SendAsync("OnAccountantReply", messageDto);
            }
            else
            {
                await _hubContext.Clients.Group("Accountants").SendAsync("OnNewEmployeeMessage", messageDto);
                await _hubContext.Clients.Group($"Employee_{userId}").SendAsync("OnNewMessage", messageDto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR broadcast notification caught exception (fallback to polling).");
        }

        return StatusCode(StatusCodes.Status201Created, ApiResponse<ChatMessageDto>.Ok(messageDto, "Message sent successfully."));
    }

    /// <summary>
    /// Retrieve conversation threads across all employees (Accountant & CEO only).
    /// </summary>
    [HttpGet("conversations")]
    [Authorize(Roles = "Accountant,CEO")]
    [ProducesResponseType(typeof(ApiResponse<ConversationsListResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConversations()
    {
        var employees = await _context.Users
            .Where(u => u.Role != "Accountant" && u.Role != "CEO")
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var summaries = new List<ConversationSummaryDto>();
        int totalUnread = 0;

        foreach (var emp in employees)
        {
            var lastMsg = await _context.ChatMessages
                .Where(m => m.SenderId == emp.Id || m.RecipientId == emp.Id)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync();

            var unreadCount = await _context.ChatMessages
                .CountAsync(m => m.SenderId == emp.Id && !m.IsFromAccountant && !m.IsReadByAccountant);

            totalUnread += unreadCount;

            string? attType = null;
            if (lastMsg != null && lastMsg.HasAttachment)
            {
                var att = await _context.ChatAttachments
                    .Where(a => a.MessageId == lastMsg.Id)
                    .FirstOrDefaultAsync();
                attType = att?.FileType;
            }

            summaries.Add(new ConversationSummaryDto
            {
                EmployeeId = emp.Id,
                FullName = emp.FullName,
                Username = emp.Username,
                Role = emp.Role,
                Email = emp.Email,
                PhoneNumber = emp.PhoneNumber,
                LastMessageText = lastMsg != null 
                    ? (!string.IsNullOrEmpty(lastMsg.MessageText) ? lastMsg.MessageText : "[Attachment]")
                    : "New conversation",
                LastMessageTime = lastMsg?.SentAt,
                UnreadCount = unreadCount,
                HasAttachment = lastMsg?.HasAttachment ?? false,
                AttachmentType = attType
            });
        }

        var sorted = summaries
            .OrderByDescending(c => c.UnreadCount > 0)
            .ThenByDescending(c => c.LastMessageTime ?? DateTime.MinValue)
            .ThenBy(c => c.FullName)
            .ToList();

        var result = new ConversationsListResponseDto
        {
            TotalUnread = totalUnread,
            Conversations = sorted
        };

        return Ok(ApiResponse<ConversationsListResponseDto>.Ok(result));
    }

    /// <summary>
    /// Retrieve messages for a specific employee thread and mark unread messages as read (Accountant & CEO only).
    /// </summary>
    [HttpGet("conversations/{employeeId}")]
    [Authorize(Roles = "Accountant,CEO")]
    [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversationMessages(Guid employeeId)
    {
        var employee = await _context.Users.FindAsync(employeeId);
        if (employee == null)
        {
            return NotFound(ApiResponse.Fail("Employee not found."));
        }

        // Mark unread messages from this employee as read by accountant
        var unread = await _context.ChatMessages
            .Where(m => m.SenderId == employeeId && !m.IsFromAccountant && !m.IsReadByAccountant)
            .ToListAsync();

        if (unread.Any())
        {
            foreach (var u in unread) u.IsReadByAccountant = true;
            await _context.SaveChangesAsync();
        }

        var rawMessages = await _context.ChatMessages
            .Include(m => m.Attachments)
            .Where(m => m.SenderId == employeeId || m.RecipientId == employeeId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        var dtos = rawMessages.Select(m => MapToDto(m, m.Attachments.FirstOrDefault())).ToList();

        var result = new ConversationDetailResponseDto
        {
            Employee = new UserDto
            {
                Id = employee.Id,
                FullName = employee.FullName,
                Username = employee.Username,
                Role = employee.Role,
                Email = employee.Email,
                PhoneNumber = employee.PhoneNumber,
                CreatedAt = employee.CreatedAt
            },
            Messages = dtos
        };

        return Ok(ApiResponse<ConversationDetailResponseDto>.Ok(result));
    }

    /// <summary>
    /// Retrieve message history for the currently authenticated employee.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<List<ChatMessageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChatHistory()
    {
        var (userId, _, _, _, isAccountant) = GetCurrentUser();

        // Mark incoming replies as read if employee
        if (!isAccountant)
        {
            var unreadReplies = await _context.ChatMessages
                .Where(m => m.RecipientId == userId && m.IsFromAccountant && !m.IsReadByUser)
                .ToListAsync();

            if (unreadReplies.Any())
            {
                foreach (var r in unreadReplies) r.IsReadByUser = true;
                await _context.SaveChangesAsync();
            }
        }

        var rawMessages = await _context.ChatMessages
            .Include(m => m.Attachments)
            .Where(m => m.SenderId == userId || m.RecipientId == userId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        var dtos = rawMessages.Select(m => MapToDto(m, m.Attachments.FirstOrDefault())).ToList();
        return Ok(ApiResponse<List<ChatMessageDto>>.Ok(dtos));
    }

    /// <summary>
    /// Fallback long-polling endpoint for clients without WebSocket connectivity.
    /// </summary>
    [HttpGet("poll")]
    [ProducesResponseType(typeof(ApiResponse<PollChatResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PollChatUpdates([FromQuery] Guid? activeEmployeeId, [FromQuery] string? after)
    {
        var (userId, _, _, _, isAccountant) = GetCurrentUser();

        DateTime since = DateTime.UtcNow.AddSeconds(-8);
        if (!string.IsNullOrEmpty(after) && DateTime.TryParse(after, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
        {
            since = parsed.ToUniversalTime();
        }

        if (isAccountant)
        {
            var newBroadcastsRaw = await _context.ChatMessages
                .Include(m => m.Attachments)
                .Where(m => !m.IsFromAccountant && m.SentAt > since)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            var newBroadcasts = newBroadcastsRaw.Select(m => MapToDto(m, m.Attachments.FirstOrDefault())).ToList();

            var activeMessages = new List<ChatMessageDto>();
            if (activeEmployeeId.HasValue)
            {
                var newActiveRaw = await _context.ChatMessages
                    .Include(m => m.Attachments)
                    .Where(m => (m.SenderId == activeEmployeeId.Value || m.RecipientId == activeEmployeeId.Value) && m.SentAt > since)
                    .OrderBy(m => m.SentAt)
                    .ToListAsync();

                var unreadInActive = newActiveRaw.Where(m => !m.IsFromAccountant && !m.IsReadByAccountant).ToList();
                if (unreadInActive.Any())
                {
                    foreach (var m in unreadInActive) m.IsReadByAccountant = true;
                    await _context.SaveChangesAsync();
                }

                activeMessages = newActiveRaw.Select(m => MapToDto(m, m.Attachments.FirstOrDefault())).ToList();
            }

            var totalUnread = await _context.ChatMessages
                .CountAsync(m => !m.IsFromAccountant && !m.IsReadByAccountant);

            var result = new PollChatResponseDto
            {
                IsAccountant = true,
                ServerTime = DateTime.UtcNow.ToString("o"),
                TotalUnread = totalUnread,
                NewBroadcasts = newBroadcasts,
                ActiveMessages = activeMessages
            };

            return Ok(ApiResponse<PollChatResponseDto>.Ok(result));
        }
        else
        {
            var newMessagesRaw = await _context.ChatMessages
                .Include(m => m.Attachments)
                .Where(m => (m.RecipientId == userId || m.SenderId == userId) && m.SentAt > since)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            var unreadFromAcc = newMessagesRaw.Where(m => m.RecipientId == userId && m.IsFromAccountant && !m.IsReadByUser).ToList();
            if (unreadFromAcc.Any())
            {
                foreach (var m in unreadFromAcc) m.IsReadByUser = true;
                await _context.SaveChangesAsync();
            }

            var result = new PollChatResponseDto
            {
                IsAccountant = false,
                ServerTime = DateTime.UtcNow.ToString("o"),
                NewMessages = newMessagesRaw.Select(m => MapToDto(m, m.Attachments.FirstOrDefault())).ToList()
            };

            return Ok(ApiResponse<PollChatResponseDto>.Ok(result));
        }
    }

    /// <summary>
    /// Stream / view an authenticated chat attachment file.
    /// </summary>
    [HttpGet("attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttachment(Guid attachmentId)
    {
        var (userId, _, _, role, isAccountant) = GetCurrentUser();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse.Fail("Invalid user token."));
        }

        var attachment = await _context.ChatAttachments
            .Include(a => a.Message)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (attachment == null || attachment.Message == null)
        {
            return NotFound(ApiResponse.Fail("Attachment was not found."));
        }

        // Strict Authorization:
        // Accountants and CEOs can view all attachments.
        // Employees can only view attachments for messages they sent or received.
        bool isAuthorized = isAccountant || 
                            attachment.Message.SenderId == userId || 
                            attachment.Message.RecipientId == userId;

        if (!isAuthorized)
        {
            _logger.LogWarning("User {UserId} unauthorized to access attachment {AttachmentId}", userId, attachmentId);
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("You are not authorized to view this attachment."));
        }

        var fullDiskPath = ResolveAttachmentDiskPath(attachment.FilePath);
        if (string.IsNullOrEmpty(fullDiskPath) || !System.IO.File.Exists(fullDiskPath))
        {
            _logger.LogWarning("Attachment file missing on disk for {AttachmentId}: {FilePath}", attachmentId, attachment.FilePath);
            return NotFound(ApiResponse.Fail("Attachment file is missing from storage."));
        }

        var contentType = GetMimeType(attachment.FileName);
        return PhysicalFile(fullDiskPath, contentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// Download an authenticated chat attachment file with its original filename.
    /// </summary>
    [HttpGet("attachments/{attachmentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId)
    {
        var (userId, _, _, role, isAccountant) = GetCurrentUser();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse.Fail("Invalid user token."));
        }

        var attachment = await _context.ChatAttachments
            .Include(a => a.Message)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (attachment == null || attachment.Message == null)
        {
            return NotFound(ApiResponse.Fail("Attachment was not found."));
        }

        bool isAuthorized = isAccountant || 
                            attachment.Message.SenderId == userId || 
                            attachment.Message.RecipientId == userId;

        if (!isAuthorized)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("You are not authorized to download this attachment."));
        }

        var fullDiskPath = ResolveAttachmentDiskPath(attachment.FilePath);
        if (string.IsNullOrEmpty(fullDiskPath) || !System.IO.File.Exists(fullDiskPath))
        {
            return NotFound(ApiResponse.Fail("Attachment file is missing from storage."));
        }

        var contentType = GetMimeType(attachment.FileName);
        return PhysicalFile(fullDiskPath, contentType, fileDownloadName: attachment.FileName, enableRangeProcessing: true);
    }

    private string? ResolveAttachmentDiskPath(string storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return null;

        var fileName = Path.GetFileName(storedPath);

        // 1. Primary secure storage directory (outside wwwroot)
        var rootDir = _env.ContentRootPath ?? Directory.GetCurrentDirectory();
        var primaryDir = Path.Combine(rootDir, "uploads", "chat");
        var candidatePrimary = Path.Combine(primaryDir, fileName);
        if (System.IO.File.Exists(candidatePrimary)) return candidatePrimary;

        // 2. Legacy wwwroot/uploads/chat fallback for existing files
        var legacyDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "chat");
        var candidateLegacy = Path.Combine(legacyDir, fileName);
        if (System.IO.File.Exists(candidateLegacy)) return candidateLegacy;

        // 3. Direct relative path fallback
        var candidateRel = Path.Combine(Directory.GetCurrentDirectory(), storedPath.TrimStart('/', '\\'));
        if (System.IO.File.Exists(candidateRel)) return candidateRel;

        return candidatePrimary;
    }

    private static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv",
            ".txt" => "text/plain",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    private static ChatMessageDto MapToDto(ChatMessage m, ChatAttachment? att)
    {
        return new ChatMessageDto
        {
            Id = m.Id,
            SenderId = m.SenderId,
            SenderName = m.SenderName,
            SenderUsername = m.SenderUsername,
            SenderRole = m.SenderRole,
            RecipientId = m.RecipientId,
            RecipientName = m.RecipientName,
            RecipientUsername = m.RecipientUsername,
            RecipientRole = m.RecipientRole,
            MessageText = m.MessageText,
            HasAttachment = m.HasAttachment,
            IsFromAccountant = m.IsFromAccountant,
            IsReadByAccountant = m.IsReadByAccountant,
            IsReadByUser = m.IsReadByUser,
            SentAt = m.SentAt,
            ClientMessageId = m.ClientMessageId,
            Attachment = att == null ? null : new ChatAttachmentDto
            {
                Id = att.Id,
                FileName = att.FileName,
                FilePath = att.FilePath,
                Url = $"/api/chat/attachments/{att.Id}",
                FileType = att.FileType,
                FileSizeBytes = att.FileSizeBytes,
                UploadedAt = att.UploadedAt
            }
        };
    }
}
