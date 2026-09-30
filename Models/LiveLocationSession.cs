namespace FaceStone.Web.Models;

public class LiveLocationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // The user who initiated the live location sharing
    public Guid UserId { get; set; }

    // The employee conversation thread this belongs to
    public Guid EmployeeId { get; set; }

    // The chat message ID associated with this live location card
    public Guid MessageId { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = "ACTIVE"; // "ACTIVE", "ENDED", "EXPIRED"

    // Latest coordinates
    public double LastLatitude { get; set; }

    public double LastLongitude { get; set; }

    // Configured duration: 15, 30, or 60 minutes
    public int DurationMinutes { get; set; } = 15;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
