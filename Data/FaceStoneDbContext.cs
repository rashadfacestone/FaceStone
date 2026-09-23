using Microsoft.EntityFrameworkCore;
using FaceStone.Web.Models;

namespace FaceStone.Web.Data;

public class FaceStoneDbContext : DbContext
{
    public FaceStoneDbContext(DbContextOptions<FaceStoneDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();

            entity.Property(e => e.FullName)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.Username)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Email)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.CountryCode)
                  .HasMaxLength(10)
                  .HasDefaultValue("+20");

            entity.Property(e => e.PhoneNumber)
                  .HasMaxLength(30)
                  .IsRequired();

            entity.Property(e => e.PasswordHash)
                  .HasMaxLength(256)
                  .IsRequired();

            entity.Property(e => e.Role)
                  .HasMaxLength(50)
                  .HasDefaultValue("Employee");

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SenderName)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.SenderUsername)
                  .HasMaxLength(50);

            entity.Property(e => e.SenderRole)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.RecipientName)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.RecipientUsername)
                  .HasMaxLength(50);

            entity.Property(e => e.RecipientRole)
                  .HasMaxLength(50);

            entity.Property(e => e.MessageText)
                  .IsUnicode(true); // Plain text, unencrypted

            entity.Property(e => e.ClientMessageId)
                  .HasMaxLength(64);

            entity.HasIndex(e => new { e.SenderId, e.ClientMessageId });

            entity.Property(e => e.SentAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasMany(e => e.Attachments)
                  .WithOne(a => a.Message)
                  .HasForeignKey(a => a.MessageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatAttachment>(entity =>
        {
            entity.ToTable("ChatAttachments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SenderName)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.RecipientName)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.FileName)
                  .HasMaxLength(260)
                  .IsRequired();

            entity.Property(e => e.FilePath)
                  .HasMaxLength(500)
                  .IsRequired();

            entity.Property(e => e.FileType)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.UploadedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
        });
    }
}
