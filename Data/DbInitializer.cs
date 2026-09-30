using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using FaceStone.Web.Models;

namespace FaceStone.Web.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FaceStoneDbContext>();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var dbName = db.Database.GetDbConnection().Database;
            logger.LogInformation("[DB] Connection test started for database: {DbName}", dbName);
            if (await db.Database.CanConnectAsync(cts.Token))
            {
                logger.LogInformation("[DB] Connection successful to database: {DbName}", dbName);
            }
            await db.Database.EnsureCreatedAsync(cts.Token);

            // Resilient schema update for existing databases
            try
            {
                if (db.Database.IsSqlServer())
                {
                    await db.Database.ExecuteSqlRawAsync(@"
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ChatMessages')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ChatMessages') AND name = 'MessageType')
        ALTER TABLE ChatMessages ADD MessageType NVARCHAR(50) NOT NULL CONSTRAINT DF_ChatMessages_MessageType DEFAULT 'Text';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ChatMessages') AND name = 'Latitude')
        ALTER TABLE ChatMessages ADD Latitude FLOAT NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ChatMessages') AND name = 'Longitude')
        ALTER TABLE ChatMessages ADD Longitude FLOAT NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ChatMessages') AND name = 'LiveSessionId')
        ALTER TABLE ChatMessages ADD LiveSessionId UNIQUEIDENTIFIER NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ChatMessages') AND name = 'Status')
        ALTER TABLE ChatMessages ADD Status NVARCHAR(50) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LiveLocationSessions')
BEGIN
    CREATE TABLE LiveLocationSessions (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        EmployeeId UNIQUEIDENTIFIER NOT NULL,
        MessageId UNIQUEIDENTIFIER NOT NULL,
        StartedAt DATETIME2 NOT NULL CONSTRAINT DF_LiveLoc_StartedAt DEFAULT GETUTCDATE(),
        ExpiresAt DATETIME2 NOT NULL,
        EndedAt DATETIME2 NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_LiveLoc_IsActive DEFAULT 1,
        Status NVARCHAR(50) NOT NULL CONSTRAINT DF_LiveLoc_Status DEFAULT 'ACTIVE',
        LastLatitude FLOAT NOT NULL,
        LastLongitude FLOAT NOT NULL,
        DurationMinutes INT NOT NULL CONSTRAINT DF_LiveLoc_Duration DEFAULT 15,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_LiveLoc_UpdatedAt DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_LiveLoc_UserId ON LiveLocationSessions(UserId);
    CREATE INDEX IX_LiveLoc_EmployeeId ON LiveLocationSessions(EmployeeId);
    CREATE INDEX IX_LiveLoc_MessageId ON LiveLocationSessions(MessageId);
    CREATE INDEX IX_LiveLoc_Emp_Active ON LiveLocationSessions(EmployeeId, IsActive);
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LiveLocationSessions') AND name = 'Status')
        ALTER TABLE LiveLocationSessions ADD Status NVARCHAR(50) NOT NULL CONSTRAINT DF_LiveLoc_Status DEFAULT 'ACTIVE';
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Transportation')
BEGIN
    CREATE TABLE Transportation (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TransactionNumber INT NOT NULL,
        TransactionDate DATETIME2 NOT NULL,
        CarNumber NVARCHAR(50) NOT NULL,
        DriverName NVARCHAR(150) NOT NULL,
        Source NVARCHAR(200) NOT NULL,
        PONumber NVARCHAR(100) NOT NULL,
        MaterialType NVARCHAR(100) NOT NULL,
        WeightBeforeLoading DECIMAL(18,3) NOT NULL,
        WeightWhenEnteringFactory DECIMAL(18,3) NOT NULL,
        WeightWhenExitingFactory DECIMAL(18,3) NOT NULL,
        ExpectedWeight DECIMAL(18,3) NOT NULL,
        NetWeight DECIMAL(18,3) NOT NULL,
        Destination NVARCHAR(200) NOT NULL,
        TransactionStatus NVARCHAR(50) NOT NULL,
        ReceivingStatus NVARCHAR(50) NOT NULL,
        Notes NVARCHAR(1000) NULL,
        DateOfSubmit DATETIME2 NOT NULL
    );
    CREATE UNIQUE INDEX IX_Transportation_TransactionNumber ON Transportation(TransactionNumber);
END
ELSE
BEGIN
    -- Ensure columns exist and have correct schema if table already exists
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'TransactionDate')
    BEGIN
        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'Date')
            EXEC sp_rename 'Transportation.Date', 'TransactionDate', 'COLUMN';
        ELSE
            ALTER TABLE Transportation ADD TransactionDate DATETIME2 NOT NULL CONSTRAINT DF_Trans_TxDate DEFAULT GETUTCDATE();
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'CarNumber')
    BEGIN
        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'CarPlatesNumber')
            EXEC sp_rename 'Transportation.CarPlatesNumber', 'CarNumber', 'COLUMN';
        ELSE
            ALTER TABLE Transportation ADD CarNumber NVARCHAR(50) NOT NULL CONSTRAINT DF_Trans_CarNo DEFAULT '';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'PONumber')
    BEGIN
        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'PO')
            EXEC sp_rename 'Transportation.PO', 'PONumber', 'COLUMN';
        ELSE
            ALTER TABLE Transportation ADD PONumber NVARCHAR(100) NOT NULL CONSTRAINT DF_Trans_PONo DEFAULT '';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'WeightWhenEnteringFactory')
    BEGIN
        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'WeightWhenEnterFactory')
            EXEC sp_rename 'Transportation.WeightWhenEnterFactory', 'WeightWhenEnteringFactory', 'COLUMN';
        ELSE
            ALTER TABLE Transportation ADD WeightWhenEnteringFactory DECIMAL(18,3) NOT NULL CONSTRAINT DF_Trans_WtEnter DEFAULT 0;
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'WeightWhenExitingFactory')
    BEGIN
        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'WeightWhenExitFactory')
            EXEC sp_rename 'Transportation.WeightWhenExitFactory', 'WeightWhenExitingFactory', 'COLUMN';
        ELSE
            ALTER TABLE Transportation ADD WeightWhenExitingFactory DECIMAL(18,3) NOT NULL CONSTRAINT DF_Trans_WtExit DEFAULT 0;
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ExpectedWeight')
        ALTER TABLE Transportation ADD ExpectedWeight DECIMAL(18,3) NOT NULL CONSTRAINT DF_Trans_ExpWt DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'TransactionStatus')
    BEGIN
        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'DeliveryStatus')
            EXEC sp_rename 'Transportation.DeliveryStatus', 'TransactionStatus', 'COLUMN';
        ELSE
            ALTER TABLE Transportation ADD TransactionStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_Trans_TxStat DEFAULT 'Pending';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ReceivingStatus')
    BEGIN
        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ShippingStatus')
            EXEC sp_rename 'Transportation.ShippingStatus', 'ReceivingStatus', 'COLUMN';
        ELSE
            ALTER TABLE Transportation ADD ReceivingStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_Trans_RecvStat DEFAULT 'Pending';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'DateOfSubmit')
        ALTER TABLE Transportation ADD DateOfSubmit DATETIME2 NOT NULL CONSTRAINT DF_Trans_SubmitDate DEFAULT GETUTCDATE();
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Management')
BEGIN
    CREATE TABLE Management (
        TransactionNumber NVARCHAR(100) NOT NULL PRIMARY KEY,
        Date DATETIME2 NOT NULL,
        PO NVARCHAR(100) NULL,
        MaterialType NVARCHAR(100) NOT NULL,
        NumberOfTransactions INT NOT NULL,
        TotalReceivedAmount DECIMAL(18,3) NOT NULL,
        CarPlatesNumber NVARCHAR(50) NOT NULL,
        AcceptedQuantity DECIMAL(18,3) NOT NULL,
        UnacceptedPendingQuantity DECIMAL(18,3) NOT NULL,
        DeliveryStatus NVARCHAR(50) NOT NULL,
        Destination NVARCHAR(200) NOT NULL,
        Notes NVARCHAR(1000) NULL
    );
    CREATE INDEX IX_Management_Date ON Management(Date);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StorageTransactions')
