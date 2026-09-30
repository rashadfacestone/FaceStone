using Microsoft.AspNetCore.Http;

namespace FaceStone.Web.Models.DTOs;

public class LocationCoordsDto
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
}

public class SendMessageDto
{
    public string? MessageType { get; set; } = "TEXT";
    public string? Message { get; set; }
    public Guid? TargetEmployeeId { get; set; }
    public Guid? ConversationId { get; set; }
    public string? Status { get; set; } = "ACTIVE";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public LocationCoordsDto? Location { get; set; }
    public int? DurationMinutes { get; set; } = 15;
    public Guid? LiveSessionId { get; set; }
    public DateTime? StartedAt { get; set; }
    public IFormFile? Attachment { get; set; }
    public string? ClientMessageId { get; set; }

    public double? GetEffectiveLatitude()
    {
        return Latitude ?? Lat ?? Location?.Latitude ?? Location?.Lat;
    }

    public double? GetEffectiveLongitude()
    {
        return Longitude ?? Lng ?? Location?.Longitude ?? Location?.Lng;
    }

    public Guid? GetEffectiveTargetEmployeeId()
    {
        if (TargetEmployeeId.HasValue && TargetEmployeeId.Value != Guid.Empty)
            return TargetEmployeeId.Value;
        if (ConversationId.HasValue && ConversationId.Value != Guid.Empty)
            return ConversationId.Value;
        return null;
    }
}

public class ChatAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid? SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderUsername { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public Guid? RecipientId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientUsername { get; set; }
    public string? RecipientRole { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public bool HasAttachment { get; set; }
    public bool IsFromAccountant { get; set; }
    public bool IsReadByAccountant { get; set; }
    public bool IsReadByUser { get; set; }
    public DateTime SentAt { get; set; }
    public string? ClientMessageId { get; set; }
    public ChatAttachmentDto? Attachment { get; set; }

    // Location Fields
    public string MessageType { get; set; } = "TEXT"; // "TEXT", "LOCATION", "LIVE_LOCATION"
    public string? Status { get; set; } // "ACTIVE", "ENDED", "EXPIRED"
    public Guid? ConversationId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public Guid? LiveSessionId { get; set; }
    public bool? IsLiveActive { get; set; }
    public DateTime? LiveExpiresAt { get; set; }
    public DateTime? LiveStartedAt { get; set; }
    public DateTime? LiveEndedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? LiveDurationMinutes { get; set; }
}

public static class MessageTypes
{
    public const string Text = "TEXT";
    public const string Location = "LOCATION";
    public const string LiveLocation = "LIVE_LOCATION";
    public const string LocationUpdate = "LOCATION_UPDATE";
    public const string LiveLocationEnded = "LIVE_LOCATION_ENDED";
}

public class SendLocationDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public Guid? TargetEmployeeId { get; set; }
    public string? ClientMessageId { get; set; }
}

public class StartLiveLocationDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int DurationMinutes { get; set; } = 15;
    public Guid? TargetEmployeeId { get; set; }
    public string? ClientMessageId { get; set; }
}

public class UpdateLiveLocationDto
{
    public Guid SessionId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class StopLiveLocationDto
{
    public Guid SessionId { get; set; }
}

public class LiveLocationUpdateDto
{
    public string MessageType { get; set; } = "LOCATION_UPDATE";
    public string Status { get; set; } = "ACTIVE";
    public Guid SessionId { get; set; }
    public Guid MessageId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid? ConversationId => EmployeeId;
    public Guid SenderId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class LiveLocationEndedDto
{
    public string MessageType { get; set; } = "LIVE_LOCATION_ENDED";
    public string Status { get; set; } = "ENDED";
    public Guid SessionId { get; set; }
    public Guid MessageId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid? ConversationId => EmployeeId;
    public DateTime EndedAt { get; set; }
    public double LastLatitude { get; set; }
    public double LastLongitude { get; set; }
}

public class ConversationSummaryDto
{
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string LastMessageText { get; set; } = string.Empty;
    public DateTime? LastMessageTime { get; set; }
    public int UnreadCount { get; set; }
    public bool HasAttachment { get; set; }
    public string? AttachmentType { get; set; }
}

public class ConversationsListResponseDto
{
    public int TotalUnread { get; set; }
    public List<ConversationSummaryDto> Conversations { get; set; } = new();
}

public class ConversationDetailResponseDto
{
    public UserDto Employee { get; set; } = null!;
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class PollChatResponseDto
{
    public bool IsAccountant { get; set; }
    public string ServerTime { get; set; } = string.Empty;
    public int TotalUnread { get; set; }
    public List<ChatMessageDto> NewBroadcasts { get; set; } = new();
    public List<ChatMessageDto> ActiveMessages { get; set; } = new();
    public List<ChatMessageDto> NewMessages { get; set; } = new();
}
