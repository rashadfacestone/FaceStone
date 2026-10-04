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
    /// Send a chat message with optional file attachment or structured location data.
    /// Supports TEXT, LOCATION, and LIVE_LOCATION message types.
    /// </summary>
    [HttpPost("send")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendMessage()
    {
        var (userId, fullName, username, role, isAccountant) = GetCurrentUser();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse.Fail("Invalid user token."));
        }

        SendMessageDto? request = null;
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            var att = form.Files.GetFile("Attachment") ?? form.Files.FirstOrDefault();

            double? lat = null;
            var latStr = form["Latitude"].FirstOrDefault() ?? form["latitude"].FirstOrDefault() ?? form["lat"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(latStr) && double.TryParse(latStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedLat))
            {
                lat = parsedLat;
            }

            double? lng = null;
            var lngStr = form["Longitude"].FirstOrDefault() ?? form["longitude"].FirstOrDefault() ?? form["lng"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(lngStr) && double.TryParse(lngStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedLng))
            {
                lng = parsedLng;
            }

            Guid? targetEmp = null;
            var targetEmpStr = form["TargetEmployeeId"].FirstOrDefault() ?? form["targetEmployeeId"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(targetEmpStr) && Guid.TryParse(targetEmpStr, out var parsedTargetEmp))
            {
                targetEmp = parsedTargetEmp;
            }

            Guid? convId = null;
            var convIdStr = form["ConversationId"].FirstOrDefault() ?? form["conversationId"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(convIdStr) && Guid.TryParse(convIdStr, out var parsedConvId))
            {
                convId = parsedConvId;
            }

            int? durMin = null;
            var durStr = form["DurationMinutes"].FirstOrDefault() ?? form["durationMinutes"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(durStr) && int.TryParse(durStr, out var parsedDur))
            {
                durMin = parsedDur;
            }

            request = new SendMessageDto
            {
                MessageType = form["MessageType"].FirstOrDefault() ?? form["messageType"].FirstOrDefault() ?? MessageTypes.Text,
                Message = form["Message"].FirstOrDefault() ?? form["message"].FirstOrDefault(),
                TargetEmployeeId = targetEmp,
                ConversationId = convId,
                Latitude = lat,
                Longitude = lng,
                DurationMinutes = durMin ?? 15,
                Status = form["Status"].FirstOrDefault() ?? form["status"].FirstOrDefault() ?? "ACTIVE",
                ClientMessageId = form["ClientMessageId"].FirstOrDefault() ?? form["clientMessageId"].FirstOrDefault(),
                Attachment = att
            };
        }
        else
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            try
            {
                request = await System.Text.Json.JsonSerializer.DeserializeAsync<SendMessageDto>(Request.Body, options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize JSON body in /api/chat/send.");
                return BadRequest(ApiResponse.Fail("Invalid JSON request body."));
            }
        }

        if (request == null)
        {
            return BadRequest(ApiResponse.Fail("Request payload is empty."));
        }

        var msgType = (request.MessageType ?? MessageTypes.Text).Trim().ToUpperInvariant();
        if (msgType is "LIVELOCATION") msgType = MessageTypes.LiveLocation;

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

        // Determine target recipient user
        var effectiveTargetId = request.GetEffectiveTargetEmployeeId();
        if (!effectiveTargetId.HasValue || effectiveTargetId.Value == Guid.Empty)
        {
            return BadRequest(ApiResponse.Fail("Please select a recipient before sending a message."));
        }

        if (effectiveTargetId.Value == userId)
        {
            return BadRequest(ApiResponse.Fail("You cannot send a message to yourself."));
        }

        var targetUser = await _context.Users.FindAsync(effectiveTargetId.Value);
        if (targetUser == null)
        {
            return NotFound(ApiResponse.Fail("Recipient user was not found."));
        }

        Guid recipientId = targetUser.Id;
        string recipientName = targetUser.FullName;
        string? recipientUsername = targetUser.Username;
        string? recipientRole = targetUser.Role;
        Guid threadEmployeeId = targetUser.Id;

        // Branch by MessageType
        if (msgType == MessageTypes.LiveLocation)
        {
            var lat = request.GetEffectiveLatitude();
            var lng = request.GetEffectiveLongitude();

            if (!lat.HasValue || !lng.HasValue || !IsValidCoordinate(lat.Value, lng.Value))
            {
                _logger.LogWarning("[LiveLocation] SEND_FAILED: Invalid latitude or longitude. Lat: {Lat}, Lng: {Lng}", lat, lng);
                return BadRequest(ApiResponse.Fail("Valid latitude (-90 to +90) and longitude (-180 to +180) are required for LIVE_LOCATION."));
            }

            int duration = request.DurationMinutes switch
            {
                15 => 15,
                30 => 30,
                60 => 60,
                _ => 15
            };

            _logger.LogInformation("[LiveLocation] START /api/chat/send User: {UserId}, Lat: {Lat}, Lng: {Lng}, Duration: {Duration}",
                userId, lat.Value, lng.Value, duration);

            // Deactivate any prior active live sessions from this user in this conversation
            var priorActive = await _context.LiveLocationSessions
                .Where(s => s.UserId == userId && s.EmployeeId == threadEmployeeId && s.IsActive)
                .ToListAsync();

            if (priorActive.Any())
            {
                foreach (var p in priorActive)
                {
                    p.IsActive = false;
                    p.Status = "ENDED";
                    p.EndedAt = DateTime.UtcNow;
                    p.UpdatedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
            }

            var session = new LiveLocationSession
            {
                UserId = userId,
                EmployeeId = threadEmployeeId,
                LastLatitude = lat.Value,
                LastLongitude = lng.Value,
                DurationMinutes = duration,
                StartedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(duration),
                IsActive = true,
                Status = "ACTIVE",
                UpdatedAt = DateTime.UtcNow
            };
            await _context.LiveLocationSessions.AddAsync(session);
            await _context.SaveChangesAsync();

            var chatMsg = new ChatMessage
            {
                SenderId = userId,
                SenderName = fullName,
                SenderUsername = username,
                SenderRole = role,
                RecipientId = recipientId,
                RecipientName = recipientName,
                RecipientUsername = recipientUsername,
                RecipientRole = recipientRole,
                MessageText = string.Empty, // CRITICAL: NEVER store "Live Location" text
                MessageType = MessageTypes.LiveLocation,
                Status = "ACTIVE",
                Latitude = lat.Value,
                Longitude = lng.Value,
                LiveSessionId = session.Id,
                HasAttachment = false,
                IsFromAccountant = isAccountant,
                IsReadByAccountant = isAccountant,
                IsReadByUser = false,
                SentAt = DateTime.UtcNow,
                ClientMessageId = request.ClientMessageId?.Trim()
            };
            await _context.ChatMessages.AddAsync(chatMsg);
            await _context.SaveChangesAsync();

            session.MessageId = chatMsg.Id;
            await _context.SaveChangesAsync();

            var messageDto = MapToDto(chatMsg, null);

            // Real-Time Broadcast via SignalR (dedicated OnLiveLocationStarted event)
            try
            {
                _logger.LogInformation("[LiveLocation] BROADCAST OnLiveLocationStarted MessageId: {MsgId}, SessionId: {SessionId}",
                    chatMsg.Id, session.Id);

                await _hubContext.Clients.Group($"Employee_{recipientId}").SendAsync("OnLiveLocationStarted", messageDto);
                await _hubContext.Clients.Group($"Employee_{userId}").SendAsync("OnLiveLocationStarted", messageDto);
                if (recipientRole == "Accountant" || recipientRole == "CEO")
                {
                    await _hubContext.Clients.Group("Accountants").SendAsync("OnLiveLocationStarted", messageDto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[LiveLocation] SignalR broadcast notification caught exception on live location start.");
            }

            return StatusCode(StatusCodes.Status201Created, ApiResponse<ChatMessageDto>.Ok(messageDto, "Live location started successfully."));
        }
        else if (msgType == MessageTypes.Location)
        {
            var lat = request.GetEffectiveLatitude();
            var lng = request.GetEffectiveLongitude();

            if (!lat.HasValue || !lng.HasValue || !IsValidCoordinate(lat.Value, lng.Value))
            {
                return BadRequest(ApiResponse.Fail("Valid latitude (-90 to +90) and longitude (-180 to +180) are required for LOCATION."));
            }

            var chatMsg = new ChatMessage
            {
                SenderId = userId,
                SenderName = fullName,
                SenderUsername = username,
                SenderRole = role,
                RecipientId = recipientId,
                RecipientName = recipientName,
                RecipientUsername = recipientUsername,
                RecipientRole = recipientRole,
                MessageText = string.Empty, // Static location messages have empty text
                MessageType = MessageTypes.Location,
                Latitude = lat.Value,
                Longitude = lng.Value,
                HasAttachment = false,
                IsFromAccountant = isAccountant,
                IsReadByAccountant = isAccountant,
                IsReadByUser = false,
                SentAt = DateTime.UtcNow,
                ClientMessageId = request.ClientMessageId?.Trim()
            };

            await _context.ChatMessages.AddAsync(chatMsg);
            await _context.SaveChangesAsync();

            var messageDto = MapToDto(chatMsg, null);

            try
            {
                await _hubContext.Clients.Group($"Employee_{recipientId}").SendAsync("OnNewMessage", messageDto);
                await _hubContext.Clients.Group($"Employee_{userId}").SendAsync("OnNewMessage", messageDto);
                if (recipientRole == "Accountant" || recipientRole == "CEO")
                {
                    await _hubContext.Clients.Group("Accountants").SendAsync("OnNewMessage", messageDto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalR broadcast notification caught exception on location send.");
            }

            return StatusCode(StatusCodes.Status201Created, ApiResponse<ChatMessageDto>.Ok(messageDto, "Location shared successfully."));
        }
        else if (msgType == MessageTypes.Text)
        {
            var effectiveText = request.GetEffectiveMessage();
            if (string.IsNullOrWhiteSpace(effectiveText) && (request.Attachment == null || request.Attachment.Length == 0))
            {
                return BadRequest(ApiResponse.Fail("Please provide a message text or attach a file."));
            }

            var chatMsg = new ChatMessage
            {
                SenderId = userId,
                SenderName = fullName,
                SenderUsername = username,
                SenderRole = role,
                RecipientId = recipientId,
                RecipientName = recipientName,
                RecipientUsername = recipientUsername,
                RecipientRole = recipientRole,
                MessageText = effectiveText?.Trim() ?? string.Empty,
                MessageType = MessageTypes.Text,
                HasAttachment = (request.Attachment != null && request.Attachment.Length > 0),
                IsFromAccountant = isAccountant,
                IsReadByAccountant = isAccountant,
                IsReadByUser = false,
                SentAt = DateTime.UtcNow,
                ClientMessageId = request.ClientMessageId?.Trim()
            };

            await _context.ChatMessages.AddAsync(chatMsg);
            await _context.SaveChangesAsync();

            // Handle Attachment if present
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

            try
            {
                await _hubContext.Clients.Group($"Employee_{recipientId}").SendAsync("OnNewMessage", messageDto);
                await _hubContext.Clients.Group($"Employee_{userId}").SendAsync("OnNewMessage", messageDto);
                if (recipientRole == "Accountant" || recipientRole == "CEO")
                {
                    await _hubContext.Clients.Group("Accountants").SendAsync("OnNewMessage", messageDto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalR broadcast notification caught exception (fallback to polling).");
            }

            return StatusCode(StatusCodes.Status201Created, ApiResponse<ChatMessageDto>.Ok(messageDto, "Message sent successfully."));
        }
        else
        {
            return BadRequest(ApiResponse.Fail($"Unsupported message type '{request.MessageType}'. Supported types are TEXT, LIVE_LOCATION, and LOCATION."));
        }
    }

    /// <summary>
    /// Send a static GPS location message into the chat.
    /// </summary>
    [HttpPost("location/send")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendLocation([FromBody] SendLocationDto request)
    {
        if (!IsValidCoordinate(request.Latitude, request.Longitude))
        {
            return BadRequest(ApiResponse.Fail("Invalid latitude or longitude coordinates."));
        }

        var (userId, fullName, username, role, isAccountant) = GetCurrentUser();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse.Fail("Invalid user token."));
        }

        // Idempotency check
        if (!string.IsNullOrWhiteSpace(request.ClientMessageId))
        {
            var cleanClientMsgId = request.ClientMessageId.Trim();
            var existing = await _context.ChatMessages
                .Include(m => m.Attachments)
                .FirstOrDefaultAsync(m => m.SenderId == userId && m.ClientMessageId == cleanClientMsgId);

            if (existing != null)
            {
                var existingDto = MapToDto(existing, existing.Attachments.FirstOrDefault());
                return Ok(ApiResponse<ChatMessageDto>.Ok(existingDto, "Location message already processed."));
            }
        }

        var effectiveTargetId = request.GetEffectiveTargetId();
        if (!effectiveTargetId.HasValue || effectiveTargetId.Value == Guid.Empty)
        {
            return BadRequest(ApiResponse.Fail("Please select a recipient before sending location."));
        }
        if (effectiveTargetId.Value == userId)
        {
            return BadRequest(ApiResponse.Fail("You cannot send a message to yourself."));
        }
        var targetUser = await _context.Users.FindAsync(effectiveTargetId.Value);
        if (targetUser == null)
        {
            return NotFound(ApiResponse.Fail("Recipient user was not found."));
        }
        Guid recipientId = targetUser.Id;
        string recipientName = targetUser.FullName;
        string? recipientUsername = targetUser.Username;
        string? recipientRole = targetUser.Role;

        var chatMsg = new ChatMessage
        {
            SenderId = userId,
            SenderName = fullName,
            SenderUsername = username,
            SenderRole = role,
            RecipientId = recipientId,
            RecipientName = recipientName,
            RecipientUsername = recipientUsername,
            RecipientRole = recipientRole,
            MessageText = string.Empty, // Location messages have empty text body
            MessageType = MessageTypes.Location,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            HasAttachment = false,
            IsFromAccountant = isAccountant,
            IsReadByAccountant = isAccountant,
            IsReadByUser = false,
            SentAt = DateTime.UtcNow,
            ClientMessageId = request.ClientMessageId?.Trim()
        };

        await _context.ChatMessages.AddAsync(chatMsg);
        await _context.SaveChangesAsync();

        var messageDto = MapToDto(chatMsg, null);

        // Real-Time Broadcast via SignalR
        try
        {
            await _hubContext.Clients.Group($"Employee_{recipientId}").SendAsync("OnNewMessage", messageDto);
            await _hubContext.Clients.Group($"Employee_{userId}").SendAsync("OnNewMessage", messageDto);
            if (recipientRole == "Accountant" || recipientRole == "CEO")
            {
                await _hubContext.Clients.Group("Accountants").SendAsync("OnNewMessage", messageDto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR broadcast notification caught exception on location send.");
        }

        return StatusCode(StatusCodes.Status201Created, ApiResponse<ChatMessageDto>.Ok(messageDto, "Location shared successfully."));
    }

    /// <summary>
    /// Start a continuous live-location sharing session.
    /// </summary>
    [HttpPost("live-location/start")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> StartLiveLocation([FromBody] StartLiveLocationDto request)
    {
        if (!IsValidCoordinate(request.Latitude, request.Longitude))
        {
            return BadRequest(ApiResponse.Fail("Invalid latitude or longitude coordinates."));
        }

        var (userId, fullName, username, role, isAccountant) = GetCurrentUser();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse.Fail("Invalid user token."));
        }

        int duration = request.DurationMinutes switch
        {
            15 => 15,
            30 => 30,
            60 => 60,
            _ => 15
        };

        _logger.LogInformation("[LiveLocation] START User: {UserId}, Lat: {Lat}, Lng: {Lng}, Duration: {Duration}", 
            userId, request.Latitude, request.Longitude, duration);

        var effectiveTargetId = request.GetEffectiveTargetId();
        if (!effectiveTargetId.HasValue || effectiveTargetId.Value == Guid.Empty)
        {
            return BadRequest(ApiResponse.Fail("Please select a recipient before starting live location."));
        }
        if (effectiveTargetId.Value == userId)
        {
            return BadRequest(ApiResponse.Fail("You cannot send a message to yourself."));
        }
        var targetUser = await _context.Users.FindAsync(effectiveTargetId.Value);
        if (targetUser == null)
        {
            return NotFound(ApiResponse.Fail("Recipient user was not found."));
        }
        Guid recipientId = targetUser.Id;
        string recipientName = targetUser.FullName;
        string? recipientUsername = targetUser.Username;
        string? recipientRole = targetUser.Role;
        Guid threadEmployeeId = targetUser.Id;

        // Deactivate any prior active live sessions from this user in this conversation
        var priorActive = await _context.LiveLocationSessions
            .Where(s => s.UserId == userId && s.EmployeeId == threadEmployeeId && s.IsActive)
            .ToListAsync();

        if (priorActive.Any())
        {
            foreach (var p in priorActive)
            {
                p.IsActive = false;
                p.Status = "ENDED";
                p.EndedAt = DateTime.UtcNow;
            }
        }

        var liveSession = new LiveLocationSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EmployeeId = threadEmployeeId,
            StartedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(duration),
            IsActive = true,
            Status = "ACTIVE",
            LastLatitude = request.Latitude,
            LastLongitude = request.Longitude,
            DurationMinutes = duration,
            UpdatedAt = DateTime.UtcNow
        };

        var chatMsg = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SenderId = userId,
            SenderName = fullName,
            SenderUsername = username,
            SenderRole = role,
            RecipientId = recipientId,
            RecipientName = recipientName,
            RecipientUsername = recipientUsername,
            RecipientRole = recipientRole,
            MessageText = string.Empty, // Strictly empty text body: NEVER store 'Live Location' text string
            MessageType = MessageTypes.LiveLocation,
            Status = "ACTIVE",
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            LiveSessionId = liveSession.Id,
            HasAttachment = false,
            IsFromAccountant = isAccountant,
            IsReadByAccountant = isAccountant,
            IsReadByUser = false,
            SentAt = DateTime.UtcNow,
            ClientMessageId = request.ClientMessageId?.Trim()
        };

        liveSession.MessageId = chatMsg.Id;

        await _context.LiveLocationSessions.AddAsync(liveSession);
        await _context.ChatMessages.AddAsync(chatMsg);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[LiveLocation] PERSISTED MessageId: {MsgId}, SessionId: {SessionId}", chatMsg.Id, liveSession.Id);

        var messageDto = MapToDto(chatMsg, null, liveSession);

        // Real-Time Broadcast dedicated LIVE_LOCATION event via SignalR
        try
        {
            _logger.LogInformation("[LiveLocation] BROADCAST OnLiveLocationStarted MessageId: {MsgId}, SessionId: {SessionId}", 
                chatMsg.Id, liveSession.Id);

            await _hubContext.Clients.Group($"Employee_{recipientId}").SendAsync("OnLiveLocationStarted", messageDto);
            await _hubContext.Clients.Group($"Employee_{userId}").SendAsync("OnLiveLocationStarted", messageDto);
            if (recipientRole == "Accountant" || recipientRole == "CEO")
            {
                await _hubContext.Clients.Group("Accountants").SendAsync("OnLiveLocationStarted", messageDto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LiveLocation] SignalR broadcast notification caught exception on live location start.");
        }

        return StatusCode(StatusCodes.Status201Created, ApiResponse<ChatMessageDto>.Ok(messageDto, "Live location sharing started."));
    }

    /// <summary>
    /// Stream continuous GPS coordinate updates for an active live location session.
    /// </summary>
    [HttpPost("live-location/update")]
    [ProducesResponseType(typeof(ApiResponse<LiveLocationUpdateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLiveLocation([FromBody] UpdateLiveLocationDto request)
    {
        if (!IsValidCoordinate(request.Latitude, request.Longitude))
        {
            return BadRequest(ApiResponse.Fail("Invalid latitude or longitude coordinates."));
        }

        var (userId, _, _, _, _) = GetCurrentUser();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse.Fail("Invalid user token."));
        }

        var session = await _context.LiveLocationSessions.FindAsync(request.SessionId);
        if (session == null)
        {
            return NotFound(ApiResponse.Fail("Live location session was not found."));
        }

        _logger.LogInformation("[LiveLocation] SERVER_RECEIVED UPDATE SessionId: {SessionId}, Coords: [{Lat}, {Lng}]", 
            request.SessionId, request.Latitude, request.Longitude);

        // Ownership verification: Only the sender can update their live location
        if (session.UserId != userId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("You are not authorized to update this live location session."));
        }

        // Expiration check
        if (!session.IsActive || session.ExpiresAt <= DateTime.UtcNow)
        {
            session.IsActive = false;
            session.Status = "EXPIRED";
            session.EndedAt ??= DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return BadRequest(ApiResponse.Fail("Live location sharing has expired."));
        }

        session.LastLatitude = request.Latitude;
        session.LastLongitude = request.Longitude;
        session.UpdatedAt = DateTime.UtcNow;
        session.Status = "ACTIVE";

        // Also update the associated ChatMessage latest coordinates
        var chatMsg = await _context.ChatMessages.FindAsync(session.MessageId);
        if (chatMsg != null)
        {
            chatMsg.Latitude = request.Latitude;
            chatMsg.Longitude = request.Longitude;
            chatMsg.Status = "ACTIVE";
        }

        await _context.SaveChangesAsync();

        var updateDto = new LiveLocationUpdateDto
        {
            MessageType = MessageTypes.LocationUpdate,
            Status = "ACTIVE",
            SessionId = session.Id,
            MessageId = session.MessageId,
            EmployeeId = session.EmployeeId,
            SenderId = session.UserId,
            Latitude = session.LastLatitude,
            Longitude = session.LastLongitude,
            UpdatedAt = session.UpdatedAt,
            IsActive = true,
            ExpiresAt = session.ExpiresAt
        };

        // Broadcast to relevant conversation parties
        try
        {
            _logger.LogInformation("[LiveLocation] BROADCAST OnLiveLocationUpdate SessionId: {SessionId}", session.Id);
            await _hubContext.Clients.Group("Accountants").SendAsync("OnLiveLocationUpdate", updateDto);
            await _hubContext.Clients.Group($"Employee_{session.EmployeeId}").SendAsync("OnLiveLocationUpdate", updateDto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LiveLocation] SignalR live location update broadcast caught exception.");
        }

        return Ok(ApiResponse<LiveLocationUpdateDto>.Ok(updateDto, "Live coordinates updated."));
    }

    /// <summary>
    /// Stop an active live location session manually.
    /// </summary>
    [HttpPost("live-location/stop")]
    [ProducesResponseType(typeof(ApiResponse<LiveLocationEndedDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StopLiveLocation([FromBody] StopLiveLocationDto request)
    {
        _logger.LogInformation("[LiveLocation] SERVER_RECEIVED STOP SessionId: {SessionId}", request.SessionId);

        var (userId, _, _, _, isAccountant) = GetCurrentUser();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse.Fail("Invalid user token."));
        }

        var session = await _context.LiveLocationSessions.FindAsync(request.SessionId);
        if (session == null)
        {
            return NotFound(ApiResponse.Fail("Live location session was not found."));
        }

        // Ownership verification: Senders can stop their session; accountants can also end it if necessary
        if (session.UserId != userId && !isAccountant)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("You are not authorized to stop this live location session."));
        }

        session.IsActive = false;
        session.Status = "ENDED";
        session.EndedAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;

        var chatMsg = await _context.ChatMessages.FindAsync(session.MessageId);
        if (chatMsg != null)
        {
            chatMsg.Status = "ENDED";
        }

        await _context.SaveChangesAsync();

        var endedDto = new LiveLocationEndedDto
        {
            MessageType = MessageTypes.LiveLocationEnded,
            Status = "ENDED",
            SessionId = session.Id,
            MessageId = session.MessageId,
            EmployeeId = session.EmployeeId,
            EndedAt = session.EndedAt.Value,
            LastLatitude = session.LastLatitude,
            LastLongitude = session.LastLongitude
        };

        try
        {
            _logger.LogInformation("[LiveLocation] BROADCAST OnLiveLocationEnded SessionId: {SessionId}", session.Id);
            await _hubContext.Clients.Group("Accountants").SendAsync("OnLiveLocationEnded", endedDto);
            await _hubContext.Clients.Group($"Employee_{session.EmployeeId}").SendAsync("OnLiveLocationEnded", endedDto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LiveLocation] SignalR live location ended broadcast caught exception.");
        }

        return Ok(ApiResponse<LiveLocationEndedDto>.Ok(endedDto, "Live location sharing ended."));
    }

    /// <summary>
    /// Search FaceStone users for starting new 1-to-1 conversations.
    /// Accessible to all authenticated users.
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchUsers([FromQuery] string? search)
    {
        var (userId, _, _, _, _) = GetCurrentUser();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse.Fail("Invalid user token."));
        }

        var query = _context.Users.AsNoTracking().Where(u => u.Id != userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(s) ||
                u.Username.ToLower().Contains(s) ||
                u.Email.ToLower().Contains(s) ||
                u.Role.ToLower().Contains(s));
        }

        var users = await query
            .OrderBy(u => u.FullName)
            .Take(50)
            .Select(u => new UserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Username = u.Username,
                Role = u.Role,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<List<UserDto>>.Ok(users));
    }

    /// <summary>
    /// Retrieve conversation threads for the authenticated user.
    /// Supports user-to-user private conversations with unread counters and latest message snippets.
    /// </summary>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(ApiResponse<ConversationsListResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConversations()
    {
        var (userId, _, _, role, isAccountant) = GetCurrentUser();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse.Fail("Invalid user token."));
        }

        // 1. Identify distinct partner user IDs who have exchanged messages with this user
        var sentToPartners = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.SenderId == userId && m.RecipientId != null && m.RecipientId != userId)
            .Select(m => m.RecipientId!.Value)
            .Distinct()
            .ToListAsync();

        var receivedFromPartners = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.RecipientId == userId && m.SenderId != null && m.SenderId != userId)
            .Select(m => m.SenderId!.Value)
            .Distinct()
            .ToListAsync();

        var partnerIdsSet = sentToPartners.Concat(receivedFromPartners).ToHashSet();

        // Include legacy messages for accountants / CEOs if any
        if (isAccountant)
        {
            var legacySenders = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.RecipientId == null && m.SenderId != null && m.SenderId != userId)
                .Select(m => m.SenderId!.Value)
                .Distinct()
                .ToListAsync();

            foreach (var ls in legacySenders)
            {
                partnerIdsSet.Add(ls);
            }
        }

        // Also if the list is empty and user is an accountant, show default employee accounts to start chat with
        if (partnerIdsSet.Count == 0 && isAccountant)
        {
            var defaultPartners = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id != userId)
                .Take(10)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var dp in defaultPartners) partnerIdsSet.Add(dp);
        }

        var partnerUsers = await _context.Users
            .AsNoTracking()
            .Where(u => partnerIdsSet.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        if (partnerUsers.Count == 0)
        {
            return Ok(ApiResponse<ConversationsListResponseDto>.Ok(new ConversationsListResponseDto
            {
                TotalUnread = 0,
                Conversations = new List<ConversationSummaryDto>()
            }));
        }

        // 2. Fetch unread counts grouped by partner (messages sent TO userId where not read)
        var unreadCounts = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.RecipientId == userId && !m.IsReadByUser && m.SenderId.HasValue && partnerIdsSet.Contains(m.SenderId.Value))
            .GroupBy(m => m.SenderId!.Value)
            .Select(g => new { PartnerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PartnerId, x => x.Count);

        if (isAccountant)
        {
            var legacyUnread = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.RecipientId == null && !m.IsFromAccountant && !m.IsReadByAccountant && m.SenderId.HasValue && partnerIdsSet.Contains(m.SenderId.Value))
                .GroupBy(m => m.SenderId!.Value)
                .Select(g => new { PartnerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PartnerId, x => x.Count);

            foreach (var kvp in legacyUnread)
            {
                if (unreadCounts.TryGetValue(kvp.Key, out var existing))
                {
                    unreadCounts[kvp.Key] = existing + kvp.Value;
                }
                else
                {
                    unreadCounts[kvp.Key] = kvp.Value;
                }
            }
        }

        // 3. Fetch latest messages for each partner
        var allRelevantMessages = await _context.ChatMessages
            .AsNoTracking()
            .Include(m => m.Attachments)
            .Where(m => (m.SenderId == userId && m.RecipientId != null && partnerIdsSet.Contains(m.RecipientId.Value)) ||
                        (m.RecipientId == userId && m.SenderId != null && partnerIdsSet.Contains(m.SenderId.Value)) ||
                        (isAccountant && m.RecipientId == null && m.SenderId != null && partnerIdsSet.Contains(m.SenderId.Value)))
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();

        var latestMessagesByPartner = allRelevantMessages
            .GroupBy(m => (m.SenderId == userId) ? (m.RecipientId ?? Guid.Empty) : (m.SenderId ?? Guid.Empty))
            .Where(g => g.Key != Guid.Empty && partnerUsers.ContainsKey(g.Key))
            .ToDictionary(g => g.Key, g => g.First());

        var summaries = new List<ConversationSummaryDto>(partnerUsers.Count);
        int totalUnread = 0;

        foreach (var (partnerId, partner) in partnerUsers)
        {
            latestMessagesByPartner.TryGetValue(partnerId, out var lastMsg);
            unreadCounts.TryGetValue(partnerId, out var unreadCount);
            totalUnread += unreadCount;

            string? attType = lastMsg?.Attachments?.FirstOrDefault()?.FileType;
            string previewText = "محادثة جديدة";
            if (lastMsg != null)
            {
                var msgType = lastMsg.MessageType ?? "";
                if (msgType.Equals("LIVE_LOCATION", StringComparison.OrdinalIgnoreCase) || 
                    msgType.Equals("LIVELOCATION", StringComparison.OrdinalIgnoreCase) || 
                    lastMsg.LiveSessionId.HasValue)
                {
                    previewText = "📍 موقع مباشر";
                }
                else if (msgType.Equals("LOCATION", StringComparison.OrdinalIgnoreCase))
                {
                    previewText = "📍 موقع جغرافي";
                }
                else if (!string.IsNullOrEmpty(lastMsg.MessageText))
                {
                    previewText = lastMsg.MessageText;
                }
                else if (lastMsg.HasAttachment)
                {
                    previewText = "[مرفق]";
                }
            }

            summaries.Add(new ConversationSummaryDto
            {
                EmployeeId = partner.Id,
                FullName = partner.FullName,
                Username = partner.Username,
                Role = partner.Role,
                Email = partner.Email,
                PhoneNumber = partner.PhoneNumber,
                LastMessageText = previewText,
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

        return Ok(ApiResponse<ConversationsListResponseDto>.Ok(new ConversationsListResponseDto
        {
            TotalUnread = totalUnread,
            Conversations = sorted
        }));
    }

    /// <summary>
    /// Retrieve messages for a specific partner conversation and mark unread messages as read.
    /// Strictly authorized: returns only the messages exchanged between authenticated user and the partner.
    /// </summary>
    [HttpGet("conversations/{employeeId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversationMessages(Guid employeeId)
    {
        var (userId, _, _, _, isAccountant) = GetCurrentUser();
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse.Fail("Invalid user token."));
        }

        var partner = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == employeeId);
        if (partner == null)
        {
            return NotFound(ApiResponse.Fail("User not found."));
        }

        // Fast atomic update: Mark messages sent by partner to current user as read
        await _context.ChatMessages
            .Where(m => m.SenderId == employeeId && m.RecipientId == userId && !m.IsReadByUser)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsReadByUser, true));

        if (isAccountant)
        {
            await _context.ChatMessages
                .Where(m => m.SenderId == employeeId && !m.IsFromAccountant && !m.IsReadByAccountant)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsReadByAccountant, true));
        }

        // Strictly fetch messages between current user and partner (plus legacy accounting messages if accountant)
        var rawMessages = await _context.ChatMessages
            .AsNoTracking()
            .Include(m => m.Attachments)
            .Where(m => (m.SenderId == userId && m.RecipientId == employeeId) ||
                        (m.SenderId == employeeId && m.RecipientId == userId) ||
                        (isAccountant && m.SenderId == employeeId && m.RecipientId == null))
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        var liveSessionIds = rawMessages
            .Where(m => m.LiveSessionId.HasValue)
            .Select(m => m.LiveSessionId!.Value)
            .Distinct()
            .ToList();

        var liveSessions = liveSessionIds.Any()
            ? await _context.LiveLocationSessions
                .AsNoTracking()
                .Where(s => liveSessionIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id)
            : new Dictionary<Guid, LiveLocationSession>();

        var expiredSessions = liveSessions.Values
            .Where(s => s.IsActive && s.ExpiresAt <= DateTime.UtcNow)
            .ToList();

        if (expiredSessions.Any())
        {
            var expIds = expiredSessions.Select(s => s.Id).ToList();
            await _context.LiveLocationSessions
                .Where(s => expIds.Contains(s.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false)
                                          .SetProperty(x => x.Status, "ENDED")
                                          .SetProperty(x => x.EndedAt, x => x.ExpiresAt));
            foreach (var s in expiredSessions)
            {
                s.IsActive = false;
                s.Status = "ENDED";
                s.EndedAt = s.ExpiresAt;
            }
        }

        var dtos = rawMessages.Select(m => 
            MapToDto(m, m.Attachments.FirstOrDefault(), m.LiveSessionId.HasValue && liveSessions.TryGetValue(m.LiveSessionId.Value, out var ls) ? ls : null)
        ).ToList();

        var result = new ConversationDetailResponseDto
        {
            Employee = new UserDto
            {
                Id = partner.Id,
                FullName = partner.FullName,
                Username = partner.Username,
                Role = partner.Role,
                Email = partner.Email,
                PhoneNumber = partner.PhoneNumber,
                CreatedAt = partner.CreatedAt
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

        // Mark incoming replies as read if employee via fast atomic update
        if (!isAccountant)
        {
            await _context.ChatMessages
                .Where(m => m.RecipientId == userId && m.IsFromAccountant && !m.IsReadByUser)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsReadByUser, true));
        }

        var rawMessages = await _context.ChatMessages
            .AsNoTracking()
            .Include(m => m.Attachments)
            .Where(m => m.SenderId == userId || m.RecipientId == userId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        var liveSessionIds = rawMessages
            .Where(m => m.LiveSessionId.HasValue)
            .Select(m => m.LiveSessionId!.Value)
            .Distinct()
            .ToList();

        var liveSessions = liveSessionIds.Any()
            ? await _context.LiveLocationSessions
                .AsNoTracking()
                .Where(s => liveSessionIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id)
            : new Dictionary<Guid, LiveLocationSession>();

        var expiredSessions = liveSessions.Values
            .Where(s => s.IsActive && s.ExpiresAt <= DateTime.UtcNow)
            .ToList();

        if (expiredSessions.Any())
        {
            var expIds = expiredSessions.Select(s => s.Id).ToList();
            await _context.LiveLocationSessions
                .Where(s => expIds.Contains(s.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false)
                                          .SetProperty(x => x.Status, "ENDED")
                                          .SetProperty(x => x.EndedAt, x => x.ExpiresAt));
            foreach (var s in expiredSessions)
            {
                s.IsActive = false;
                s.Status = "ENDED";
                s.EndedAt = s.ExpiresAt;
            }
        }

        var dtos = rawMessages.Select(m => 
            MapToDto(m, m.Attachments.FirstOrDefault(), m.LiveSessionId.HasValue && liveSessions.TryGetValue(m.LiveSessionId.Value, out var ls) ? ls : null)
        ).ToList();
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
        if (userId == Guid.Empty)
        {
            return Unauthorized(ApiResponse.Fail("Invalid user token."));
        }

        DateTime since = DateTime.UtcNow.AddSeconds(-8);
        if (!string.IsNullOrEmpty(after) && DateTime.TryParse(after, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
        {
            since = parsed.ToUniversalTime();
        }

        var newMessagesRaw = await _context.ChatMessages
            .AsNoTracking()
            .Include(m => m.Attachments)
            .Where(m => (m.RecipientId == userId || m.SenderId == userId || (isAccountant && m.RecipientId == null)) && m.SentAt > since)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        if (activeEmployeeId.HasValue && activeEmployeeId.Value != Guid.Empty)
        {
            await _context.ChatMessages
                .Where(m => m.SenderId == activeEmployeeId.Value && m.RecipientId == userId && !m.IsReadByUser)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsReadByUser, true));
        }

        var pollMsgIds = newMessagesRaw
            .Where(m => m.LiveSessionId.HasValue)
            .Select(m => m.LiveSessionId!.Value)
            .Distinct()
            .ToList();

        var pollLiveSessions = pollMsgIds.Any()
            ? await _context.LiveLocationSessions
                .AsNoTracking()
                .Where(s => pollMsgIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id)
            : new Dictionary<Guid, LiveLocationSession>();

        var totalUnread = await _context.ChatMessages
            .AsNoTracking()
            .CountAsync(m => m.RecipientId == userId && !m.IsReadByUser);

        var dtos = newMessagesRaw.Select(m => 
            MapToDto(m, m.Attachments.FirstOrDefault(), m.LiveSessionId.HasValue && pollLiveSessions.TryGetValue(m.LiveSessionId.Value, out var s) ? s : null)
        ).ToList();

        var result = new PollChatResponseDto
        {
            IsAccountant = isAccountant,
            ServerTime = DateTime.UtcNow.ToString("o"),
            TotalUnread = totalUnread,
            NewMessages = dtos,
            ActiveMessages = dtos,
            NewBroadcasts = dtos
        };

        return Ok(ApiResponse<PollChatResponseDto>.Ok(result));
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

        // Strict Authorization: Only conversation participants can view attachments (plus accountants for legacy accounting desk messages)
        bool isAuthorized = attachment.Message.SenderId == userId || 
                            attachment.Message.RecipientId == userId ||
                            (isAccountant && (attachment.Message.IsFromAccountant || attachment.Message.RecipientId == null));

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

        bool isAuthorized = attachment.Message.SenderId == userId || 
                            attachment.Message.RecipientId == userId ||
                            (isAccountant && (attachment.Message.IsFromAccountant || attachment.Message.RecipientId == null));

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

    private static bool IsValidCoordinate(double lat, double lng)
    {
        return !double.IsNaN(lat) && !double.IsInfinity(lat) &&
               !double.IsNaN(lng) && !double.IsInfinity(lng) &&
               lat >= -90.0 && lat <= 90.0 &&
               lng >= -180.0 && lng <= 180.0;
    }

    private static ChatMessageDto MapToDto(ChatMessage m, ChatAttachment? att, LiveLocationSession? liveSession = null)
    {
        bool? isLiveActive = null;
        DateTime? liveExpiresAt = null;
        DateTime? liveStartedAt = null;
        DateTime? liveEndedAt = null;
        int? liveDurationMinutes = null;
        double? lat = m.Latitude;
        double? lng = m.Longitude;
        string? status = m.Status;

        if (liveSession != null)
        {
            isLiveActive = liveSession.IsActive && liveSession.ExpiresAt > DateTime.UtcNow;
            liveExpiresAt = liveSession.ExpiresAt;
            liveStartedAt = liveSession.StartedAt;
            liveEndedAt = liveSession.EndedAt;
            liveDurationMinutes = liveSession.DurationMinutes;
            lat = liveSession.LastLatitude;
            lng = liveSession.LastLongitude;
            status = liveSession.Status ?? (isLiveActive == true ? "ACTIVE" : "ENDED");
        }
        else if (m.LiveSessionId.HasValue ||
                 string.Equals(m.MessageType, MessageTypes.LiveLocation, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(m.MessageType, "LiveLocation", StringComparison.OrdinalIgnoreCase))
        {
            isLiveActive = false;
            status = m.Status ?? "ENDED";
        }

        var isLiveLoc = m.LiveSessionId.HasValue ||
                        string.Equals(m.MessageType, MessageTypes.LiveLocation, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(m.MessageType, "LiveLocation", StringComparison.OrdinalIgnoreCase);
        var isStaticLoc = string.Equals(m.MessageType, MessageTypes.Location, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(m.MessageType, "Location", StringComparison.OrdinalIgnoreCase);

        var normalizedType = isLiveLoc ? MessageTypes.LiveLocation : (isStaticLoc ? MessageTypes.Location : (string.IsNullOrEmpty(m.MessageType) ? MessageTypes.Text : m.MessageType));

        // CRITICAL: Ensure location messages NEVER carry "Live Location" or "Shared Location" as text payload
        var sanitizedText = (isLiveLoc || isStaticLoc) ? string.Empty : (m.MessageText ?? string.Empty);

        // Derive conversation identifier: For employee<->accountant chats, it is the employee's ID
        Guid? conversationId = m.IsFromAccountant ? m.RecipientId : m.SenderId;

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
            MessageText = sanitizedText,
            HasAttachment = m.HasAttachment,
            IsFromAccountant = m.IsFromAccountant,
            IsReadByAccountant = m.IsReadByAccountant,
            IsReadByUser = m.IsReadByUser,
            SentAt = m.SentAt,
            ClientMessageId = m.ClientMessageId,
            MessageType = normalizedType,
            Status = status,
            ConversationId = conversationId,
            Latitude = lat,
            Longitude = lng,
            LiveSessionId = m.LiveSessionId,
            IsLiveActive = isLiveActive,
            LiveExpiresAt = liveExpiresAt,
            LiveStartedAt = liveStartedAt,
            LiveEndedAt = liveEndedAt,
            StartedAt = liveStartedAt ?? m.SentAt,
            EndedAt = liveEndedAt,
            LiveDurationMinutes = liveDurationMinutes,
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