BEGIN
    CREATE TABLE StorageTransactions (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MovementNumber INT NOT NULL,
        TransactionDate DATETIME2 NOT NULL,
        ServiceType NVARCHAR(50) NOT NULL,
        ReferenceNumber INT NOT NULL,
        PONumber NVARCHAR(100) NOT NULL,
        IncomingQuantity DECIMAL(18,3) NOT NULL,
        OutgoingQuantity DECIMAL(18,3) NOT NULL,
        OpeningBalance DECIMAL(18,3) NOT NULL,
        ClosingBalance DECIMAL(18,3) NOT NULL,
        StorageLocation NVARCHAR(200) NOT NULL,
        DestinationStorageLocation NVARCHAR(200) NULL,
        ReceivingEmployee NVARCHAR(150) NOT NULL,
        Notes NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_StorageTx_CreatedAt DEFAULT GETUTCDATE()
    );
    CREATE UNIQUE INDEX IX_StorageTransactions_ReferenceNumber ON StorageTransactions(ReferenceNumber);
    CREATE INDEX IX_StorageTransactions_Date ON StorageTransactions(TransactionDate DESC);
    CREATE INDEX IX_StorageTransactions_MovementNumber ON StorageTransactions(MovementNumber);
    CREATE INDEX IX_StorageTransactions_Location ON StorageTransactions(StorageLocation);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StorageTransactionMovements')
