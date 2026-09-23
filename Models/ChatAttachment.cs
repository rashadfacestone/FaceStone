namespace FaceStone.Web.Models;

public class ChatAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }

    // Who sent and to whom
    public string SenderName { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;

    // File details
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty; // "pdf", "image", "word", "excel", "document"
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual ChatMessage? Message { get; set; }
}
