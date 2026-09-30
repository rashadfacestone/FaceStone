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
    public DbSet<LiveLocationSession> LiveLocationSessions => Set<LiveLocationSession>();
    public DbSet<Transportation> Transportations => Set<Transportation>();
    public DbSet<Management> Management => Set<Management>();
    public DbSet<StorageTransaction> StorageTransactions => Set<StorageTransaction>();
    public DbSet<StorageTransactionMovement> StorageTransactionMovements => Set<StorageTransactionMovement>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<FinancePayment> FinancePayments => Set<FinancePayment>();

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
            entity.HasIndex(e => new { e.SenderId, e.SentAt });
            entity.HasIndex(e => new { e.RecipientId, e.SentAt });
            entity.HasIndex(e => new { e.SenderId, e.IsFromAccountant, e.IsReadByAccountant });
            entity.HasIndex(e => new { e.RecipientId, e.IsFromAccountant, e.IsReadByUser });
            entity.HasIndex(e => e.LiveSessionId);

            entity.Property(e => e.SentAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.MessageType)
                  .HasMaxLength(50)
                  .HasDefaultValue("Text");

            entity.Property(e => e.Latitude);
            entity.Property(e => e.Longitude);
            entity.Property(e => e.LiveSessionId);

            entity.HasMany(e => e.Attachments)
                  .WithOne(a => a.Message)
                  .HasForeignKey(a => a.MessageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LiveLocationSession>(entity =>
        {
            entity.ToTable("LiveLocationSessions");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => new { e.EmployeeId, e.IsActive });

            entity.Property(e => e.StartedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");
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

        modelBuilder.Entity<Transportation>(entity =>
        {
            entity.ToTable("Transportation");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.TransactionNumber)
                  .IsRequired();

            entity.HasIndex(e => e.TransactionNumber)
                  .IsUnique();

            entity.Property(e => e.TransactionDate)
                  .IsRequired();

            entity.Property(e => e.CarNumber)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.DriverName)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.Source)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.PONumber)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.MaterialType)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.WeightBeforeLoading)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.WeightWhenEnteringFactory)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.WeightWhenExitingFactory)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.ExpectedWeight)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.NetWeight)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.Destination)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.TransactionStatus)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.ReceivingStatus)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.DateOfSubmit)
                  .IsRequired();
        });

        modelBuilder.Entity<Management>(entity =>
        {
            entity.ToTable("Management");

            entity.HasKey(e => e.TransactionNumber);

            entity.Property(e => e.TransactionNumber)
                  .HasMaxLength(100)
                  .ValueGeneratedNever()
                  .IsRequired();

            entity.Property(e => e.Date)
                  .IsRequired();

            entity.Property(e => e.PO)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.MaterialType)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.NumberOfTransactions)
                  .IsRequired();

            entity.Property(e => e.TotalReceivedAmount)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.CarPlatesNumber)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.AcceptedQuantity)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.UnacceptedPendingQuantity)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.DeliveryStatus)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Destination)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.HasIndex(e => e.Date);
        });

        modelBuilder.Entity<StorageTransaction>(entity =>
        {
            entity.ToTable("StorageTransactions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MovementNumber)
                  .IsRequired();

            entity.Property(e => e.TransactionDate)
                  .IsRequired();

            entity.Property(e => e.ServiceType)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.ReferenceNumber)
                  .IsRequired();

            entity.HasIndex(e => e.ReferenceNumber)
                  .IsUnique();

            entity.Property(e => e.PONumber)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.IncomingQuantity)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.OutgoingQuantity)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.OpeningBalance)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.ClosingBalance)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.StorageLocation)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.DestinationStorageLocation)
                  .HasMaxLength(200)
                  .IsRequired(false);

            entity.Property(e => e.ReceivingEmployee)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasMany(e => e.TransportationMovements)
                  .WithOne(m => m.StorageTransaction)
                  .HasForeignKey(m => m.StorageTransactionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.TransactionDate);
            entity.HasIndex(e => e.MovementNumber);
            entity.HasIndex(e => e.StorageLocation);
        });

        modelBuilder.Entity<StorageTransactionMovement>(entity =>
        {
            entity.ToTable("StorageTransactionMovements");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TransportationMovementNumber)
                  .IsRequired();

            entity.HasIndex(e => e.StorageTransactionId);
            entity.HasIndex(e => e.TransportationMovementNumber);
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("PurchaseOrders");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PONumber)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.HasIndex(e => e.PONumber)
                  .IsUnique();

            entity.Property(e => e.Supplier)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.OrderDate)
                  .IsRequired();

            entity.Property(e => e.Quantity)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.PricePerTon)
                  .HasPrecision(18, 2)
                  .IsRequired();

            entity.Property(e => e.TotalValue)
                  .HasPrecision(18, 2)
                  .IsRequired();

            entity.Property(e => e.InvoiceNumber)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasMany(e => e.Payments)
                  .WithOne(p => p.PurchaseOrder)
                  .HasForeignKey(p => p.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.OrderDate);
            entity.HasIndex(e => e.Supplier);
            entity.HasIndex(e => e.InvoiceNumber);
        });

        modelBuilder.Entity<FinancePayment>(entity =>
        {
            entity.ToTable("FinancePayments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PONumber)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Amount)
                  .HasPrecision(18, 2)
                  .IsRequired();

            entity.Property(e => e.PaymentDate)
                  .IsRequired();

            entity.Property(e => e.PaymentMethod)
                  .HasMaxLength(50)
                  .HasDefaultValue("Bank Transfer");

            entity.Property(e => e.ReferenceNumber)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.PaidBy)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.PONumber);
            entity.HasIndex(e => e.PaymentDate);
        });
    }
}

