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
    public DbSet<PurchaseOrderPaymentAudit> PurchaseOrderPaymentAudits => Set<PurchaseOrderPaymentAudit>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<EmployeeFinancialRecord> EmployeeFinancialRecords => Set<EmployeeFinancialRecord>();
    public DbSet<EmployeeLending> EmployeeLendings => Set<EmployeeLending>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

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

            entity.HasIndex(e => new { e.PONumber, e.TransactionNumber })
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

            entity.Property(e => e.CompanyName)
                  .HasMaxLength(200)
                  .HasDefaultValue(string.Empty)
                  .IsRequired();

            entity.Property(e => e.InternalWeight)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.ExternalLoadedWeight)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.ExternalEmptyWeight)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.ExternalNetWeight)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.WasteQuantity)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.DriverPhone)
                  .HasMaxLength(30)
                  .IsRequired(false);

            entity.Property(e => e.DepartureTime)
                  .IsRequired(false);

            entity.Property(e => e.ArrivalTime)
                  .IsRequired(false);

            entity.Property(e => e.ShippingSupervisor)
                  .HasMaxLength(100)
                  .HasDefaultValue("محمد ناجح")
                  .IsRequired();

            entity.Property(e => e.DepartmentSupervisor)
                  .HasMaxLength(100)
                  .HasDefaultValue("ازهر محمد")
                  .IsRequired();

            entity.Property(e => e.RecipientName)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.Property(e => e.RecipientTitle)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.ReceivedDate)
                  .IsRequired(false);

            entity.Property(e => e.InternalWeighbridgeCode)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.InternalEmptyWeightTime)
                  .IsRequired(false);

            entity.Property(e => e.InternalLoadedWeightTime)
                  .IsRequired(false);

            entity.Property(e => e.ExternalWeighbridgeCode)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.ExternalEmptyWeightTime)
                  .IsRequired(false);

            entity.Property(e => e.ExternalLoadedWeightTime)
                  .IsRequired(false);

            entity.Property(e => e.ShippingDurationMinutes)
                  .IsRequired(false);

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

            entity.Property(e => e.Ikramiya)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.CustodyAmount)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.InvoiceAttachmentPath)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.InvoiceFileName)
                  .HasMaxLength(260)
                  .IsRequired(false);

            entity.Property(e => e.InvoiceFileType)
                  .HasMaxLength(50)
                  .IsRequired(false);

            entity.Property(e => e.InvoiceFileSize)
                  .HasDefaultValue(0L)
                  .IsRequired();

            // Section 1: Weighbridge Attachment
            entity.Property(e => e.WeighbridgeAttachmentPath)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.WeighbridgeFileName)
                  .HasMaxLength(260)
                  .IsRequired(false);

            entity.Property(e => e.WeighbridgeFileType)
                  .HasMaxLength(50)
                  .IsRequired(false);

            entity.Property(e => e.WeighbridgeFileSize)
                  .HasDefaultValue(0L)
                  .IsRequired();

            // Section 2: Daily Supply Attachment
            entity.Property(e => e.DailySupplyAttachmentPath)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.DailySupplyFileName)
                  .HasMaxLength(260)
                  .IsRequired(false);

            entity.Property(e => e.DailySupplyFileType)
                  .HasMaxLength(50)
                  .IsRequired(false);

            entity.Property(e => e.DailySupplyFileSize)
                  .HasDefaultValue(0L)
                  .IsRequired();

            // Section 3: Bill of Lading Attachment
            entity.Property(e => e.BillOfLadingAttachmentPath)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.BillOfLadingFileName)
                  .HasMaxLength(260)
                  .IsRequired(false);

            entity.Property(e => e.BillOfLadingFileType)
                  .HasMaxLength(50)
                  .IsRequired(false);

            entity.Property(e => e.BillOfLadingFileSize)
                  .HasDefaultValue(0L)
                  .IsRequired();

            entity.Property(e => e.SubmittedBy)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.HasOne(e => e.PurchaseOrder)
                  .WithMany(p => p.TransportationTransactions)
                  .HasForeignKey(e => e.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.SetNull);

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

            entity.Property(e => e.Material)
                  .HasMaxLength(100)
                  .HasDefaultValue("powder")
                  .IsRequired();

            entity.Property(e => e.Action)
                  .HasMaxLength(50)
                  .HasDefaultValue("Import")
                  .IsRequired();

            entity.Property(e => e.ServiceType)
                  .HasMaxLength(50)
                  .HasDefaultValue("إيداع")
                  .IsRequired();

            entity.Property(e => e.ReferenceNumber)
                  .IsRequired();

            entity.HasIndex(e => e.ReferenceNumber);

            entity.Property(e => e.IsExportRelatedToPO)
                  .HasDefaultValue(false)
                  .IsRequired();

            entity.Property(e => e.PONumber)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.PurchaseOrderId)
                  .IsRequired(false);

            entity.HasOne(e => e.PurchaseOrder)
                  .WithMany(p => p.StorageTransactions)
                  .HasForeignKey(e => e.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.Property(e => e.IncomingQuantity)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.OutgoingQuantity)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.OpeningBalance)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.ClosingBalance)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.StorageLocation)
                  .HasMaxLength(200)
                  .HasDefaultValue("مستودع الخامات الرئيسي")
                  .IsRequired();

            entity.Property(e => e.DestinationStorageLocation)
                  .HasMaxLength(200)
                  .IsRequired(false);

            // Section 3: الميزان الداخلي
            entity.Property(e => e.InternalWeighbridgeReference)
                  .HasMaxLength(100)
                  .HasDefaultValue(string.Empty)
                  .IsRequired();

            entity.Property(e => e.InternalLoadedWeight)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.InternalEmptyWeight)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.InternalNetWeight)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(e => e.InternalEmptyWeightTime)
                  .IsRequired(false);

            entity.Property(e => e.InternalLoadedWeightTime)
                  .IsRequired(false);

            // Section 3: الميزان الخارجي
            entity.Property(e => e.HasExternalWeighbridge)
                  .HasDefaultValue(false)
                  .IsRequired();

            entity.Property(e => e.ExternalWeighbridgeReference)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.ExternalLoadedWeight)
                  .HasPrecision(18, 3)
                  .IsRequired(false);

            entity.Property(e => e.ExternalEmptyWeight)
                  .HasPrecision(18, 3)
                  .IsRequired(false);

            entity.Property(e => e.ExternalNetWeight)
                  .HasPrecision(18, 3)
                  .IsRequired(false);

            entity.Property(e => e.ExternalEmptyWeightTime)
                  .IsRequired(false);

            entity.Property(e => e.ExternalLoadedWeightTime)
                  .IsRequired(false);

            // Section 3: إيصال الميزان
            entity.Property(e => e.WeighbridgeAttachmentPath)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.WeighbridgeFileName)
                  .HasMaxLength(260)
                  .IsRequired(false);

            entity.Property(e => e.WeighbridgeFileType)
                  .HasMaxLength(50)
                  .IsRequired(false);

            entity.Property(e => e.WeighbridgeFileSize)
                  .HasDefaultValue(0L)
                  .IsRequired();

            // Section 4: الموظف المسؤول
            entity.Property(e => e.ReceivingEmployee)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.CreatedBy)
                  .HasMaxLength(150)
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
            entity.HasIndex(e => e.Material);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.PONumber);
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

            entity.Property(e => e.TonPrice)
                  .HasPrecision(18, 3)
                  .HasDefaultValue(0m)
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

            entity.Property(e => e.Destination)
                  .HasMaxLength(200)
                  .HasDefaultValue("Cairo Factory")
                  .IsRequired();

            entity.Property(e => e.RemainingQuantity)
                  .HasPrecision(18, 3)
                  .IsRequired();

            entity.Property(e => e.PaymentStatus)
                  .HasMaxLength(50)
                  .HasDefaultValue("غير مدفوع")
                  .IsRequired();

            entity.Property(e => e.PaymentStatusUpdatedAt)
                  .IsRequired(false);

            entity.Property(e => e.PaymentStatusUpdatedBy)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasMany(e => e.Payments)
                  .WithOne(p => p.PurchaseOrder)
                  .HasForeignKey(p => p.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.PaymentAudits)
                  .WithOne(a => a.PurchaseOrder)
                  .HasForeignKey(a => a.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.OrderDate);
            entity.HasIndex(e => e.Supplier);
            entity.HasIndex(e => e.InvoiceNumber);
            entity.HasIndex(e => e.PaymentStatus);
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

        modelBuilder.Entity<PurchaseOrderPaymentAudit>(entity =>
        {
            entity.ToTable("PurchaseOrderPaymentAudits");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PONumber)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.PreviousStatus)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.NewStatus)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.ChangedBy)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.ChangedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.PONumber);
            entity.HasIndex(e => e.PurchaseOrderId);
            entity.HasIndex(e => e.ChangedAt);
        });

        modelBuilder.Entity<BankTransaction>(entity =>
        {
            entity.ToTable("BankTransactions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TransactionType)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Direction)
                  .HasMaxLength(20)
                  .HasDefaultValue("In")
                  .IsRequired();

            entity.Property(e => e.Amount)
                  .HasPrecision(18, 2)
                  .IsRequired();

            entity.Property(e => e.BankName)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ReferenceNumber)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.Description)
                  .HasMaxLength(500)
                  .IsRequired();

            entity.Property(e => e.CreatedBy)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.TransactionDate);
            entity.HasIndex(e => e.BankName);
            entity.HasIndex(e => e.Direction);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.ToTable("Expenses");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Category)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Amount)
                  .HasPrecision(18, 2)
                  .IsRequired();

            entity.Property(e => e.Title)
                  .HasMaxLength(300)
                  .IsRequired();

            entity.Property(e => e.Beneficiary)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.ReferenceNumber)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.PaymentMethod)
                  .HasMaxLength(50)
                  .HasDefaultValue("Bank Transfer")
                  .IsRequired();

            entity.Property(e => e.CreatedBy)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(1000)
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ExpenseDate);
            entity.HasIndex(e => e.Category);
        });

        modelBuilder.Entity<EmployeeFinancialRecord>(entity =>
        {
            entity.ToTable("EmployeeFinancialRecords");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.BasicSalary)
                  .HasPrecision(18, 2)
                  .HasDefaultValue(0m);

            entity.Property(e => e.Allowances)
                  .HasPrecision(18, 2)
                  .HasDefaultValue(0m);

            entity.Property(e => e.InsuranceDeductions)
                  .HasPrecision(18, 2)
                  .HasDefaultValue(0m);

            entity.Property(e => e.NetSalary)
                  .HasPrecision(18, 2)
                  .HasDefaultValue(0m);

            entity.Property(e => e.PaymentMethod)
                  .HasMaxLength(50)
                  .HasDefaultValue("Bank Transfer");

            entity.Property(e => e.BankAccountNumber)
                  .HasMaxLength(100)
                  .IsRequired(false);

            entity.Property(e => e.Notes)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.UpdatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<EmployeeLending>(entity =>
        {
            entity.ToTable("EmployeeLendings");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.LendingAmount)
                  .HasPrecision(18, 2)
                  .IsRequired();

            entity.Property(e => e.PaidAmount)
                  .HasPrecision(18, 2)
                  .HasDefaultValue(0m);

            entity.Property(e => e.RemainingAmount)
                  .HasPrecision(18, 2)
                  .IsRequired();

            entity.Property(e => e.ApprovalStatus)
                  .HasMaxLength(50)
                  .HasDefaultValue("معتمد")
                  .IsRequired();

            entity.Property(e => e.ApprovedBy)
                  .HasMaxLength(150)
                  .IsRequired(false);

            entity.Property(e => e.Notes)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.RequestDate);
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.ToTable("AttendanceRecords");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Status)
                  .HasMaxLength(50)
                  .HasDefaultValue("Present")
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.UserId, e.Date });
        });
    }
}