BEGIN
    CREATE TABLE StorageTransactionMovements (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        StorageTransactionId INT NOT NULL CONSTRAINT FK_StorageTxMovements_StorageTx REFERENCES StorageTransactions(Id) ON DELETE CASCADE,
        TransportationMovementNumber INT NOT NULL,
        TransportationId INT NULL
    );
    CREATE INDEX IX_StorageTxMovements_StorageTxId ON StorageTransactionMovements(StorageTransactionId);
    CREATE INDEX IX_StorageTxMovements_TxNumber ON StorageTransactionMovements(TransportationMovementNumber);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseOrders')
BEGIN
    CREATE TABLE PurchaseOrders (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PONumber NVARCHAR(100) NOT NULL,
        Supplier NVARCHAR(200) NOT NULL,
        OrderDate DATETIME2 NOT NULL,
        Quantity DECIMAL(18,3) NOT NULL,
        PricePerTon DECIMAL(18,2) NOT NULL,
        TotalValue DECIMAL(18,2) NOT NULL,
        InvoiceNumber NVARCHAR(100) NULL,
        Notes NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PO_CreatedAt DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL
    );
    CREATE UNIQUE INDEX IX_PurchaseOrders_PONumber ON PurchaseOrders(PONumber);
    CREATE INDEX IX_PurchaseOrders_OrderDate ON PurchaseOrders(OrderDate);
    CREATE INDEX IX_PurchaseOrders_Supplier ON PurchaseOrders(Supplier);
    CREATE INDEX IX_PurchaseOrders_InvoiceNumber ON PurchaseOrders(InvoiceNumber);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FinancePayments')
