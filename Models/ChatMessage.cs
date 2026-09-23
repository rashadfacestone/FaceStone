namespace FaceStone.Web.Models;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Who sent this message
    public Guid? SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderUsername { get; set; }
    public string SenderRole { get; set; } = string.Empty;

    // To whom this message was sent
    public Guid? RecipientId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientUsername { get; set; }
    public string? RecipientRole { get; set; }

    // Plain text message (NO HASHING, NO ENCRYPTION)
    public string MessageText { get; set; } = string.Empty;
    public bool HasAttachment { get; set; }

    public bool IsFromAccountant { get; set; }
    public bool IsReadByAccountant { get; set; }
    public bool IsReadByUser { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    // Client-generated Correlation ID for idempotency and optimistic UI deduplication
    public string? ClientMessageId { get; set; }

    // Navigation property
    public virtual ICollection<ChatAttachment> Attachments { get; set; } = new List<ChatAttachment>();
}
