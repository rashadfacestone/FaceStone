using Microsoft.AspNetCore.Http;

namespace FaceStone.Web.Models.DTOs;

public class SendMessageDto
{
    public string? Message { get; set; }
    public Guid? TargetEmployeeId { get; set; }
    public IFormFile? Attachment { get; set; }
    public string? ClientMessageId { get; set; }
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