BEGIN
    CREATE TABLE FinancePayments (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PurchaseOrderId INT NULL CONSTRAINT FK_FinancePayments_PurchaseOrders REFERENCES PurchaseOrders(Id) ON DELETE SET NULL,
        PONumber NVARCHAR(100) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        PaymentDate DATETIME2 NOT NULL,
        PaymentMethod NVARCHAR(50) NOT NULL CONSTRAINT DF_FP_PaymentMethod DEFAULT 'Bank Transfer',
        ReferenceNumber NVARCHAR(100) NULL,
        PaidBy NVARCHAR(150) NULL,
        Notes NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_FP_CreatedAt DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_FinancePayments_PONumber ON FinancePayments(PONumber);
    CREATE INDEX IX_FinancePayments_PaymentDate ON FinancePayments(PaymentDate);
END
", cts.Token);

                    // Step 2: In a separate batch, normalize any legacy location rows now that columns exist
                    await db.Database.ExecuteSqlRawAsync(@"
UPDATE ChatMessages SET MessageType = 'LIVE_LOCATION', MessageText = '' WHERE LiveSessionId IS NOT NULL OR MessageType = 'LiveLocation' OR MessageText = 'Live Location';
UPDATE ChatMessages SET MessageType = 'LOCATION', MessageText = '' WHERE (MessageType = 'Location' OR MessageText = 'Shared Location') AND LiveSessionId IS NULL;
UPDATE ChatMessages SET Status = 'ACTIVE' WHERE MessageType = 'LIVE_LOCATION' AND (Status IS NULL OR Status = '');
UPDATE LiveLocationSessions SET Status = 'ACTIVE' WHERE (Status IS NULL OR Status = '') AND IsActive = 1;
UPDATE LiveLocationSessions SET Status = 'ENDED' WHERE (Status IS NULL OR Status = '') AND IsActive = 0;
", cts.Token);

                    // Step 3: High-performance indexes for ChatMessages
                    await db.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ChatMessages_Sender_SentAt' AND object_id = OBJECT_ID('ChatMessages'))
    CREATE INDEX IX_ChatMessages_Sender_SentAt ON ChatMessages(SenderId, SentAt DESC);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ChatMessages_Recipient_SentAt' AND object_id = OBJECT_ID('ChatMessages'))
    CREATE INDEX IX_ChatMessages_Recipient_SentAt ON ChatMessages(RecipientId, SentAt DESC);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ChatMessages_Unread_Acc' AND object_id = OBJECT_ID('ChatMessages'))
    CREATE INDEX IX_ChatMessages_Unread_Acc ON ChatMessages(SenderId, IsFromAccountant, IsReadByAccountant);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ChatMessages_Unread_User' AND object_id = OBJECT_ID('ChatMessages'))
    CREATE INDEX IX_ChatMessages_Unread_User ON ChatMessages(RecipientId, IsFromAccountant, IsReadByUser);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ChatMessages_LiveSessionId' AND object_id = OBJECT_ID('ChatMessages'))
    CREATE INDEX IX_ChatMessages_LiveSessionId ON ChatMessages(LiveSessionId);
", cts.Token);
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation("Database schema synchronization notice: {Msg}", ex.Message);
            }

            if (!await db.Users.AnyAsync(cts.Token))
            {
                logger.LogInformation("Database is empty. Seeding initial accounts...");

                var defaultAccounts = new List<UserAccount>
                {
                    new UserAccount
                    {
                        Id = Guid.NewGuid(),
                        FullName = "Sarah Mitchell",
                        Username = "sarah_acc",
                        Email = "sarah@facestone.com",
                        PhoneNumber = "+20 10 1122 3344",
                        CountryCode = "+20",
                        Role = AppRoles.Accountant,
                        PasswordHash = HashPassword("SecretPass123!"),
                        CreatedAt = DateTime.UtcNow
                    },
                    new UserAccount
                    {
                        Id = Guid.NewGuid(),
                        FullName = "Alexander Wright",
                        Username = "alex_stone",
                        Email = "alex@facestone.com",
                        PhoneNumber = "+20 10 5566 7788",
                        CountryCode = "+20",
                        Role = AppRoles.SeniorStructuralEngineer,
                        PasswordHash = HashPassword("Password123!"),
                        CreatedAt = DateTime.UtcNow
                    }
                };

                await db.Users.AddRangeAsync(defaultAccounts);
                await db.SaveChangesAsync();
                logger.LogInformation("Database seeded successfully with initial accounts.");
            }

            // Safe update: Ensure CEO, IT, and TRANSPORTATION_MANAGER roles exist for department testing
            var ceoUser = await db.Users.FirstOrDefaultAsync(u => u.Role == AppRoles.CEO || u.Email == "kareem.ceo@facestone.com", cts.Token);
            if (ceoUser != null)
            {
                ceoUser.PasswordHash = HashPassword("Password123!");
            }
            else
            {
                await db.Users.AddAsync(new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Kareem Hassan",
                    Username = "kareem_ceo",
                    Email = "kareem.ceo@facestone.com",
                    PhoneNumber = "+20 10 0011 2233",
                    CountryCode = "+20",
                    Role = AppRoles.CEO,
                    PasswordHash = HashPassword("Password123!"),
                    CreatedAt = DateTime.UtcNow
                }, cts.Token);
            }

            var itUser = await db.Users.FirstOrDefaultAsync(u => u.Role == AppRoles.IT || u.Email == "it@facestone.com", cts.Token);
            if (itUser == null)
            {
                await db.Users.AddAsync(new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "FaceStone IT Support",
                    Username = "it_admin",
                    Email = "it@facestone.com",
                    PhoneNumber = "+20 10 7788 9900",
                    CountryCode = "+20",
                    Role = AppRoles.IT,
                    PasswordHash = HashPassword("Password123!"),
                    CreatedAt = DateTime.UtcNow
                }, cts.Token);
            }

            var transUser = await db.Users.FirstOrDefaultAsync(u => u.Role == AppRoles.TransportationManager || u.Email == "transport@facestone.com", cts.Token);
            if (transUser == null)
            {
                await db.Users.AddAsync(new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Transportation Logistics Manager",
                    Username = "transport_manager",
                    Email = "transport@facestone.com",
                    PhoneNumber = "+20 10 3344 5566",
                    CountryCode = "+20",
                    Role = AppRoles.TransportationManager,
                    PasswordHash = HashPassword("Password123!"),
                    CreatedAt = DateTime.UtcNow
                }, cts.Token);
            }

            var storageUser = await db.Users.FirstOrDefaultAsync(u => u.Role == AppRoles.StorageManager || u.Role == "StorageManager" || u.Email == "storage@facestone.com", cts.Token);
            if (storageUser == null)
            {
                await db.Users.AddAsync(new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Storage & Factory Operations Manager",
                    Username = "storage_manager",
                    Email = "storage@facestone.com",
                    PhoneNumber = "+20 10 4455 6677",
                    CountryCode = "+20",
                    Role = AppRoles.StorageManager,
                    PasswordHash = HashPassword("Storage123!"),
                    CreatedAt = DateTime.UtcNow
                }, cts.Token);
            }
            else
            {
                storageUser.PasswordHash = HashPassword("Storage123!");
                storageUser.Role = AppRoles.StorageManager;
            }

            var factoryManagerUser = await db.Users.FirstOrDefaultAsync(u => u.Role == AppRoles.FactoryManager || u.Email == "manager@facestone.com", cts.Token);
            if (factoryManagerUser == null)
            {
                await db.Users.AddAsync(new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Factory Manager",
                    Username = "factory_manager",
                    Email = "manager@facestone.com",
                    PhoneNumber = "+20 10 5566 7788",
                    CountryCode = "+20",
                    Role = AppRoles.FactoryManager,
                    PasswordHash = HashPassword("Manager123!"),
                    CreatedAt = DateTime.UtcNow
                }, cts.Token);
            }
            else
            {
                factoryManagerUser.PasswordHash = HashPassword("Manager123!");
                factoryManagerUser.Role = AppRoles.FactoryManager;
            }

            // Seed initial Storage Transactions if table is empty
            if (!await db.StorageTransactions.AnyAsync(cts.Token))
            {
                logger.LogInformation("Seeding initial storage transactions...");
                var tx1 = new StorageTransaction
                {
                    MovementNumber = 5001,
                    TransactionDate = DateTime.UtcNow.AddDays(-2),
                    ServiceType = "إضافة",
                    ReferenceNumber = 7001,
                    PONumber = "PO-2026-FS-101",
                    IncomingQuantity = 150.000m,
                    OutgoingQuantity = 0.000m,
                    OpeningBalance = 0.000m,
                    ClosingBalance = 150.000m,
                    StorageLocation = "مستودع الخامات الرئيسي (صوامع وتخزين الركام)",
                    ReceivingEmployee = "Storage & Factory Operations Manager",
                    Notes = "رصيد افتتاحي وتوريد مواد خام خرسانية معتمد",
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    TransportationMovements = new List<StorageTransactionMovement>
                    {
                        new() { TransportationMovementNumber = 1001 },
                        new() { TransportationMovementNumber = 1002 }
                    }
                };

                var tx2 = new StorageTransaction
                {
                    MovementNumber = 5002,
                    TransactionDate = DateTime.UtcNow.AddDays(-1),
                    ServiceType = "صرف",
                    ReferenceNumber = 7002,
                    PONumber = "PO-2026-FS-101",
                    IncomingQuantity = 0.000m,
                    OutgoingQuantity = 35.500m,
                    OpeningBalance = 150.000m,
                    ClosingBalance = 114.500m,
                    StorageLocation = "مستودع الخامات الرئيسي (صوامع وتخزين الركام)",
                    ReceivingEmployee = "Storage & Factory Operations Manager",
                    Notes = "صرف مواد لخط إنتاج الصب الجاهز",
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    TransportationMovements = new List<StorageTransactionMovement>
                    {
                        new() { TransportationMovementNumber = 1003 }
                    }
                };

                await db.StorageTransactions.AddRangeAsync(new[] { tx1, tx2 }, cts.Token);
            }

            // Seed initial Purchase Orders if table is empty
            if (!await db.PurchaseOrders.AnyAsync(cts.Token))
            {
                logger.LogInformation("Seeding initial Purchase Orders for Finance Department...");
                var po1 = new PurchaseOrder
                {
                    PONumber = "PO-2026-9810",
                    Supplier = "Suez Quarry Sector 4",
                    OrderDate = DateTime.UtcNow.AddDays(-10),
                    Quantity = 200.000m,
                    PricePerTon = 1450.00m,
                    TotalValue = 200.000m * 1450.00m, // 290,000.00 EGP
                    InvoiceNumber = "FS-2026-INV-892",
                    Notes = "توريد خرسانة مسلحة C35/45 - موقع البرج الإداري بالعاصمة الإدارية",
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                };

                var po2 = new PurchaseOrder
                {
                    PONumber = "PO-2026-FS-101",
                    Supplier = "FaceStone Raw Materials & Silos",
                    OrderDate = DateTime.UtcNow.AddDays(-12),
                    Quantity = 300.000m,
                    PricePerTon = 1200.00m,
                    TotalValue = 300.000m * 1200.00m, // 360,000.00 EGP
                    InvoiceNumber = "FS-2026-INV-750",
                    Notes = "رصيد وتوريد مواد خام خرسانية ومواد إسمنتية معتمدة للصوامع",
                    CreatedAt = DateTime.UtcNow.AddDays(-12)
                };

                var po3 = new PurchaseOrder
                {
                    PONumber = "PO-2026-ST-001",
                    Supplier = "Misr Concrete Technologies",
                    OrderDate = DateTime.UtcNow.AddDays(-8),
                    Quantity = 150.000m,
                    PricePerTon = 1850.00m,
                    TotalValue = 150.000m * 1850.00m, // 277,500.00 EGP
                    InvoiceNumber = "FS-2026-INV-810",
                    Notes = "عقد توريد إضافات خرسانية فائقة الأداء للمشروعات التخصصية",
                    CreatedAt = DateTime.UtcNow.AddDays(-8)
                };

                var po4 = new PurchaseOrder
                {
                    PONumber = "PO-2026-TR-001",
                    Supplier = "Sinai Aggregates Company",
                    OrderDate = DateTime.UtcNow.AddDays(-7),
                    Quantity = 100.000m,
                    PricePerTon = 950.00m,
                    TotalValue = 100.000m * 950.00m, // 95,000.00 EGP
                    InvoiceNumber = "FS-2026-INV-815",
                    Notes = "ركام وسن خرساني متدرج قطاع سيناء والمنطقة الشرقية",
                    CreatedAt = DateTime.UtcNow.AddDays(-7)
                };

                var po5 = new PurchaseOrder
                {
                    PONumber = "po-01",
                    Supplier = "محجر السويس",
                    OrderDate = DateTime.UtcNow.AddDays(-5),
                    Quantity = 120.000m,
                    PricePerTon = 1350.00m,
                    TotalValue = 120.000m * 1350.00m, // 162,000.00 EGP
                    InvoiceNumber = null,
                    Notes = "أمر توريد تجريبي جاري استكمال مستندات الفاتورة",
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                };

                var po6 = new PurchaseOrder
                {
                    PONumber = "123",
                    Supplier = "orascom",
                    OrderDate = DateTime.UtcNow.AddDays(-3),
                    Quantity = 50.000m,
                    PricePerTon = 2100.00m,
                    TotalValue = 50.000m * 2100.00m, // 105,000.00 EGP
                    InvoiceNumber = null,
                    Notes = "خرسانة عالية الإجهاد HPC قيد الفحص المعملي",
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                };

                var po7 = new PurchaseOrder
                {
                    PONumber = "578",
                    Supplier = "orascom",
                    OrderDate = DateTime.UtcNow.AddDays(-2),
                    Quantity = 60.000m,
                    PricePerTon = 2050.00m,
                    TotalValue = 60.000m * 2050.00m, // 123,000.00 EGP
                    InvoiceNumber = "FS-2026-INV-902",
                    Notes = "إسمنت بورتلاندي 42.5N معتمد",
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                };

                await db.PurchaseOrders.AddRangeAsync(new[] { po1, po2, po3, po4, po5, po6, po7 }, cts.Token);
            }

            await db.SaveChangesAsync(cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DbInitializer completed with notice.");
        }
    }

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password + "_FaceStoneSalt_2026"));
        return Convert.ToBase64String(bytes);
    }
}
