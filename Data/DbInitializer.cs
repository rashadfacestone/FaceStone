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
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var dbName = db.Database.GetDbConnection().Database;
            logger.LogInformation("[DB] Connection test started for database: {DbName}", dbName);
            if (await db.Database.CanConnectAsync(cts.Token))
            {
                logger.LogInformation("[DB] Connection successful to database: {DbName}", dbName);
            }

            // 1. In production / hosted environments, apply pending EF Core migrations safely
            if (db.Database.IsSqlServer())
            {
                try
                {
                    var pendingMigrations = (await db.Database.GetPendingMigrationsAsync(cts.Token)).ToList();
                    if (pendingMigrations.Any())
                    {
                        logger.LogInformation("[DB] Applying {Count} pending EF Core migrations to {DbName}: {Migrations}",
                            pendingMigrations.Count, dbName, string.Join(", ", pendingMigrations));
                        await db.Database.MigrateAsync(cts.Token);
                        logger.LogInformation("[DB] EF Core migrations applied successfully.");
                    }
                    else
                    {
                        logger.LogInformation("[DB] Database is up to date with all EF Core migrations.");
                    }
                }
                catch (Exception exMig)
                {
                    logger.LogWarning(exMig, "[DB] Automatic migration check encountered a warning. Falling back to schema verifications.");
                }
            }

            try
            {
                await db.Database.EnsureCreatedAsync(cts.Token);
            }
            catch (Exception exEc)
            {
                logger.LogWarning(exEc, "[DB] EnsureCreated check skipped (tables may already exist).");
            }

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

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'Ikramiya')
        ALTER TABLE Transportation ADD Ikramiya DECIMAL(18,3) NOT NULL CONSTRAINT DF_Trans_Ikramiya DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'InvoiceAttachmentPath')
        ALTER TABLE Transportation ADD InvoiceAttachmentPath NVARCHAR(500) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'InvoiceFileName')
        ALTER TABLE Transportation ADD InvoiceFileName NVARCHAR(260) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'InvoiceFileType')
        ALTER TABLE Transportation ADD InvoiceFileType NVARCHAR(50) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'InvoiceFileSize')
        ALTER TABLE Transportation ADD InvoiceFileSize BIGINT NOT NULL CONSTRAINT DF_Trans_InvoiceFileSize DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'SubmittedBy')
        ALTER TABLE Transportation ADD SubmittedBy NVARCHAR(150) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'SubmittedByUserId')
        ALTER TABLE Transportation ADD SubmittedByUserId UNIQUEIDENTIFIER NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'PurchaseOrderId')
        ALTER TABLE Transportation ADD PurchaseOrderId INT NULL CONSTRAINT FK_Transportation_PurchaseOrders REFERENCES PurchaseOrders(Id) ON DELETE SET NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'CompanyName')
        ALTER TABLE Transportation ADD CompanyName NVARCHAR(200) NOT NULL CONSTRAINT DF_Trans_CompanyName DEFAULT '';

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'InternalWeight')
        ALTER TABLE Transportation ADD InternalWeight DECIMAL(18,3) NOT NULL CONSTRAINT DF_Trans_InternalWeight DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ExternalLoadedWeight')
        ALTER TABLE Transportation ADD ExternalLoadedWeight DECIMAL(18,3) NOT NULL CONSTRAINT DF_Trans_ExtLoaded DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ExternalEmptyWeight')
        ALTER TABLE Transportation ADD ExternalEmptyWeight DECIMAL(18,3) NOT NULL CONSTRAINT DF_Trans_ExtEmpty DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ExternalNetWeight')
        ALTER TABLE Transportation ADD ExternalNetWeight DECIMAL(18,3) NOT NULL CONSTRAINT DF_Trans_ExtNet DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'WasteQuantity')
        ALTER TABLE Transportation ADD WasteQuantity DECIMAL(18,3) NOT NULL CONSTRAINT DF_Trans_WasteQty DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'DriverPhone')
        ALTER TABLE Transportation ADD DriverPhone NVARCHAR(30) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'DepartureTime')
        ALTER TABLE Transportation ADD DepartureTime DATETIME2 NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ArrivalTime')
        ALTER TABLE Transportation ADD ArrivalTime DATETIME2 NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ShippingSupervisor')
        ALTER TABLE Transportation ADD ShippingSupervisor NVARCHAR(100) NOT NULL CONSTRAINT DF_Trans_ShipSup DEFAULT 'محمد ناجح';

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'DepartmentSupervisor')
        ALTER TABLE Transportation ADD DepartmentSupervisor NVARCHAR(100) NOT NULL CONSTRAINT DF_Trans_DeptSup DEFAULT 'ازهر محمد';

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'RecipientName')
        ALTER TABLE Transportation ADD RecipientName NVARCHAR(150) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'RecipientTitle')
        ALTER TABLE Transportation ADD RecipientTitle NVARCHAR(100) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ReceivedDate')
        ALTER TABLE Transportation ADD ReceivedDate DATETIME2 NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'InternalWeighbridgeCode')
        ALTER TABLE Transportation ADD InternalWeighbridgeCode NVARCHAR(100) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'InternalEmptyWeightTime')
        ALTER TABLE Transportation ADD InternalEmptyWeightTime DATETIME2 NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'InternalLoadedWeightTime')
        ALTER TABLE Transportation ADD InternalLoadedWeightTime DATETIME2 NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ExternalWeighbridgeCode')
        ALTER TABLE Transportation ADD ExternalWeighbridgeCode NVARCHAR(100) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ExternalEmptyWeightTime')
        ALTER TABLE Transportation ADD ExternalEmptyWeightTime DATETIME2 NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ExternalLoadedWeightTime')
        ALTER TABLE Transportation ADD ExternalLoadedWeightTime DATETIME2 NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ShippingDurationMinutes')
        ALTER TABLE Transportation ADD ShippingDurationMinutes INT NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'WeighbridgeAttachmentPath')
        ALTER TABLE Transportation ADD WeighbridgeAttachmentPath NVARCHAR(500) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'WeighbridgeFileName')
        ALTER TABLE Transportation ADD WeighbridgeFileName NVARCHAR(260) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'WeighbridgeFileType')
        ALTER TABLE Transportation ADD WeighbridgeFileType NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'WeighbridgeFileSize')
        ALTER TABLE Transportation ADD WeighbridgeFileSize BIGINT NOT NULL CONSTRAINT DF_Trans_WbFileSize DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'DailySupplyAttachmentPath')
        ALTER TABLE Transportation ADD DailySupplyAttachmentPath NVARCHAR(500) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'DailySupplyFileName')
        ALTER TABLE Transportation ADD DailySupplyFileName NVARCHAR(260) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'DailySupplyFileType')
        ALTER TABLE Transportation ADD DailySupplyFileType NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'DailySupplyFileSize')
        ALTER TABLE Transportation ADD DailySupplyFileSize BIGINT NOT NULL CONSTRAINT DF_Trans_DsFileSize DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'BillOfLadingAttachmentPath')
        ALTER TABLE Transportation ADD BillOfLadingAttachmentPath NVARCHAR(500) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'BillOfLadingFileName')
        ALTER TABLE Transportation ADD BillOfLadingFileName NVARCHAR(260) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'BillOfLadingFileType')
        ALTER TABLE Transportation ADD BillOfLadingFileType NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'BillOfLadingFileSize')
        ALTER TABLE Transportation ADD BillOfLadingFileSize BIGINT NOT NULL CONSTRAINT DF_Trans_BolFileSize DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'CustodyAmount')
        ALTER TABLE Transportation ADD CustodyAmount DECIMAL(18,3) NOT NULL CONSTRAINT DF_Trans_CustodyAmount DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ApprovalStatus')
        ALTER TABLE Transportation ADD ApprovalStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_Trans_ApprovalStatus DEFAULT 'APPROVED';

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ReviewedBy')
        ALTER TABLE Transportation ADD ReviewedBy NVARCHAR(150) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ReviewedByUserId')
        ALTER TABLE Transportation ADD ReviewedByUserId UNIQUEIDENTIFIER NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ReviewedAt')
        ALTER TABLE Transportation ADD ReviewedAt DATETIME2 NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'RejectionReason')
        ALTER TABLE Transportation ADD RejectionReason NVARCHAR(1000) NULL;

    IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Transportation_TransactionNumber' AND object_id = OBJECT_ID('Transportation'))
        DROP INDEX IX_Transportation_TransactionNumber ON Transportation;

    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Transportation_PONumber_TransactionNumber' AND object_id = OBJECT_ID('Transportation'))
        CREATE UNIQUE INDEX IX_Transportation_PONumber_TransactionNumber ON Transportation(PONumber, TransactionNumber);
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
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'Material')
        ALTER TABLE StorageTransactions ADD Material NVARCHAR(100) NOT NULL CONSTRAINT DF_StorageTx_Mat DEFAULT 'powder';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'Action')
        ALTER TABLE StorageTransactions ADD Action NVARCHAR(50) NOT NULL CONSTRAINT DF_StorageTx_Act DEFAULT 'Import';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'IsExportRelatedToPO')
        ALTER TABLE StorageTransactions ADD IsExportRelatedToPO BIT NOT NULL CONSTRAINT DF_StorageTx_IsExportPO DEFAULT 0;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'PurchaseOrderId')
        ALTER TABLE StorageTransactions ADD PurchaseOrderId INT NULL CONSTRAINT FK_StorageTransactions_PurchaseOrders REFERENCES PurchaseOrders(Id) ON DELETE SET NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'InternalWeighbridgeReference')
        ALTER TABLE StorageTransactions ADD InternalWeighbridgeReference NVARCHAR(100) NOT NULL CONSTRAINT DF_StorageTx_IntWbRef DEFAULT '';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'InternalLoadedWeight')
        ALTER TABLE StorageTransactions ADD InternalLoadedWeight DECIMAL(18,3) NOT NULL CONSTRAINT DF_StorageTx_IntLoaded DEFAULT 0;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'InternalEmptyWeight')
        ALTER TABLE StorageTransactions ADD InternalEmptyWeight DECIMAL(18,3) NOT NULL CONSTRAINT DF_StorageTx_IntEmpty DEFAULT 0;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'InternalNetWeight')
        ALTER TABLE StorageTransactions ADD InternalNetWeight DECIMAL(18,3) NOT NULL CONSTRAINT DF_StorageTx_IntNet DEFAULT 0;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'InternalEmptyWeightTime')
        ALTER TABLE StorageTransactions ADD InternalEmptyWeightTime DATETIME2 NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'InternalLoadedWeightTime')
        ALTER TABLE StorageTransactions ADD InternalLoadedWeightTime DATETIME2 NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'HasExternalWeighbridge')
        ALTER TABLE StorageTransactions ADD HasExternalWeighbridge BIT NOT NULL CONSTRAINT DF_StorageTx_HasExtWb DEFAULT 0;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'ExternalWeighbridgeReference')
        ALTER TABLE StorageTransactions ADD ExternalWeighbridgeReference NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'ExternalLoadedWeight')
        ALTER TABLE StorageTransactions ADD ExternalLoadedWeight DECIMAL(18,3) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'ExternalEmptyWeight')
        ALTER TABLE StorageTransactions ADD ExternalEmptyWeight DECIMAL(18,3) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'ExternalNetWeight')
        ALTER TABLE StorageTransactions ADD ExternalNetWeight DECIMAL(18,3) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'ExternalEmptyWeightTime')
        ALTER TABLE StorageTransactions ADD ExternalEmptyWeightTime DATETIME2 NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'ExternalLoadedWeightTime')
        ALTER TABLE StorageTransactions ADD ExternalLoadedWeightTime DATETIME2 NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'WeighbridgeAttachmentPath')
        ALTER TABLE StorageTransactions ADD WeighbridgeAttachmentPath NVARCHAR(500) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'WeighbridgeFileName')
        ALTER TABLE StorageTransactions ADD WeighbridgeFileName NVARCHAR(260) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'WeighbridgeFileType')
        ALTER TABLE StorageTransactions ADD WeighbridgeFileType NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'WeighbridgeFileSize')
        ALTER TABLE StorageTransactions ADD WeighbridgeFileSize BIGINT NOT NULL CONSTRAINT DF_StorageTx_WbFileSize DEFAULT 0;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'CreatedBy')
        ALTER TABLE StorageTransactions ADD CreatedBy NVARCHAR(150) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'CreatedByUserId')
        ALTER TABLE StorageTransactions ADD CreatedByUserId UNIQUEIDENTIFIER NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'ApprovalStatus')
        ALTER TABLE StorageTransactions ADD ApprovalStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_StorageTx_ApprovalStatus DEFAULT 'APPROVED';

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'ReviewedBy')
        ALTER TABLE StorageTransactions ADD ReviewedBy NVARCHAR(150) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'ReviewedByUserId')
        ALTER TABLE StorageTransactions ADD ReviewedByUserId UNIQUEIDENTIFIER NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'ReviewedAt')
        ALTER TABLE StorageTransactions ADD ReviewedAt DATETIME2 NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageTransactions') AND name = 'RejectionReason')
        ALTER TABLE StorageTransactions ADD RejectionReason NVARCHAR(1000) NULL;
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'StorageOperations')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageOperations') AND name = 'WeighbridgeAttachmentPath')
        ALTER TABLE StorageOperations ADD WeighbridgeAttachmentPath NVARCHAR(500) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageOperations') AND name = 'WeighbridgeFileName')
        ALTER TABLE StorageOperations ADD WeighbridgeFileName NVARCHAR(260) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageOperations') AND name = 'WeighbridgeFileType')
        ALTER TABLE StorageOperations ADD WeighbridgeFileType NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageOperations') AND name = 'WeighbridgeFileSize')
        ALTER TABLE StorageOperations ADD WeighbridgeFileSize BIGINT NOT NULL CONSTRAINT DF_StorageOp_WbFileSize DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageOperations') AND name = 'QocAttachmentPath')
        ALTER TABLE StorageOperations ADD QocAttachmentPath NVARCHAR(500) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageOperations') AND name = 'QocFileName')
        ALTER TABLE StorageOperations ADD QocFileName NVARCHAR(260) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageOperations') AND name = 'QocFileType')
        ALTER TABLE StorageOperations ADD QocFileType NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageOperations') AND name = 'QocFileSize')
        ALTER TABLE StorageOperations ADD QocFileSize BIGINT NOT NULL CONSTRAINT DF_StorageOp_QocFileSize DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageOperations') AND name = 'DocumentAttachmentPath')
        ALTER TABLE StorageOperations ADD DocumentAttachmentPath NVARCHAR(500) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageOperations') AND name = 'DocumentFileName')
        ALTER TABLE StorageOperations ADD DocumentFileName NVARCHAR(260) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageOperations') AND name = 'DocumentFileType')
        ALTER TABLE StorageOperations ADD DocumentFileType NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageOperations') AND name = 'DocumentFileSize')
        ALTER TABLE StorageOperations ADD DocumentFileSize BIGINT NOT NULL CONSTRAINT DF_StorageOp_DocFileSize DEFAULT 0;
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'FormApprovalHistories')
    AND EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FormApprovalHistories') AND name = 'TransactionOrMovementNumber' AND system_type_id = 56)
BEGIN
    ALTER TABLE FormApprovalHistories ALTER COLUMN TransactionOrMovementNumber NVARCHAR(100) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FormApprovalHistories')
BEGIN
    CREATE TABLE FormApprovalHistories (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FormType NVARCHAR(50) NOT NULL,
        FormId INT NOT NULL,
        PONumber NVARCHAR(100) NULL,
        TransactionOrMovementNumber NVARCHAR(100) NULL,
        PreviousStatus NVARCHAR(50) NOT NULL,
        NewStatus NVARCHAR(50) NOT NULL,
        Action NVARCHAR(50) NOT NULL,
        ActorName NVARCHAR(150) NOT NULL,
        ActorUserId UNIQUEIDENTIFIER NULL,
        ActorRole NVARCHAR(100) NULL,
        Comment NVARCHAR(1000) NULL,
        Timestamp DATETIME2 NOT NULL CONSTRAINT DF_FormApprHist_Timestamp DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_FormApprovalHistories_FormType_FormId ON FormApprovalHistories(FormType, FormId);
    CREATE INDEX IX_FormApprovalHistories_Timestamp ON FormApprovalHistories(Timestamp DESC);
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

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RawMaterials')
BEGIN
    CREATE TABLE RawMaterials (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        InitialQuantity DECIMAL(18,3) NOT NULL CONSTRAINT DF_RawMat_InitQty DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_RawMat_CreatedAt DEFAULT GETUTCDATE(),
        CreatedBy NVARCHAR(150) NULL,
        CreatedByUserId UNIQUEIDENTIFIER NULL
    );
    CREATE UNIQUE INDEX IX_RawMaterials_Name ON RawMaterials(Name);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StorageItemTransactions')
BEGIN
    CREATE TABLE StorageItemTransactions (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        RawMaterialId INT NOT NULL CONSTRAINT FK_StorageItemTx_RawMat REFERENCES RawMaterials(Id) ON DELETE CASCADE,
        TransactionDate DATETIME2 NOT NULL,
        TransactionType NVARCHAR(50) NOT NULL CONSTRAINT DF_StorageItemTx_Type DEFAULT 'Incoming',
        Description NVARCHAR(100) NOT NULL,
        Quantity DECIMAL(18,3) NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL CONSTRAINT DF_StorageItemTx_UnitPrice DEFAULT 0,
        TotalValue DECIMAL(18,2) NOT NULL CONSTRAINT DF_StorageItemTx_TotalValue DEFAULT 0,
        DestinationType NVARCHAR(50) NULL,
        Destination NVARCHAR(200) NULL,
        SourceTransportationId INT NULL CONSTRAINT FK_StorageItemTx_Transportation REFERENCES Transportation(Id) ON DELETE SET NULL,
        SourceTransportationNumber INT NULL,
        RunningQuantityBalance DECIMAL(18,3) NOT NULL CONSTRAINT DF_StorageItemTx_RunQty DEFAULT 0,
        RunningValueBalance DECIMAL(18,2) NOT NULL CONSTRAINT DF_StorageItemTx_RunVal DEFAULT 0,
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_StorageItemTx_CreatedAt DEFAULT GETUTCDATE(),
        CreatedBy NVARCHAR(150) NULL,
        CreatedByUserId UNIQUEIDENTIFIER NULL
    );
    CREATE INDEX IX_StorageItemTransactions_RawMaterialId ON StorageItemTransactions(RawMaterialId);
    CREATE INDEX IX_StorageItemTransactions_TransactionDate ON StorageItemTransactions(TransactionDate);
    CREATE UNIQUE INDEX IX_StorageItemTransactions_SourceTransportationId ON StorageItemTransactions(SourceTransportationId) WHERE SourceTransportationId IS NOT NULL;
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseOrders')
BEGIN
    CREATE TABLE PurchaseOrders (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PONumber NVARCHAR(100) NOT NULL,
        Supplier NVARCHAR(200) NOT NULL,
        OrderDate DATETIME2 NOT NULL,
        Quantity DECIMAL(18,3) NOT NULL,
        RemainingQuantity DECIMAL(18,3) NOT NULL CONSTRAINT DF_PO_RemQty DEFAULT 0,
        Destination NVARCHAR(200) NOT NULL CONSTRAINT DF_PO_Dest DEFAULT 'Cairo Factory',
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
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'Destination')
        ALTER TABLE PurchaseOrders ADD Destination NVARCHAR(200) NOT NULL CONSTRAINT DF_PO_Dest DEFAULT 'Cairo Factory';

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'RemainingQuantity')
    BEGIN
        ALTER TABLE PurchaseOrders ADD RemainingQuantity DECIMAL(18,3) NOT NULL CONSTRAINT DF_PO_RemQty DEFAULT 0;
        EXEC('UPDATE PurchaseOrders SET RemainingQuantity = Quantity WHERE RemainingQuantity = 0;');
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'TonPrice')
    BEGIN
        ALTER TABLE PurchaseOrders ADD TonPrice DECIMAL(18,3) NOT NULL CONSTRAINT DF_PO_TonPrice DEFAULT 0;
        EXEC('UPDATE PurchaseOrders SET TonPrice = PricePerTon WHERE TonPrice = 0;');
    END

    -- Ensure TonPrice is synced with PricePerTon
    EXEC('UPDATE PurchaseOrders SET TonPrice = PricePerTon WHERE TonPrice = 0 AND PricePerTon > 0;');

    -- Safely backfill PurchaseOrderId in Transportation
    EXEC('UPDATE t SET t.PurchaseOrderId = po.Id FROM Transportation t INNER JOIN PurchaseOrders po ON t.PONumber = po.PONumber WHERE t.PurchaseOrderId IS NULL;');

    -- Safely backfill PurchaseOrderId in StorageTransactions for PO-linked records
    EXEC('UPDATE s SET s.PurchaseOrderId = po.Id FROM StorageTransactions s INNER JOIN PurchaseOrders po ON s.PONumber = po.PONumber WHERE s.PurchaseOrderId IS NULL AND s.PONumber IS NOT NULL AND s.PONumber <> '''';');

    -- Ensure existing POs have realistic remaining quantities if still 0
    EXEC('UPDATE PurchaseOrders SET RemainingQuantity = Quantity WHERE RemainingQuantity <= 0;');

    -- Backfill destination from Transportation records where applicable
    EXEC('UPDATE po SET po.Destination = t.Destination FROM PurchaseOrders po INNER JOIN Transportation t ON po.PONumber = t.PONumber WHERE (po.Destination = ''Cairo Factory'' OR po.Destination IS NULL OR po.Destination = '''') AND t.Destination IS NOT NULL AND t.Destination <> '''';');

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'PaymentStatus')
    BEGIN
        ALTER TABLE PurchaseOrders ADD PaymentStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_PurchaseOrders_PaymentStatus DEFAULT N'غير مدفوع';
        EXEC('UPDATE PurchaseOrders SET PaymentStatus = N''غير مدفوع'' WHERE PaymentStatus IS NULL;');
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'PaymentStatusUpdatedAt')
        ALTER TABLE PurchaseOrders ADD PaymentStatusUpdatedAt DATETIME2 NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'PaymentStatusUpdatedBy')
        ALTER TABLE PurchaseOrders ADD PaymentStatusUpdatedBy NVARCHAR(150) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseOrderPaymentAudits')
BEGIN
    CREATE TABLE PurchaseOrderPaymentAudits (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PurchaseOrderId INT NULL,
        PONumber NVARCHAR(100) NOT NULL,
        PreviousStatus NVARCHAR(50) NOT NULL,
        NewStatus NVARCHAR(50) NOT NULL,
        ChangedBy NVARCHAR(150) NOT NULL,
        ChangedAt DATETIME2 NOT NULL CONSTRAINT DF_POPA_ChangedAt DEFAULT GETUTCDATE(),
        Notes NVARCHAR(500) NULL
    );
    CREATE INDEX IX_POPA_PONumber ON PurchaseOrderPaymentAudits(PONumber);
    CREATE INDEX IX_POPA_PurchaseOrderId ON PurchaseOrderPaymentAudits(PurchaseOrderId);
    CREATE INDEX IX_POPA_ChangedAt ON PurchaseOrderPaymentAudits(ChangedAt);
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

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BankTransactions')
BEGIN
    CREATE TABLE BankTransactions (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TransactionDate DATETIME2 NOT NULL,
        TransactionType NVARCHAR(50) NOT NULL,
        Direction NVARCHAR(20) NOT NULL CONSTRAINT DF_BT_Dir DEFAULT 'In',
        Amount DECIMAL(18,2) NOT NULL,
        BankName NVARCHAR(100) NOT NULL,
        ReferenceNumber NVARCHAR(100) NULL,
        Description NVARCHAR(500) NOT NULL,
        CreatedBy NVARCHAR(150) NOT NULL,
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        Notes NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_BT_CreatedAt DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_BankTransactions_Date ON BankTransactions(TransactionDate DESC);
    CREATE INDEX IX_BankTransactions_BankName ON BankTransactions(BankName);
    CREATE INDEX IX_BankTransactions_Direction ON BankTransactions(Direction);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Expenses')
BEGIN
    CREATE TABLE Expenses (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ExpenseDate DATETIME2 NOT NULL,
        Category NVARCHAR(100) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        Title NVARCHAR(300) NOT NULL,
        Beneficiary NVARCHAR(200) NOT NULL,
        ReferenceNumber NVARCHAR(100) NULL,
        PaymentMethod NVARCHAR(50) NOT NULL CONSTRAINT DF_Exp_PayMethod DEFAULT 'Bank Transfer',
        CreatedBy NVARCHAR(150) NOT NULL,
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        Notes NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Exp_CreatedAt DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_Expenses_Date ON Expenses(ExpenseDate DESC);
    CREATE INDEX IX_Expenses_Category ON Expenses(Category);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmployeeFinancialRecords')
BEGIN
    CREATE TABLE EmployeeFinancialRecords (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_EmpFin_Users REFERENCES Users(Id) ON DELETE CASCADE,
        BasicSalary DECIMAL(18,2) NOT NULL CONSTRAINT DF_EFR_Basic DEFAULT 0,
        Allowances DECIMAL(18,2) NOT NULL CONSTRAINT DF_EFR_Allow DEFAULT 0,
        InsuranceDeductions DECIMAL(18,2) NOT NULL CONSTRAINT DF_EFR_Deduct DEFAULT 0,
        NetSalary DECIMAL(18,2) NOT NULL CONSTRAINT DF_EFR_Net DEFAULT 0,
        EffectiveFrom DATETIME2 NOT NULL CONSTRAINT DF_EFR_EffFrom DEFAULT GETUTCDATE(),
        PaymentMethod NVARCHAR(50) NOT NULL CONSTRAINT DF_EFR_PayMethod DEFAULT 'Bank Transfer',
        BankAccountNumber NVARCHAR(100) NULL,
        Notes NVARCHAR(500) NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_EFR_UpdatedAt DEFAULT GETUTCDATE()
    );
    CREATE UNIQUE INDEX IX_EmployeeFinancialRecords_UserId ON EmployeeFinancialRecords(UserId);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmployeeLendings')
BEGIN
    CREATE TABLE EmployeeLendings (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_EmpLending_Users REFERENCES Users(Id) ON DELETE CASCADE,
        LendingAmount DECIMAL(18,2) NOT NULL,
        PaidAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_EL_Paid DEFAULT 0,
        RemainingAmount DECIMAL(18,2) NOT NULL,
        RequestDate DATETIME2 NOT NULL,
        ApprovalStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_EL_Status DEFAULT 'معتمد',
        ApprovedBy NVARCHAR(150) NULL,
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_EL_CreatedAt DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_EmployeeLendings_UserId ON EmployeeLendings(UserId);
    CREATE INDEX IX_EmployeeLendings_RequestDate ON EmployeeLendings(RequestDate DESC);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AttendanceRecords')
BEGIN
    CREATE TABLE AttendanceRecords (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Attendance_Users REFERENCES Users(Id) ON DELETE CASCADE,
        Date DATETIME2 NOT NULL,
        Status NVARCHAR(50) NOT NULL CONSTRAINT DF_Att_Status DEFAULT 'Present',
        CheckInTime DATETIME2 NULL,
        CheckOutTime DATETIME2 NULL,
        Notes NVARCHAR(500) NULL
    );
    CREATE INDEX IX_AttendanceRecords_UserId ON AttendanceRecords(UserId);
    CREATE INDEX IX_AttendanceRecords_Date ON AttendanceRecords(Date DESC);
    CREATE UNIQUE INDEX IX_AttendanceRecords_UserDate ON AttendanceRecords(UserId, Date);
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

                    // Step 4: Attendance department tables and columns
                    await db.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AttendanceSubmissions')
BEGIN
    CREATE TABLE AttendanceSubmissions (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AttendanceDate DATETIME2 NOT NULL,
        SubmittedByUserId UNIQUEIDENTIFIER NULL,
        SubmittedBy NVARCHAR(150) NOT NULL,
        SubmittedAt DATETIME2 NOT NULL CONSTRAINT DF_AttSub_SubmittedAt DEFAULT GETUTCDATE(),
        ApprovalStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_AttSub_ApprovalStatus DEFAULT 'PENDING',
        ReviewedByUserId UNIQUEIDENTIFIER NULL,
        ReviewedBy NVARCHAR(150) NULL,
        ReviewedAt DATETIME2 NULL,
        RejectionReason NVARCHAR(1000) NULL,
        Notes NVARCHAR(1000) NULL,
        TotalEmployeesCount INT NOT NULL CONSTRAINT DF_AttSub_Total DEFAULT 0,
        PresentCount INT NOT NULL CONSTRAINT DF_AttSub_Present DEFAULT 0,
        AbsentCount INT NOT NULL CONSTRAINT DF_AttSub_Absent DEFAULT 0
    );
    CREATE INDEX IX_AttSub_Date ON AttendanceSubmissions(AttendanceDate);
    CREATE INDEX IX_AttSub_ApprovalStatus ON AttendanceSubmissions(ApprovalStatus);
    CREATE INDEX IX_AttSub_SubmittedAt ON AttendanceSubmissions(SubmittedAt DESC);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AttendanceAttachments')
BEGIN
    CREATE TABLE AttendanceAttachments (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AttendanceSubmissionId INT NOT NULL CONSTRAINT FK_AttAtt_Sub REFERENCES AttendanceSubmissions(Id) ON DELETE CASCADE,
        FileName NVARCHAR(255) NOT NULL,
        OriginalFileName NVARCHAR(255) NOT NULL,
        FilePath NVARCHAR(500) NOT NULL,
        FileType NVARCHAR(100) NOT NULL,
        FileSize BIGINT NOT NULL CONSTRAINT DF_AttAtt_FileSize DEFAULT 0,
        UploadedAt DATETIME2 NOT NULL CONSTRAINT DF_AttAtt_UploadedAt DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_AttAtt_SubId ON AttendanceAttachments(AttendanceSubmissionId);
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AttendanceRecords')
BEGIN
    -- Drop foreign key constraint on Users if present to allow isolated fake records
    IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Attendance_Users')
        ALTER TABLE AttendanceRecords DROP CONSTRAINT FK_Attendance_Users;
    IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AttRec_User')
        ALTER TABLE AttendanceRecords DROP CONSTRAINT FK_AttRec_User;
    IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AttendanceRecords_Users_UserId')
        ALTER TABLE AttendanceRecords DROP CONSTRAINT FK_AttendanceRecords_Users_UserId;

    -- Make UserId nullable
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'UserId')
        ALTER TABLE AttendanceRecords ALTER COLUMN UserId UNIQUEIDENTIFIER NULL;

    -- Drop unique index on (UserId, Date) if exists so fake employees / nulls do not conflict
    IF EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'IX_AttendanceRecords_UserDate')
        DROP INDEX IX_AttendanceRecords_UserDate ON AttendanceRecords;
    IF EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'IX_AttRec_UserId_Date')
        DROP INDEX IX_AttRec_UserId_Date ON AttendanceRecords;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'EmployeeName')
        ALTER TABLE AttendanceRecords ADD EmployeeName NVARCHAR(150) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'EmployeeRole')
        ALTER TABLE AttendanceRecords ADD EmployeeRole NVARCHAR(100) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'IsTestData')
        ALTER TABLE AttendanceRecords ADD IsTestData BIT NOT NULL CONSTRAINT DF_AttRec_IsTest DEFAULT 1;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'AttendanceSubmissionId')
    BEGIN
        ALTER TABLE AttendanceRecords ADD AttendanceSubmissionId INT NULL CONSTRAINT FK_AttRec_Sub REFERENCES AttendanceSubmissions(Id) ON DELETE CASCADE;
        CREATE INDEX IX_AttRec_SubId ON AttendanceRecords(AttendanceSubmissionId);
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'ApprovalStatus')
    BEGIN
        ALTER TABLE AttendanceRecords ADD ApprovalStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_AttRec_ApprovalStatus DEFAULT 'PENDING';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'RejectionReason')
    BEGIN
        ALTER TABLE AttendanceRecords ADD RejectionReason NVARCHAR(1000) NULL;
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'ReviewedBy')
    BEGIN
        ALTER TABLE AttendanceRecords ADD ReviewedBy NVARCHAR(150) NULL;
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'ReviewedByUserId')
    BEGIN
        ALTER TABLE AttendanceRecords ADD ReviewedByUserId UNIQUEIDENTIFIER NULL;
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'ReviewedAt')
    BEGIN
        ALTER TABLE AttendanceRecords ADD ReviewedAt DATETIME2 NULL;
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'CreatedAt')
    BEGIN
        ALTER TABLE AttendanceRecords ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AttRec_CreatedAt DEFAULT GETUTCDATE();
    END
END
ELSE
BEGIN
    CREATE TABLE AttendanceRecords (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NULL,
        EmployeeName NVARCHAR(150) NULL,
        EmployeeRole NVARCHAR(100) NULL,
        IsTestData BIT NOT NULL CONSTRAINT DF_AttRec_IsTest2 DEFAULT 1,
        Date DATETIME2 NOT NULL,
        Status NVARCHAR(50) NOT NULL CONSTRAINT DF_AttRec_Status DEFAULT 'Present',
        CheckInTime DATETIME2 NULL,
        CheckOutTime DATETIME2 NULL,
        Notes NVARCHAR(500) NULL,
        AttendanceSubmissionId INT NULL CONSTRAINT FK_AttRec_Sub2 REFERENCES AttendanceSubmissions(Id) ON DELETE CASCADE,
        ApprovalStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_AttRec_ApprovalStatus DEFAULT 'PENDING',
        RejectionReason NVARCHAR(1000) NULL,
        ReviewedBy NVARCHAR(150) NULL,
        ReviewedByUserId UNIQUEIDENTIFIER NULL,
        ReviewedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AttRec_CreatedAt2 DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_AttRec_UserId ON AttendanceRecords(UserId);
    CREATE INDEX IX_AttRec_Date ON AttendanceRecords(Date);
    CREATE INDEX IX_AttRec_SubId ON AttendanceRecords(AttendanceSubmissionId);
END

-- Laboratory Department Resilient Schema
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LaboratoryProductionSequences')
BEGIN
    CREATE TABLE LaboratoryProductionSequences (
        SequenceName NVARCHAR(50) NOT NULL PRIMARY KEY,
        NextValue INT NOT NULL CONSTRAINT DF_LabSeq_NextValue DEFAULT 1
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LaboratoryUserSampleSequences')
BEGIN
    CREATE TABLE LaboratoryUserSampleSequences (
        UserId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        NextSampleNumber INT NOT NULL CONSTRAINT DF_LabUserSeq_Next DEFAULT 1,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_LabUserSeq_Updated DEFAULT GETUTCDATE()
    );
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StorageOperations') AND name = 'ProductionReferenceNumber')
BEGIN
    ALTER TABLE StorageOperations ADD ProductionReferenceNumber INT NULL;
    CREATE INDEX IX_StorageOperations_ProductionReferenceNumber ON StorageOperations(ProductionReferenceNumber);
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProductionWorkInProcess') AND name = 'ProductionReferenceNumber')
BEGIN
    ALTER TABLE ProductionWorkInProcess ADD ProductionReferenceNumber INT NULL;
    CREATE INDEX IX_ProductionWorkInProcess_ProductionReferenceNumber ON ProductionWorkInProcess(ProductionReferenceNumber);
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Transportation') AND name = 'ProductionReferenceNumber')
BEGIN
    ALTER TABLE Transportation ADD ProductionReferenceNumber INT NULL;
    CREATE INDEX IX_Transportation_ProductionReferenceNumber ON Transportation(ProductionReferenceNumber);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LaboratoryRawMaterialReports')
BEGIN
    CREATE TABLE LaboratoryRawMaterialReports (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductionReferenceNumber INT NOT NULL,
        SampleNumber INT NOT NULL CONSTRAINT DF_LabRaw_SampleNumber DEFAULT 0,
        SampleName NVARCHAR(250) NOT NULL CONSTRAINT DF_LabRaw_SampleName DEFAULT 'سيليكا باودر (Micro Silica Powder)',
        SampleDescription NVARCHAR(500) NULL,
        BatchNumber NVARCHAR(100) NULL,
        SampleReceivedDate DATETIME2 NULL,
        ReportIssueDate DATETIME2 NULL,
        LabManagerName NVARCHAR(150) NULL,
        LabManagerSignature NVARCHAR(150) NULL,
        LabManagerDate DATETIME2 NULL,
        ComplyStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_LabRaw_ComplyStatus DEFAULT 'COMPLY',
        LaboratorySection NVARCHAR(20) NOT NULL,
        AnalysisDateTime DATETIME2 NOT NULL,
        RawMaterialDescription NVARCHAR(250) NOT NULL,
        RawMaterialId INT NULL CONSTRAINT FK_LabRaw_RawMat REFERENCES RawMaterials(Id) ON DELETE SET NULL,
        SupplierName NVARCHAR(200) NULL,
        SourceTransportationId INT NULL CONSTRAINT FK_LabRaw_Trans REFERENCES Transportation(Id) ON DELETE SET NULL,
        TransportationTransactionNumber INT NULL,
        PurchaseOrderId INT NULL CONSTRAINT FK_LabRaw_PO REFERENCES PurchaseOrders(Id) ON DELETE SET NULL,
        PONumber NVARCHAR(100) NULL,
        ReceivedQuantity DECIMAL(18,3) NOT NULL,
        AcceptedQuantity DECIMAL(18,3) NOT NULL,
        RejectedQuantity DECIMAL(18,3) NOT NULL,
        AnalysisResultsJson NVARCHAR(MAX) NULL,
        InspectionDecision NVARCHAR(50) NOT NULL,
        Notes NVARCHAR(MAX) NULL,
        AttachmentPath NVARCHAR(MAX) NULL,
        AttachmentFileName NVARCHAR(MAX) NULL,
        AttachmentFileType NVARCHAR(MAX) NULL,
        AttachmentFileSize BIGINT NOT NULL CONSTRAINT DF_LabRaw_AttSize DEFAULT 0,
        ResponsibleEmployee NVARCHAR(150) NOT NULL,
        ResponsibleUserId UNIQUEIDENTIFIER NULL,
        ApprovalStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_LabRaw_ApprovalStatus DEFAULT 'PENDING',
        ReviewedBy NVARCHAR(150) NULL,
        ReviewedByUserId UNIQUEIDENTIFIER NULL,
        ReviewedAt DATETIME2 NULL,
        RejectionReason NVARCHAR(500) NULL,
        IsStorageReceived BIT NOT NULL CONSTRAINT DF_LabRaw_IsStorageReceived DEFAULT 0,
        StorageReceivedAt DATETIME2 NULL,
        StorageOperationId INT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_LabRaw_CreatedAt DEFAULT GETUTCDATE(),
        CreatedBy NVARCHAR(150) NOT NULL
    );
    CREATE INDEX IX_LaboratoryRawMaterialReports_ProductionReferenceNumber ON LaboratoryRawMaterialReports(ProductionReferenceNumber);
    CREATE UNIQUE INDEX IX_LaboratoryRawMaterialReports_ResponsibleUserId_SampleNumber ON LaboratoryRawMaterialReports(ResponsibleUserId, SampleNumber) WHERE ResponsibleUserId IS NOT NULL AND SampleNumber > 0;
    CREATE INDEX IX_LaboratoryRawMaterialReports_SampleNumber ON LaboratoryRawMaterialReports(SampleNumber);
    CREATE INDEX IX_LaboratoryRawMaterialReports_BatchNumber ON LaboratoryRawMaterialReports(BatchNumber);
    CREATE INDEX IX_LaboratoryRawMaterialReports_ComplyStatus ON LaboratoryRawMaterialReports(ComplyStatus);
    CREATE INDEX IX_LaboratoryRawMaterialReports_LaboratorySection ON LaboratoryRawMaterialReports(LaboratorySection);
    CREATE INDEX IX_LaboratoryRawMaterialReports_ApprovalStatus ON LaboratoryRawMaterialReports(ApprovalStatus);
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LaboratoryRawMaterialReports') AND name = 'SampleNumber')
        ALTER TABLE LaboratoryRawMaterialReports ADD SampleNumber INT NOT NULL CONSTRAINT DF_LabRaw_SampleNumber DEFAULT 0;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LaboratoryRawMaterialReports') AND name = 'SampleName')
        ALTER TABLE LaboratoryRawMaterialReports ADD SampleName NVARCHAR(250) NOT NULL CONSTRAINT DF_LabRaw_SampleName DEFAULT 'سيليكا باودر (Micro Silica Powder)';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LaboratoryRawMaterialReports') AND name = 'SampleDescription')
        ALTER TABLE LaboratoryRawMaterialReports ADD SampleDescription NVARCHAR(500) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LaboratoryRawMaterialReports') AND name = 'BatchNumber')
        ALTER TABLE LaboratoryRawMaterialReports ADD BatchNumber NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LaboratoryRawMaterialReports') AND name = 'SampleReceivedDate')
        ALTER TABLE LaboratoryRawMaterialReports ADD SampleReceivedDate DATETIME2 NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LaboratoryRawMaterialReports') AND name = 'ReportIssueDate')
        ALTER TABLE LaboratoryRawMaterialReports ADD ReportIssueDate DATETIME2 NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LaboratoryRawMaterialReports') AND name = 'LabManagerName')
        ALTER TABLE LaboratoryRawMaterialReports ADD LabManagerName NVARCHAR(150) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LaboratoryRawMaterialReports') AND name = 'LabManagerSignature')
        ALTER TABLE LaboratoryRawMaterialReports ADD LabManagerSignature NVARCHAR(150) NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LaboratoryRawMaterialReports') AND name = 'LabManagerDate')
        ALTER TABLE LaboratoryRawMaterialReports ADD LabManagerDate DATETIME2 NULL;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LaboratoryRawMaterialReports') AND name = 'ComplyStatus')
        ALTER TABLE LaboratoryRawMaterialReports ADD ComplyStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_LabRaw_ComplyStatus DEFAULT 'COMPLY';

    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LaboratoryRawMaterialReports_ResponsibleUserId_SampleNumber')
    BEGIN
        CREATE UNIQUE INDEX IX_LaboratoryRawMaterialReports_ResponsibleUserId_SampleNumber 
        ON LaboratoryRawMaterialReports(ResponsibleUserId, SampleNumber) 
        WHERE ResponsibleUserId IS NOT NULL AND SampleNumber > 0;
    END
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LaboratoryInProcessReports')
BEGIN
    CREATE TABLE LaboratoryInProcessReports (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductionReferenceNumber INT NOT NULL,
        RawMaterialReportId INT NULL CONSTRAINT FK_LabInProc_RawReport REFERENCES LaboratoryRawMaterialReports(Id) ON DELETE SET NULL,
        ProductionBatchNumber NVARCHAR(100) NOT NULL,
        ProductionWorkInProcessId INT NULL CONSTRAINT FK_LabInProc_WIP REFERENCES ProductionWorkInProcess(Id) ON DELETE SET NULL,
        LaboratorySection NVARCHAR(20) NOT NULL,
        ProductName NVARCHAR(200) NOT NULL,
        TestDateTime DATETIME2 NOT NULL,
        ProductionStage NVARCHAR(100) NOT NULL,
        AnalysisResultsJson NVARCHAR(MAX) NULL,
        SpecificationCriteria NVARCHAR(MAX) NULL,
        InspectionDecision NVARCHAR(50) NOT NULL,
        Notes NVARCHAR(MAX) NULL,
        AttachmentPath NVARCHAR(MAX) NULL,
        AttachmentFileName NVARCHAR(MAX) NULL,
        AttachmentFileType NVARCHAR(MAX) NULL,
        AttachmentFileSize BIGINT NOT NULL CONSTRAINT DF_LabInProc_AttSize DEFAULT 0,
        ResponsibleEmployee NVARCHAR(150) NOT NULL,
        ResponsibleUserId UNIQUEIDENTIFIER NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_LabInProc_CreatedAt DEFAULT GETUTCDATE(),
        CreatedBy NVARCHAR(150) NOT NULL
    );
    CREATE INDEX IX_LaboratoryInProcessReports_ProductionReferenceNumber ON LaboratoryInProcessReports(ProductionReferenceNumber);
    CREATE INDEX IX_LaboratoryInProcessReports_LaboratorySection ON LaboratoryInProcessReports(LaboratorySection);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LaboratoryFinalConformityCertificates')
BEGIN
    CREATE TABLE LaboratoryFinalConformityCertificates (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CertificateNumber NVARCHAR(50) NOT NULL,
        ProductionReferenceNumber INT NOT NULL,
        RawMaterialReportId INT NULL CONSTRAINT FK_LabCert_RawReport REFERENCES LaboratoryRawMaterialReports(Id) ON DELETE SET NULL,
        LaboratorySection NVARCHAR(20) NOT NULL,
        FinalProductName NVARCHAR(200) NOT NULL,
        ActualFinalProductQuantity DECIMAL(18,3) NOT NULL,
        QuantityUnit NVARCHAR(50) NOT NULL CONSTRAINT DF_LabCert_QtyUnit DEFAULT N'طن',
        ProductionBatchNumber NVARCHAR(100) NOT NULL,
        ProductionWorkInProcessId INT NULL CONSTRAINT FK_LabCert_WIP REFERENCES ProductionWorkInProcess(Id) ON DELETE SET NULL,
        ProductionCompletionDateTime DATETIME2 NOT NULL,
        ActualRawMaterialInputQuantity DECIMAL(18,3) NOT NULL,
        CalculatedDifference DECIMAL(18,3) NOT NULL,
        ActualMeasuredWaste DECIMAL(18,3) NULL,
        WasteClassification NVARCHAR(50) NOT NULL CONSTRAINT DF_LabCert_WasteClass DEFAULT 'ORDINARY_WASTE',
        ConformityDecision NVARCHAR(50) NOT NULL,
        NonConformityAction NVARCHAR(50) NULL,
        ClassifiedProductType NVARCHAR(100) NULL,
        ReworkInstructions NVARCHAR(MAX) NULL,
        SpecificationResultsJson NVARCHAR(MAX) NULL,
        Observations NVARCHAR(MAX) NULL,
        AttachmentPath NVARCHAR(MAX) NULL,
        AttachmentFileName NVARCHAR(MAX) NULL,
        AttachmentFileType NVARCHAR(MAX) NULL,
        AttachmentFileSize BIGINT NOT NULL CONSTRAINT DF_LabCert_AttSize DEFAULT 0,
        ResponsibleEmployee NVARCHAR(150) NOT NULL,
        ResponsibleUserId UNIQUEIDENTIFIER NULL,
        ApprovalStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_LabCert_ApprovalStatus DEFAULT 'PENDING',
        ReviewedBy NVARCHAR(150) NULL,
        ReviewedByUserId UNIQUEIDENTIFIER NULL,
        ReviewedAt DATETIME2 NULL,
        RejectionReason NVARCHAR(500) NULL,
        IsStoragePosted BIT NOT NULL CONSTRAINT DF_LabCert_IsStoragePosted DEFAULT 0,
        StoragePostedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_LabCert_CreatedAt DEFAULT GETUTCDATE(),
        CreatedBy NVARCHAR(150) NOT NULL
    );
    CREATE UNIQUE INDEX IX_LaboratoryFinalConformityCertificates_CertificateNumber ON LaboratoryFinalConformityCertificates(CertificateNumber);
    CREATE INDEX IX_LaboratoryFinalConformityCertificates_ProductionReferenceNumber ON LaboratoryFinalConformityCertificates(ProductionReferenceNumber);
    CREATE INDEX IX_LaboratoryFinalConformityCertificates_LaboratorySection ON LaboratoryFinalConformityCertificates(LaboratorySection);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory')
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260927125002_AddTransportationTable')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20260927125002_AddTransportationTable', '8.0.8');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260927133035_AddManagementTable')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20260927133035_AddManagementTable', '8.0.8');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261001105431_AddFinanceTransactionsAndPoEnhancements')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20261001105431_AddFinanceTransactionsAndPoEnhancements', '8.0.8');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261003082328_UpdateTransportationDepartment')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20261003082328_UpdateTransportationDepartment', '8.0.8');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex', '8.0.8');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261003113538_AddCustodyAmountToTransportation')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20261003113538_AddCustodyAmountToTransportation', '8.0.8');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261004125029_RedesignStorageDepartment')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20261004125029_RedesignStorageDepartment', '8.0.8');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261005075013_AddTonPriceToPurchaseOrders')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20261005075013_AddTonPriceToPurchaseOrders', '8.0.8');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261012065711_AddStorageItemCardAndRawMaterials')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20261012065711_AddStorageItemCardAndRawMaterials', '8.0.8');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261013115024_AddStorageRedesignThreeCards')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20261013115024_AddStorageRedesignThreeCards', '8.0.8');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261019122421_AddStorageEnhancementsWeighingAndWaste')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20261019122421_AddStorageEnhancementsWeighingAndWaste', '8.0.8');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261024105637_AddLaboratoryDepartment')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20261024105637_AddLaboratoryDepartment', '8.0.8');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261025065700_AddProductionDepartment')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20261025065700_AddProductionDepartment', '8.0.8');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261026084230_UpdateLaboratoryDepartmentForm')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20261026084230_UpdateLaboratoryDepartmentForm', '8.0.8');
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

            var accUser = await db.Users.FirstOrDefaultAsync(u => u.Role == AppRoles.Accountant || u.Email == "sarah@facestone.com" || u.Username == "sarah_acc", cts.Token);
            if (accUser != null)
            {
                accUser.PasswordHash = HashPassword("Password123!");
                accUser.Role = AppRoles.Accountant;
            }
            else
            {
                await db.Users.AddAsync(new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Sarah Mitchell",
                    Username = "sarah_acc",
                    Email = "sarah@facestone.com",
                    PhoneNumber = "+20 10 1122 3344",
                    CountryCode = "+20",
                    Role = AppRoles.Accountant,
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

            // Ensure Finance Director exists for Payment Status management
            var financeDirectorUser = await db.Users.FirstOrDefaultAsync(u => 
                u.Role == AppRoles.FinanceDirector || 
                u.Role == AppRoles.FinanceDirectorDisplay ||
                u.Username == "finance_director" || 
                u.Email == "director.finance@facestone.com", cts.Token);
            if (financeDirectorUser == null)
            {
                await db.Users.AddAsync(new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "مدير الإدارة المالية",
                    Username = "finance_director",
                    Email = "director.finance@facestone.com",
                    PhoneNumber = "+20 10 9988 7766",
                    CountryCode = "+20",
                    Role = AppRoles.FinanceDirector,
                    PasswordHash = HashPassword("Director123!"),
                    CreatedAt = DateTime.UtcNow
                }, cts.Token);
            }
            else
            {
                financeDirectorUser.PasswordHash = HashPassword("Director123!");
                financeDirectorUser.Role = AppRoles.FinanceDirector;
            }

            // Ensure Control & Monitoring user exists
            var controlMonitoringUser = await db.Users.FirstOrDefaultAsync(u => 
                u.Role == AppRoles.ControlMonitoring || 
                u.Role == AppRoles.ControlMonitoringDisplay ||
                u.Username == "control_monitoring" || 
                u.Email == "control@facestone.com", cts.Token);
            if (controlMonitoringUser == null)
            {
                await db.Users.AddAsync(new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "مراقب ومراجع العمليات (Control & Monitoring)",
                    Username = "control_monitoring",
                    Email = "control@facestone.com",
                    PhoneNumber = "+20 10 8877 6655",
                    CountryCode = "+20",
                    Role = AppRoles.ControlMonitoring,
                    PasswordHash = HashPassword("Password123!"),
                    CreatedAt = DateTime.UtcNow
                }, cts.Token);
            }
            else
            {
                controlMonitoringUser.PasswordHash = HashPassword("Password123!");
                controlMonitoringUser.Role = AppRoles.ControlMonitoring;
            }

            // Ensure Attendance Employee user exists
            var attendanceEmployeeUser = await db.Users.FirstOrDefaultAsync(u => 
                u.Role == AppRoles.AttendanceEmployee || 
                u.Role == AppRoles.AttendanceEmployeeDisplay ||
                u.Username == "attendance_emp" || 
                u.Email == "attendance@facestone.com", cts.Token);
            if (attendanceEmployeeUser == null)
            {
                await db.Users.AddAsync(new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Tamer Hosny (مسؤول الحضور والانصراف)",
                    Username = "attendance_emp",
                    Email = "attendance@facestone.com",
                    PhoneNumber = "+20 10 9988 7766",
                    CountryCode = "+20",
                    Role = AppRoles.AttendanceEmployee,
                    PasswordHash = HashPassword("Password123!"),
                    CreatedAt = DateTime.UtcNow
                }, cts.Token);
            }
            else
            {
                attendanceEmployeeUser.PasswordHash = HashPassword("Password123!");
                attendanceEmployeeUser.Role = AppRoles.AttendanceEmployee;
            }

            await db.SaveChangesAsync(cts.Token);

            // Ensure Physical Laboratory Employee exists
            var physLabUser = await db.Users.FirstOrDefaultAsync(u => 
                u.Role == AppRoles.PhysicalLabEmployee || 
                u.Role == AppRoles.PhysicalLabEmployeeDisplay ||
                u.Username == "phys_lab" || 
                u.Email == "phys.lab@facestone.com", cts.Token);
            if (physLabUser == null)
            {
                await db.Users.AddAsync(new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Eng. Tarek Mahmoud (مسؤول المعمل الفيزيائي)",
                    Username = "phys_lab",
                    Email = "phys.lab@facestone.com",
                    PhoneNumber = "+20 10 2233 4455",
                    CountryCode = "+20",
                    Role = AppRoles.PhysicalLabEmployee,
                    PasswordHash = HashPassword("Password123!"),
                    CreatedAt = DateTime.UtcNow
                }, cts.Token);
            }
            else
            {
                physLabUser.PasswordHash = HashPassword("Password123!");
                physLabUser.Role = AppRoles.PhysicalLabEmployee;
            }

            // Ensure Chemical Laboratory Employee exists
            var chemLabUser = await db.Users.FirstOrDefaultAsync(u => 
                u.Role == AppRoles.ChemicalLabEmployee || 
                u.Role == AppRoles.ChemicalLabEmployeeDisplay ||
                u.Username == "chem_lab" || 
                u.Email == "chem.lab@facestone.com", cts.Token);
            if (chemLabUser == null)
            {
                await db.Users.AddAsync(new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Chemist Mona Adel (مسؤولة المعمل الكيميائي)",
                    Username = "chem_lab",
                    Email = "chem.lab@facestone.com",
                    PhoneNumber = "+20 10 3344 5577",
                    CountryCode = "+20",
                    Role = AppRoles.ChemicalLabEmployee,
                    PasswordHash = HashPassword("Password123!"),
                    CreatedAt = DateTime.UtcNow
                }, cts.Token);
            }
            else
            {
                chemLabUser.PasswordHash = HashPassword("Password123!");
                chemLabUser.Role = AppRoles.ChemicalLabEmployee;
            }

            await db.SaveChangesAsync(cts.Token);

            // Ensure Production Manager exists
            var prodManagerUser = await db.Users.FirstOrDefaultAsync(u => 
                u.Role == AppRoles.ProductionManager || 
                u.Role == AppRoles.ProductionManagerDisplay ||
                u.Username == "prod_manager" || 
                u.Email == "prod.manager@facestone.com", cts.Token);
            if (prodManagerUser == null)
            {
                await db.Users.AddAsync(new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Eng. Hesham Farouk (مدير الإنتاج)",
                    Username = "prod_manager",
                    Email = "prod.manager@facestone.com",
                    PhoneNumber = "+20 10 4455 8899",
                    CountryCode = "+20",
                    Role = AppRoles.ProductionManager,
                    PasswordHash = HashPassword("Production123!"),
                    CreatedAt = DateTime.UtcNow
                }, cts.Token);
            }
            else
            {
                prodManagerUser.PasswordHash = HashPassword("Production123!");
                prodManagerUser.Role = AppRoles.ProductionManager;
            }

            // Ensure Production Manager 2 (Ahmed Nabil) exists with ProductionManager role
            var prodEmployeeUser = await db.Users.FirstOrDefaultAsync(u => 
                u.Username == "prod_employee" || 
                u.Email == "prod.employee@facestone.com", cts.Token);
            if (prodEmployeeUser == null)
            {
                await db.Users.AddAsync(new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Ahmed Nabil (مدير الإنتاج - الوردية الثانية)",
                    Username = "prod_employee",
                    Email = "prod.employee@facestone.com",
                    PhoneNumber = "+20 10 7788 9900",
                    CountryCode = "+20",
                    Role = AppRoles.ProductionManager,
                    PasswordHash = HashPassword("Production123!"),
                    CreatedAt = DateTime.UtcNow
                }, cts.Token);
            }
            else
            {
                prodEmployeeUser.FullName = "Ahmed Nabil (مدير الإنتاج - الوردية الثانية)";
                prodEmployeeUser.PasswordHash = HashPassword("Production123!");
                prodEmployeeUser.Role = AppRoles.ProductionManager;
            }

            await db.SaveChangesAsync(cts.Token);

            // Initialize Production Sequences if needed
            var seqTypes = new[] { "ORDER", "BATCH", "OUTPUT", "REWORK", "DAILY_SUBMISSION" };
            foreach (var st in seqTypes)
            {
                if (!await db.ProductionSequences.AnyAsync(s => s.SequenceType == st, cts.Token))
                {
                    await db.ProductionSequences.AddAsync(new ProductionSequence { SequenceType = st, NextValue = 1 }, cts.Token);
                }
            }
            await db.SaveChangesAsync(cts.Token);

            // ========================================================================
            // SEED: STORAGE REDESIGN (Locations, Clients, Sequences, Ledger Stock)
            // ========================================================================
            if (!await db.StorageLocations.AnyAsync(cts.Token))
            {
                logger.LogInformation("Seeding default storage locations...");
                await db.StorageLocations.AddRangeAsync(new[]
                {
                    new StorageLocation { Name = "مستودع الخامات الرئيسي", Code = "WH-MAIN", Description = "المستودع المركزي لتخزين الإسمنت والركام والمواد الخام", IsActive = true },
                    new StorageLocation { Name = "صوامع الإسمنت الرئيسية", Code = "SILO-CEM", Description = "صوامع تخزين بودرة الإسمنت البورتلاندي السائب", IsActive = true },
                    new StorageLocation { Name = "مستودع قطاع ج", Code = "WH-SEC-C", Description = "مستودع تشوين الركام والسن والرمل بموقع حلوان", IsActive = true },
                    new StorageLocation { Name = "مستودع الكيماويات والمضافات", Code = "WH-CHEM", Description = "مستودع تخزين مضافات الخرسانة السائلة والكيميائية", IsActive = true }
                }, cts.Token);
                await db.SaveChangesAsync(cts.Token);
            }

            if (!await db.StorageClients.AnyAsync(cts.Token))
            {
                logger.LogInformation("Seeding default FaceStone clients and destinations...");
                var client1 = new StorageClient
                {
                    Name = "شركة المقاولون العرب",
                    PhoneNumber = "+20 10 1234 5678",
                    IsActive = true,
                    Destinations = new List<StorageClientDestination>
                    {
                        new StorageClientDestination { DestinationName = "مشروع العاصمة الإدارية - الحي السكني R3", IsActive = true },
                        new StorageClientDestination { DestinationName = "مشروع مونوريل شرق النيل - قطاع 2", IsActive = true },
                        new StorageClientDestination { DestinationName = "مشروع محور روض الفرج وكوبري تحيا مصر", IsActive = true }
                    }
                };

                var client2 = new StorageClient
                {
                    Name = "أوراسكوم للإنشاءات",
                    PhoneNumber = "+20 10 2345 6789",
                    IsActive = true,
                    Destinations = new List<StorageClientDestination>
                    {
                        new StorageClientDestination { DestinationName = "مشروع المتحف المصري الكبير", IsActive = true },
                        new StorageClientDestination { DestinationName = "مشروع محطة الضبعة النووية", IsActive = true },
                        new StorageClientDestination { DestinationName = "مشروع مترو الأنفاق الخط الرابع", IsActive = true }
                    }
                };

                var client3 = new StorageClient
                {
                    Name = "حسن علام القابضة",
                    PhoneNumber = "+20 10 3456 7890",
                    IsActive = true,
                    Destinations = new List<StorageClientDestination>
                    {
                        new StorageClientDestination { DestinationName = "مشروع أبراج مدينة العلمين الجديدة", IsActive = true },
                        new StorageClientDestination { DestinationName = "مشروع محطة قطارات بشتيل الذكية", IsActive = true }
                    }
                };

                var client4 = new StorageClient
                {
                    Name = "مجموعة طلعت مصطفى",
                    PhoneNumber = "+20 10 4567 8901",
                    IsActive = true,
                    Destinations = new List<StorageClientDestination>
                    {
                        new StorageClientDestination { DestinationName = "مشروع مدينتي - قطاع البناء 4", IsActive = true },
                        new StorageClientDestination { DestinationName = "مشروع مدينة نور كابيتال سيتي", IsActive = true }
                    }
                };

                var client5 = new StorageClient
                {
                    Name = "شركة بالم هيلز للتعمير",
                    PhoneNumber = "+20 10 5678 9012",
                    IsActive = true,
                    Destinations = new List<StorageClientDestination>
                    {
                        new StorageClientDestination { DestinationName = "مشروع بادية - 6 أكتوبر", IsActive = true },
                        new StorageClientDestination { DestinationName = "مشروع كمبوند بالم هيلز التجمع الخامس", IsActive = true }
                    }
                };

                var client6 = new StorageClient
                {
                    Name = "السويدي إليكتريك",
                    PhoneNumber = "+20 10 6789 0123",
                    IsActive = true,
                    Destinations = new List<StorageClientDestination>
                    {
                        new StorageClientDestination { DestinationName = "مجمع العاشر من رمضان الصناعي", IsActive = true },
                        new StorageClientDestination { DestinationName = "مشروع محطة بنبان للطاقة الشمسية", IsActive = true }
                    }
                };

                await db.StorageClients.AddRangeAsync(new[] { client1, client2, client3, client4, client5, client6 }, cts.Token);
                await db.SaveChangesAsync(cts.Token);
            }

            if (!await db.StorageSequences.AnyAsync(cts.Token))
            {
                logger.LogInformation("Seeding initial storage sequence values...");
                await db.StorageSequences.AddRangeAsync(new[]
                {
                    new StorageSequence { SequenceType = "IMPORT", NextValue = 1 },
                    new StorageSequence { SequenceType = "EXPORT", NextValue = 1 },
                    new StorageSequence { SequenceType = "TRANSFER", NextValue = 1 }
                }, cts.Token);
                await db.SaveChangesAsync(cts.Token);
            }

            // Seed standard materials and initial inventory ledger balances if needed
            var mainLocation = await db.StorageLocations.FirstOrDefaultAsync(l => l.Code == "WH-MAIN", cts.Token);
            if (mainLocation != null)
            {
                var standardMaterials = new (string Name, decimal Stock, string Type)[]
                {
                    ("إسمنت بورتلاندي عادي رتبة 42.5", 500.000m, MaterialProcessType.Mechanical),
                    ("ركام خرساني متدرج (سن 1)", 450.000m, MaterialProcessType.Mechanical),
                    ("ركام خرساني متدرج (سن 2)", 400.000m, MaterialProcessType.Mechanical),
                    ("رمل سيليكا ناعم معتمد", 350.000m, MaterialProcessType.Mechanical),
                    ("بودرة كربونات الكالسيوم", 200.000m, MaterialProcessType.Mechanical),
                    ("مضافات ومخفضات مياه فائقة (Slurry)", 150.000m, MaterialProcessType.Chemical)
                };

                foreach (var matInfo in standardMaterials)
                {
                    var mat = await db.RawMaterials.FirstOrDefaultAsync(m => m.Name == matInfo.Name, cts.Token);
                    if (mat == null)
                    {
                        mat = new RawMaterial
                        {
                            Name = matInfo.Name,
                            MaterialType = matInfo.Type,
                            InitialQuantity = matInfo.Stock,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "Storage Manager"
                        };
                        await db.RawMaterials.AddAsync(mat, cts.Token);
                        await db.SaveChangesAsync(cts.Token);
                    }
                    else if (string.IsNullOrEmpty(mat.MaterialType) || mat.MaterialType == "MECHANICAL" && matInfo.Type == MaterialProcessType.Chemical)
                    {
                        mat.MaterialType = matInfo.Type;
                        await db.SaveChangesAsync(cts.Token);
                    }

                    // Check if opening stock exists in ledger
                    var hasLedgerStock = await db.StorageInventoryLedger.AnyAsync(l => 
                        l.StorageLocationId == mainLocation.Id && l.RawMaterialId == mat.Id, cts.Token);
                    
                    if (!hasLedgerStock)
                    {
                        await db.StorageInventoryLedger.AddAsync(new StorageInventoryLedger
                        {
                            StorageLocationId = mainLocation.Id,
                            RawMaterialId = mat.Id,
                            MovementType = "OPENING_BALANCE",
                            Quantity = matInfo.Stock,
                            RunningBalance = matInfo.Stock,
                            MovementDate = DateTime.UtcNow.AddDays(-7),
                            CreatedAt = DateTime.UtcNow.AddDays(-7),
                            CreatedBy = "System Initializer"
                        }, cts.Token);
                    }
                }
                await db.SaveChangesAsync(cts.Token);
            }

            // Initialize Laboratory Production Reference Sequence if needed
            var labSeq = await db.LaboratoryProductionSequences.FirstOrDefaultAsync(s => s.SequenceName == "PRODUCTION_REFERENCE", cts.Token);
            if (labSeq == null)
            {
                labSeq = new LaboratoryProductionSequence
                {
                    SequenceName = "PRODUCTION_REFERENCE",
                    NextValue = 1
                };
                await db.LaboratoryProductionSequences.AddAsync(labSeq, cts.Token);
                await db.SaveChangesAsync(cts.Token);
            }

            // Seed initial demo Laboratory records for Reference #1 and Reference #2
            if (!await db.LaboratoryRawMaterialReports.AnyAsync(cts.Token))
            {
                logger.LogInformation("Seeding initial Laboratory Raw Material Reports and production traceability records...");

                var cementMat = await db.RawMaterials.FirstOrDefaultAsync(m => m.Name.Contains("إسمنت") || m.MaterialType == "MECHANICAL", cts.Token);
                var slurryMat = await db.RawMaterials.FirstOrDefaultAsync(m => m.Name.Contains("مضافات") || m.MaterialType == "CHEMICAL", cts.Token);
                var trans1 = await db.Transportations.FirstOrDefaultAsync(t => t.ApprovalStatus == "APPROVED", cts.Token);
                var po1 = await db.PurchaseOrders.FirstOrDefaultAsync(cts.Token);

                var ref1 = new LaboratoryRawMaterialReport
                {
                    ProductionReferenceNumber = 1,
                    LaboratorySection = "PHYSICAL",
                    AnalysisDateTime = DateTime.UtcNow.AddDays(-5),
                    RawMaterialDescription = "إسمنت بورتلاندي عادي رتبة 42.5N فحص ميكانيكي وفيزيائي",
                    RawMaterialId = cementMat?.Id,
                    SupplierName = "شركة أسمنت السويس الوطنية",
                    SourceTransportationId = trans1?.Id,
                    TransportationTransactionNumber = trans1?.TransactionNumber ?? 1001,
                    PurchaseOrderId = po1?.Id,
                    PONumber = po1?.PONumber ?? "PO-2026-ST-001",
                    ReceivedQuantity = 60.000m,
                    AcceptedQuantity = 55.000m,
                    RejectedQuantity = 5.000m,
                    AnalysisResultsJson = "{\"fineness\":\"3450\",\"sieveResidue90\":\"4.2\",\"moisture\":\"0.75\",\"specificGravity\":\"3.12\",\"compStrength2D\":\"22.5\",\"compStrength28D\":\"46.8\",\"soundness\":\"1.4\",\"initialSettingTime\":\"145\",\"finalSettingTime\":\"210\"}",
                    InspectionDecision = "PARTIALLY_ACCEPTED",
                    Notes = "تم قبول 55 طن مطابقة للمواصفة القياسية المصرية، واستبعاد 5 أطنان لوجود تكتلات رطوبة جزئية أثناء النقل.",
                    ResponsibleEmployee = "Eng. Tarek Mahmoud (مسؤول المعمل الفيزيائي)",
                    ApprovalStatus = "APPROVED",
                    ReviewedBy = "مراقب ومراجع العمليات (Control & Monitoring)",
                    ReviewedAt = DateTime.UtcNow.AddDays(-5),
                    IsStorageReceived = true,
                    StorageReceivedAt = DateTime.UtcNow.AddDays(-4),
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    CreatedBy = "phys_lab"
                };

                var ref2 = new LaboratoryRawMaterialReport
                {
                    ProductionReferenceNumber = 2,
                    LaboratorySection = "CHEMICAL",
                    AnalysisDateTime = DateTime.UtcNow.AddDays(-3),
                    RawMaterialDescription = "مضافات ومخفضات مياه فائقة للأداء العالي (Slurry Admixture)",
                    RawMaterialId = slurryMat?.Id,
                    SupplierName = "سولفاي كيميكالز الشرق الأوسط",
                    SourceTransportationId = null,
                    PurchaseOrderId = po1?.Id,
                    PONumber = po1?.PONumber ?? "PO-2026-ST-001",
                    ReceivedQuantity = 30.000m,
                    AcceptedQuantity = 30.000m,
                    RejectedQuantity = 0.000m,
                    AnalysisResultsJson = "{\"activeSolids\":\"38.5\",\"pH\":\"7.4\",\"density\":\"1.18\",\"chlorideContent\":\"0.02\",\"sulfateSO3\":\"0.15\",\"insolubleResidue\":\"0.05\",\"lossOnIgnition\":\"0.6\"}",
                    InspectionDecision = "ACCEPTED",
                    Notes = "عينة مطابقة لمتطلبات المواصفات الكيميائية والاشتراطات البيئية وتم اعتماد كامل الكمية.",
                    ResponsibleEmployee = "Chemist Mona Adel (مسؤولة المعمل الكيميائي)",
                    ApprovalStatus = "APPROVED",
                    ReviewedBy = "مراقب ومراجع العمليات (Control & Monitoring)",
                    ReviewedAt = DateTime.UtcNow.AddDays(-3),
                    IsStorageReceived = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    CreatedBy = "chem_lab"
                };

                await db.LaboratoryRawMaterialReports.AddRangeAsync(new[] { ref1, ref2 }, cts.Token);
                await db.SaveChangesAsync(cts.Token);

                // In-Process test report linked to Ref #1
                var inProc1 = new LaboratoryInProcessReport
                {
                    ProductionReferenceNumber = 1,
                    RawMaterialReportId = ref1.Id,
                    ProductionBatchNumber = "BATCH-2026-C35-01",
                    LaboratorySection = "PHYSICAL",
                    ProductName = "خلطة خرسانية جاهزة عالية الإجهاد رتبة C35",
                    TestDateTime = DateTime.UtcNow.AddDays(-3),
                    ProductionStage = "الخلط الأولي (Mixing)",
                    AnalysisResultsJson = "{\"slump\":\"12.5\",\"slumpFlow\":\"550\",\"concreteTemp\":\"24.5\",\"airContent\":\"1.8\",\"unitWeight\":\"2385\"}",
                    SpecificationCriteria = "الهبوط: 10-14 سم | درجة الحرارة: < 30°C | المحتوى الهوائي: < 2%",
                    InspectionDecision = "MEETS_REQUIREMENTS",
                    Notes = "فحص متجانس، القوام مطابق ونسبة السيولة والدمك ممتازة داخل الخلاط المركزي.",
                    ResponsibleEmployee = "Eng. Tarek Mahmoud (مسؤول المعمل الفيزيائي)",
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    CreatedBy = "phys_lab"
                };

                await db.LaboratoryInProcessReports.AddAsync(inProc1, cts.Token);
                await db.SaveChangesAsync(cts.Token);

                // Final Product Conformity Certificate linked to Ref #1
                var finalCert1 = new LaboratoryFinalConformityCertificate
                {
                    CertificateNumber = "CERT-2026-0001",
                    ProductionReferenceNumber = 1,
                    RawMaterialReportId = ref1.Id,
                    LaboratorySection = "PHYSICAL",
                    FinalProductName = "خرسانة جاهزة عالية الإجهاد رتبة C35",
                    ActualFinalProductQuantity = 52.000m,
                    QuantityUnit = "طن",
                    ProductionBatchNumber = "BATCH-2026-C35-01",
                    ProductionCompletionDateTime = DateTime.UtcNow.AddDays(-2),
                    ActualRawMaterialInputQuantity = 55.000m,
                    CalculatedDifference = 3.000m,
                    ActualMeasuredWaste = 1.500m,
                    WasteClassification = "RECOVERABLE_MATERIAL",
                    ConformityDecision = "CONFORMS",
                    NonConformityAction = null,
                    SpecificationResultsJson = "{\"compStrength7D\":\"28.5\",\"compStrength28D\":\"38.4\",\"waterAbsorption\":\"3.2\",\"flexuralStrength\":\"4.8\",\"density\":\"2410\"}",
                    Observations = "المنتج حقق مقاومة ضغط تتجاوز الرتبة التصميمية C35 بعد 7 و 28 يوماً. الهالك تم استرداده كركام تدوير.",
                    ResponsibleEmployee = "Eng. Tarek Mahmoud (مسؤول المعمل الفيزيائي)",
                    ApprovalStatus = "APPROVED",
                    ReviewedBy = "مراقب ومراجع العمليات (Control & Monitoring)",
                    ReviewedAt = DateTime.UtcNow.AddDays(-2),
                    IsStoragePosted = true,
                    StoragePostedAt = DateTime.UtcNow.AddDays(-2),
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    CreatedBy = "phys_lab"
                };

                await db.LaboratoryFinalConformityCertificates.AddAsync(finalCert1, cts.Token);
                await db.SaveChangesAsync(cts.Token);

                // Update sequence NextValue to 3
                labSeq.NextValue = 3;
                await db.SaveChangesAsync(cts.Token);
            }

            // Seed initial demo Production Orders, Batches, and Outputs if table is empty
            if (!await db.ProductionOrders.AnyAsync(cts.Token))
            {
                logger.LogInformation("Seeding initial Production Orders, Batches, and Outputs...");
                var loc = await db.StorageLocations.FirstOrDefaultAsync(cts.Token);
                var cementMat = await db.RawMaterials.FirstOrDefaultAsync(m => m.Name.Contains("إسمنت"), cts.Token);
                var slurryMat = await db.RawMaterials.FirstOrDefaultAsync(m => m.Name.Contains("مضافات"), cts.Token);

                var pOrder1 = new ProductionOrder
                {
                    OrderNumber = "ORD-2026-0001",
                    ProductionReferenceNumber = 1,
                    ProductName = "حجر صناعي واجهات بيج نخب أول",
                    RequestedQuantity = 100.000m,
                    Unit = "طن",
                    StorageLocationId = loc?.Id ?? 1,
                    StorageLocationName = loc?.Name ?? "مستودع الخامات الرئيسي",
                    Status = "IN_PRODUCTION",
                    CurrentStage = "التشكيل والصب",
                    ResponsibleEmployee = "Eng. Hesham Farouk (مدير الإنتاج)",
                    PlannedStartDate = DateTime.UtcNow.AddDays(-2),
                    ActualStartDate = DateTime.UtcNow.AddDays(-2),
                    IsStorageApproved = true,
                    StorageApprovedAt = DateTime.UtcNow.AddDays(-2),
                    StorageApprovedBy = "Storage & Factory Operations Manager",
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    CreatedBy = "prod_manager"
                };

                if (cementMat != null)
                {
                    pOrder1.Items.Add(new ProductionOrderItem
                    {
                        RawMaterialId = cementMat.Id,
                        MaterialName = cementMat.Name,
                        RequestedQuantity = 75.000m,
                        IssuedQuantity = 75.000m,
                        ConsumedQuantity = 75.000m,
                        Unit = "طن"
                    });
                }

                if (slurryMat != null)
                {
                    pOrder1.Items.Add(new ProductionOrderItem
                    {
                        RawMaterialId = slurryMat.Id,
                        MaterialName = slurryMat.Name,
                        RequestedQuantity = 25.000m,
                        IssuedQuantity = 25.000m,
                        ConsumedQuantity = 25.000m,
                        Unit = "طن"
                    });
                }

                await db.ProductionOrders.AddAsync(pOrder1, cts.Token);
                await db.SaveChangesAsync(cts.Token);

                var pBatch1 = new ProductionBatch
                {
                    BatchNumber = "BATCH-2026-0001",
                    ProductionOrderId = pOrder1.Id,
                    ProductionReferenceNumber = 1,
                    CurrentStage = "التشكيل والصب",
                    Status = "IN_PROGRESS",
                    QualityHold = false,
                    TotalInputQuantity = 100.000m,
                    TotalOutputQuantity = 92.000m,
                    TotalWasteQuantity = 8.000m,
                    StartedAt = DateTime.UtcNow.AddDays(-2),
                    ResponsibleEmployee = "Eng. Hesham Farouk (مدير الإنتاج)",
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    CreatedBy = "prod_manager"
                };

                await db.ProductionBatches.AddAsync(pBatch1, cts.Token);
                await db.SaveChangesAsync(cts.Token);

                // Add demo outputs: 1 Semi-Finished, 1 Finished, 1 Recoverable Waste
                var out1 = new ProductionOutput
                {
                    OutputNumber = "OUT-2026-0001",
                    ProductionOrderId = pOrder1.Id,
                    ProductionBatchId = pBatch1.Id,
                    ProductionReferenceNumber = 1,
                    ProductName = "حجر واجهات بيج نخب أول - معالج نصف مصنع",
                    Classification = "SEMI_FINISHED",
                    Quantity = 40.000m,
                    Unit = "طن",
                    ProductionStage = "التشكيل والصب",
                    NextStage = "المعالجة الكيميائية والتجفيف",
                    DestinationStorageLocationId = loc?.Id,
                    DestinationStorageLocationName = loc?.Name,
                    LaboratoryConformityStatus = "NOT_APPLICABLE",
                    ResponsibleEmployee = "Ahmed Nabil (مسؤول خط الإنتاج والتشغيل)",
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    CreatedBy = "prod_employee"
                };

                var out2 = new ProductionOutput
                {
                    OutputNumber = "OUT-2026-0002",
                    ProductionOrderId = pOrder1.Id,
                    ProductionBatchId = pBatch1.Id,
                    ProductionReferenceNumber = 1,
                    ProductName = "حجر صناعي واجهات بيج نخب أول تام الصنع",
                    Classification = "FINISHED",
                    Quantity = 52.000m,
                    Unit = "طن",
                    ProductionStage = "المرحلة النهائية",
                    DestinationStorageLocationId = loc?.Id,
                    DestinationStorageLocationName = loc?.Name,
                    LaboratoryConformityStatus = "CONFORMS",
                    ResponsibleEmployee = "Eng. Hesham Farouk (مدير الإنتاج)",
                    CreatedAt = DateTime.UtcNow.AddHours(-12),
                    CreatedBy = "prod_manager"
                };

                var out3 = new ProductionOutput
                {
                    OutputNumber = "OUT-2026-0003",
                    ProductionOrderId = pOrder1.Id,
                    ProductionBatchId = pBatch1.Id,
                    ProductionReferenceNumber = 1,
                    ProductName = "كسارة وركام خرساني مسترد لإعادة التدوير",
                    Classification = "RECOVERABLE_WASTE",
                    Quantity = 8.000m,
                    Unit = "طن",
                    ProductionStage = "المرحلة النهائية",
                    DestinationStorageLocationId = loc?.Id,
                    DestinationStorageLocationName = loc?.Name,
                    LaboratoryConformityStatus = "NOT_APPLICABLE",
                    ResponsibleEmployee = "Ahmed Nabil (مسؤول خط الإنتاج والتشغيل)",
                    CreatedAt = DateTime.UtcNow.AddHours(-10),
                    CreatedBy = "prod_employee"
                };

                await db.ProductionOutputs.AddRangeAsync(new[] { out1, out2, out3 }, cts.Token);
                await db.SaveChangesAsync(cts.Token);

                // Update sequence next values
                var ordSeq = await db.ProductionSequences.FirstOrDefaultAsync(s => s.SequenceType == "ORDER", cts.Token);
                if (ordSeq != null) ordSeq.NextValue = 2;

                var batSeq = await db.ProductionSequences.FirstOrDefaultAsync(s => s.SequenceType == "BATCH", cts.Token);
                if (batSeq != null) batSeq.NextValue = 2;

                var outSeq = await db.ProductionSequences.FirstOrDefaultAsync(s => s.SequenceType == "OUTPUT", cts.Token);
                if (outSeq != null) outSeq.NextValue = 4;

                await db.SaveChangesAsync(cts.Token);
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
                    RemainingQuantity = 200.000m,
                    Destination = "Factory Sector B - Precast Yard",
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
                    RemainingQuantity = 300.000m,
                    Destination = "Cairo Factory - Main Silos",
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
                    RemainingQuantity = 150.000m,
                    Destination = "Cairo Factory - Chemical Additives Plant",
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
                    RemainingQuantity = 100.000m,
                    Destination = "Ain Sokhna Port Logistics Hub",
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
                    RemainingQuantity = 120.000m,
                    Destination = "Factory Sector C - Silos & Aggregate Storage",
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
                    RemainingQuantity = 50.000m,
                    Destination = "Factory Sector B - Precast Yard",
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
                    RemainingQuantity = 60.000m,
                    Destination = "6th of October Industrial Complex",
                    PricePerTon = 2050.00m,
                    TotalValue = 60.000m * 2050.00m, // 123,000.00 EGP
                    InvoiceNumber = "FS-2026-INV-902",
                    Notes = "إسمنت بورتلاندي 42.5N معتمد",
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                };

            }

            // Ensure PO-100 and PO-200 exist for Transportation testing
            if (!await db.PurchaseOrders.AnyAsync(p => p.PONumber == "PO-100", cts.Token))
            {
                await db.PurchaseOrders.AddAsync(new PurchaseOrder
                {
                    PONumber = "PO-100",
                    Supplier = "FaceStone Concrete Solutions",
                    OrderDate = DateTime.UtcNow.AddDays(-1),
                    Quantity = 20000.000m,
                    RemainingQuantity = 20000.000m,
                    Destination = "Cairo Factory",
                    PricePerTon = 1200.00m,
                    TotalValue = 20000.000m * 1200.00m,
                    InvoiceNumber = "FS-PO-100-INV",
                    Notes = "أمر شراء معتمد لاختبارات النقل المتتابعة"
                }, cts.Token);
            }

            if (!await db.PurchaseOrders.AnyAsync(p => p.PONumber == "PO-200", cts.Token))
            {
                await db.PurchaseOrders.AddAsync(new PurchaseOrder
                {
                    PONumber = "PO-200",
                    Supplier = "Suez Quarry Sector",
                    OrderDate = DateTime.UtcNow.AddDays(-1),
                    Quantity = 20000.000m,
                    RemainingQuantity = 20000.000m,
                    Destination = "Cairo Factory",
                    PricePerTon = 1400.00m,
                    TotalValue = 20000.000m * 1400.00m,
                    InvoiceNumber = "FS-PO-200-INV",
                    Notes = "أمر شراء معتمد لاختبارات النقل المتتابعة"
                }, cts.Token);
            }

            if (!await db.PurchaseOrders.AnyAsync(p => p.PONumber == "PO-300", cts.Token))
            {
                await db.PurchaseOrders.AddAsync(new PurchaseOrder
                {
                    PONumber = "PO-300",
                    Supplier = "FaceStone Concrete Solutions",
                    OrderDate = DateTime.UtcNow.AddDays(-1),
                    Quantity = 50000.000m,
                    RemainingQuantity = 50000.000m,
                    Destination = "Cairo Factory",
                    PricePerTon = 1200.00m,
                    TotalValue = 50000.000m * 1200.00m,
                    InvoiceNumber = "FS-PO-300-INV",
                    Notes = "أمر شراء معتمد لاختبارات النقل المتتابعة"
                }, cts.Token);
            }

            if (!await db.PurchaseOrders.AnyAsync(p => p.PONumber == "PO-400", cts.Token))
            {
                await db.PurchaseOrders.AddAsync(new PurchaseOrder
                {
                    PONumber = "PO-400",
                    Supplier = "Suez Quarry Sector",
                    OrderDate = DateTime.UtcNow.AddDays(-1),
                    Quantity = 50000.000m,
                    RemainingQuantity = 50000.000m,
                    Destination = "Cairo Factory",
                    PricePerTon = 1400.00m,
                    TotalValue = 50000.000m * 1400.00m,
                    InvoiceNumber = "FS-PO-400-INV",
                    Notes = "أمر شراء معتمد لاختبارات النقل المتتابعة"
                }, cts.Token);
            }

            // ========================================================================
            // FAKE TEST DATA (Requirement 5: Clearly identifiable fake test data)
            // Stored in the existing source tables; used to test Management monitoring.
            // ========================================================================
            if (!await db.PurchaseOrders.AnyAsync(p => p.PONumber == "TEST-PO-001", cts.Token))
            {
                logger.LogInformation("Seeding clearly identifiable fake test POs and supplier records...");
                var testPo1 = new PurchaseOrder
                {
                    PONumber = "TEST-PO-001",
                    Supplier = "TEST-SUPPLIER-001",
                    OrderDate = DateTime.UtcNow.AddDays(-4),
                    Quantity = 500.000m,
                    RemainingQuantity = 320.000m,
                    Destination = "مصنع حلوان - قطاع ج",
                    PricePerTon = 1500.00m,
                    TotalValue = 750000.00m,
                    PaymentStatus = "مدفوع جزئياً",
                    InvoiceNumber = "INV-TEST-001",
                    Notes = "أمر شراء تجريبي معتمد لاختبار منظومة المراقبة والإدارة",
                    CreatedAt = DateTime.UtcNow.AddDays(-4)
                };

                var testPo2 = new PurchaseOrder
                {
                    PONumber = "TEST-PO-002",
                    Supplier = "TEST-SUPPLIER-002",
                    OrderDate = DateTime.UtcNow.AddDays(-2),
                    Quantity = 300.000m,
                    RemainingQuantity = 300.000m,
                    Destination = "صوامع الإسمنت الرئيسية",
                    PricePerTon = 1800.00m,
                    TotalValue = 540000.00m,
                    PaymentStatus = "غير مدفوع",
                    InvoiceNumber = "INV-TEST-002",
                    Notes = "أمر شراء تجريبي غير مسدد لاختبار تنوع حالات الدفع",
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                };

                await db.PurchaseOrders.AddRangeAsync(new[] { testPo1, testPo2 }, cts.Token);
            }

            // Fake Transportation Transactions
            if (!await db.Transportations.AnyAsync(t => t.PONumber == "TEST-PO-001", cts.Token))
            {
                logger.LogInformation("Seeding fake transportation transactions...");
                var transTx1 = new Transportation
                {
                    TransactionNumber = 8801,
                    TransactionDate = DateTime.UtcNow.AddHours(-18),
                    CarNumber = "ت س ت 1234",
                    DriverName = "سائق تجريبي - أحمد فؤاد",
                    Source = "محاجر بني سويف - قطاع الاختبار",
                    PONumber = "TEST-PO-001",
                    CompanyName = "TEST-SUPPLIER-001",
                    MaterialType = "سن وركام خرساني تدرج 1",
                    ExpectedWeight = 100.000m,
                    WeightBeforeLoading = 36.500m,
                    WeightWhenEnteringFactory = 135.000m,
                    WeightWhenExitingFactory = 36.500m,
                    InternalWeight = 98.500m,
                    ExternalLoadedWeight = 136.500m,
                    ExternalEmptyWeight = 36.500m,
                    ExternalNetWeight = 100.000m,
                    NetWeight = 98.500m,
                    WasteQuantity = 1.500m, // Difference example
                    Destination = "مصنع حلوان - قطاع ج",
                    TransactionStatus = "Approved",
                    ReceivingStatus = "Approved / Accepted",
                    DepartureTime = DateTime.UtcNow.AddHours(-22),
                    ArrivalTime = DateTime.UtcNow.AddHours(-19),
                    ShippingDurationMinutes = 180,
                    InternalWeighbridgeCode = "WB-INT-TEST-01",
                    ExternalWeighbridgeCode = "WB-EXT-TEST-01",
                    Notes = "حركة نقل تجريبية معتمدة توضح فرق الميزان وهالك النقل (1.5 طن)",
                    DateOfSubmit = DateTime.UtcNow.AddHours(-18)
                };

                var transTx2 = new Transportation
                {
                    TransactionNumber = 8802,
                    TransactionDate = DateTime.UtcNow.AddHours(-8),
                    CarNumber = "ت س ت 5678",
                    DriverName = "سائق تجريبي - محمود علي",
                    Source = "محاجر بني سويف - قطاع الاختبار",
                    PONumber = "TEST-PO-001",
                    CompanyName = "TEST-SUPPLIER-001",
                    MaterialType = "سن وركام خرساني تدرج 1",
                    ExpectedWeight = 81.500m,
                    WeightBeforeLoading = 34.000m,
                    WeightWhenEnteringFactory = 115.500m,
                    WeightWhenExitingFactory = 34.000m,
                    InternalWeight = 81.500m,
                    ExternalLoadedWeight = 115.500m,
                    ExternalEmptyWeight = 34.000m,
                    ExternalNetWeight = 81.500m,
                    NetWeight = 81.500m,
                    WasteQuantity = 0.000m,
                    Destination = "مصنع حلوان - قطاع ج",
                    TransactionStatus = "Approved",
                    ReceivingStatus = "Approved / Accepted",
                    DepartureTime = DateTime.UtcNow.AddHours(-11),
                    ArrivalTime = DateTime.UtcNow.AddHours(-9),
                    ShippingDurationMinutes = 120,
                    InternalWeighbridgeCode = "WB-INT-TEST-02",
                    ExternalWeighbridgeCode = "WB-EXT-TEST-02",
                    Notes = "حركة نقل تجريبية ثانية مطابقة تماماً دون هالك",
                    DateOfSubmit = DateTime.UtcNow.AddHours(-8)
                };

                var transTx3 = new Transportation
                {
                    TransactionNumber = 8803,
                    TransactionDate = DateTime.UtcNow.AddHours(-2),
                    CarNumber = "ت س ت 9999",
                    DriverName = "سائق تجريبي - حسن إبراهيم",
                    Source = "صوامع السويس الحديثة",
                    PONumber = "TEST-PO-002",
                    CompanyName = "TEST-SUPPLIER-002",
                    MaterialType = "إسمنت بورتلاندي فائق",
                    ExpectedWeight = 50.000m,
                    WeightBeforeLoading = 32.800m,
                    WeightWhenEnteringFactory = 82.000m,
                    WeightWhenExitingFactory = 32.800m,
                    InternalWeight = 49.200m,
                    ExternalLoadedWeight = 82.800m,
                    ExternalEmptyWeight = 32.800m,
                    ExternalNetWeight = 50.000m,
                    NetWeight = 49.200m,
                    WasteQuantity = 0.800m,
                    Destination = "صوامع الإسمنت الرئيسية",
                    TransactionStatus = "InTransit",
                    ReceivingStatus = "قيد المراجعة",
                    DepartureTime = DateTime.UtcNow.AddHours(-3),
                    ArrivalTime = DateTime.UtcNow.AddHours(-2),
                    ShippingDurationMinutes = 60,
                    InternalWeighbridgeCode = "WB-INT-TEST-03",
                    ExternalWeighbridgeCode = "WB-EXT-TEST-03",
                    Notes = "شحنة تجريبية قيد المراجعة والاستلام",
                    DateOfSubmit = DateTime.UtcNow.AddHours(-2)
                };

                await db.Transportations.AddRangeAsync(new[] { transTx1, transTx2, transTx3 }, cts.Token);
            }

            // Fake Storage Transactions
            if (!await db.StorageTransactions.AnyAsync(s => s.PONumber == "TEST-PO-001", cts.Token))
            {
                logger.LogInformation("Seeding fake storage transactions...");
                var st1 = new StorageTransaction
                {
                    MovementNumber = 8901,
                    TransactionDate = DateTime.UtcNow.AddHours(-16),
                    Action = "Import",
                    ServiceType = "إضافة",
                    ReferenceNumber = 8901,
                    PONumber = "TEST-PO-001",
                    Material = "سن وركام خرساني تدرج 1",
                    IncomingQuantity = 180.000m,
                    OutgoingQuantity = 0.000m,
                    OpeningBalance = 200.000m,
                    ClosingBalance = 380.000m,
                    StorageLocation = "مستودع الخامات الرئيسي - قطاع C",
                    ReceivingEmployee = "أمين مخزن تجريبي - هاني شاكر",
                    InternalWeighbridgeReference = "WB-REF-TEST-01",
                    InternalLoadedWeight = 216.500m,
                    InternalEmptyWeight = 36.500m,
                    InternalNetWeight = 180.000m,
                    HasExternalWeighbridge = true,
                    ExternalWeighbridgeReference = "WB-EXT-REF-01",
                    ExternalLoadedWeight = 216.500m,
                    ExternalEmptyWeight = 36.500m,
                    ExternalNetWeight = 180.000m,
                    IsExportRelatedToPO = false,
                    Notes = "إيداع مخزني تجريبي معتمد مرتبط بأمر الشراء TEST-PO-001",
                    CreatedAt = DateTime.UtcNow.AddHours(-16)
                };

                var st2 = new StorageTransaction
                {
                    MovementNumber = 8902,
                    TransactionDate = DateTime.UtcNow.AddHours(-6),
                    Action = "Export",
                    ServiceType = "صرف",
                    ReferenceNumber = 8902,
                    PONumber = "TEST-PO-001",
                    Material = "سن وركام خرساني تدرج 1",
                    IncomingQuantity = 0.000m,
                    OutgoingQuantity = 40.000m,
                    OpeningBalance = 380.000m,
                    ClosingBalance = 340.000m,
                    StorageLocation = "مستودع الخامات الرئيسي - قطاع C",
                    ReceivingEmployee = "أمين مخزن تجريبي - هاني شاكر",
                    InternalWeighbridgeReference = "WB-REF-TEST-02",
                    InternalLoadedWeight = 74.000m,
                    InternalEmptyWeight = 34.000m,
                    InternalNetWeight = 40.000m,
                    HasExternalWeighbridge = false,
                    IsExportRelatedToPO = true,
                    Notes = "صرف مخزني تجريبي لصالح خط خلط الخرسانة الجاهزة مرتبط بأمر الشراء TEST-PO-001",
                    CreatedAt = DateTime.UtcNow.AddHours(-6)
                };

                var st3 = new StorageTransaction
                {
                    MovementNumber = 8903,
                    TransactionDate = DateTime.UtcNow.AddHours(-2),
                    Action = "Import",
                    ServiceType = "إضافة",
                    ReferenceNumber = 8903,
                    PONumber = null,
                    Material = "إضافات خرسانية كيميائية",
                    IncomingQuantity = 25.000m,
                    OutgoingQuantity = 0.000m,
                    OpeningBalance = 50.000m,
                    ClosingBalance = 75.000m,
                    StorageLocation = "مستودع الكيماويات والمحسنات",
                    ReceivingEmployee = "مسؤول المستودع - خالد سالم",
                    InternalWeighbridgeReference = "WB-REF-TEST-03",
                    InternalLoadedWeight = 55.000m,
                    InternalEmptyWeight = 30.000m,
                    InternalNetWeight = 25.000m,
                    HasExternalWeighbridge = false,
                    IsExportRelatedToPO = false,
                    Notes = "وارد مخزني مستقل غير مربوط بأمر شراء",
                    CreatedAt = DateTime.UtcNow.AddHours(-2)
                };

                await db.StorageTransactions.AddRangeAsync(new[] { st1, st2, st3 }, cts.Token);
            }

            // Fake Finance Records (Payment, Expense, Bank Transaction)
            if (!await db.FinancePayments.AnyAsync(fp => fp.PONumber == "TEST-PO-001", cts.Token))
            {
                logger.LogInformation("Seeding fake finance payments and expenses...");
                await db.FinancePayments.AddAsync(new FinancePayment
                {
                    PONumber = "TEST-PO-001",
                    Amount = 270000.00m,
                    PaymentDate = DateTime.UtcNow.AddDays(-1),
                    PaidBy = "المحاسب المالي",
                    PaymentMethod = "تحويل بنكي",
                    Notes = "سداد دفعة نقدية بنكية عن كمية 180 طن مستلمة بأمر الشراء TEST-PO-001",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }, cts.Token);
            }

            if (!await db.Expenses.AnyAsync(e => e.ReferenceNumber == "CHK-TEST-001", cts.Token))
            {
                await db.Expenses.AddAsync(new Expense
                {
                    ExpenseDate = DateTime.UtcNow.AddDays(-2),
                    Category = "النقل واللوجستيات",
                    Amount = 14500.00m,
                    Title = "مصاريف شحن واختبارات معملية - TEST-EXP-001",
                    Beneficiary = "معمل ضبط الجودة الهندسي",
                    PaymentMethod = "شيك",
                    ReferenceNumber = "CHK-TEST-001",
                    CreatedBy = "sarah@facestone.com",
                    Notes = "سداد تكاليف فحص وضبط جودة الركام والشحنات",
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                }, cts.Token);
            }

            if (!await db.BankTransactions.AnyAsync(b => b.ReferenceNumber == "TRF-TEST-8891", cts.Token))
            {
                await db.BankTransactions.AddAsync(new BankTransaction
                {
                    TransactionDate = DateTime.UtcNow.AddDays(-1),
                    BankName = "البنك الأهلي المصري",
                    TransactionType = "تحويل بنكي",
                    Direction = "Out",
                    Amount = 270000.00m,
                    Description = "سداد دفعة للمورد TEST-SUPPLIER-001 عن أمر الشراء TEST-PO-001",
                    ReferenceNumber = "TRF-TEST-8891",
                    CreatedBy = "sarah@facestone.com",
                    Notes = "معاملة بنكية صادرة مؤكدة بالإشعار البنكي",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }, cts.Token);
            }

            // Fake Employees & Financial / Attendance / Lending Records
            var testEmpId1 = Guid.Parse("11111111-2222-3333-4444-555555555551");
            if (!await db.Users.AnyAsync(u => u.Id == testEmpId1, cts.Token))
            {
                logger.LogInformation("Seeding fake employee accounts and HR/Lending data...");
                var testUser1 = new UserAccount
                {
                    Id = testEmpId1,
                    FullName = "المهندس أسامة عبد الله - TEST",
                    Username = "test_engineer",
                    Email = "test.engineer@facestone.com",
                    PhoneNumber = "+201099990001",
                    Role = "SENIOR STRUCTURAL ENGINEER",
                    PasswordHash = HashPassword("TestPass123!"),
                    CreatedAt = DateTime.UtcNow.AddMonths(-6)
                };
                await db.Users.AddAsync(testUser1, cts.Token);

                await db.EmployeeFinancialRecords.AddAsync(new EmployeeFinancialRecord
                {
                    UserId = testEmpId1,
                    BasicSalary = 18000.00m,
                    NetSalary = 17200.00m,
                    Allowances = 1200.00m,
                    InsuranceDeductions = 2000.00m,
                    UpdatedAt = DateTime.UtcNow.AddDays(-1)
                }, cts.Token);

                await db.EmployeeLendings.AddAsync(new EmployeeLending
                {
                    UserId = testEmpId1,
                    LendingAmount = 10000.00m,
                    PaidAmount = 4000.00m,
                    RemainingAmount = 6000.00m,
                    RequestDate = DateTime.UtcNow.AddDays(-20),
                    ApprovalStatus = "Approved",
                    ApprovedBy = "الرئيس التنفيذي",
                    Notes = "سلفة شخصية بخصم شهري 1000 ج.م",
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                }, cts.Token);

                for (int i = 1; i <= 20; i++)
                {
                    await db.AttendanceRecords.AddAsync(new AttendanceRecord
                    {
                        UserId = testEmpId1,
                        Date = DateTime.UtcNow.Date.AddDays(-i),
                        Status = i == 5 ? "Absent" : "Present",
                        CheckInTime = DateTime.UtcNow.Date.AddDays(-i).AddHours(8).AddMinutes(15),
                        CheckOutTime = DateTime.UtcNow.Date.AddDays(-i).AddHours(16).AddMinutes(30),
                        Notes = i == 5 ? "إجازة اعتيادية معتمدة" : "حضور منتظم"
                    }, cts.Token);
                }
            }

            var testEmpId2 = Guid.Parse("11111111-2222-3333-4444-555555555552");
            if (!await db.Users.AnyAsync(u => u.Id == testEmpId2, cts.Token))
            {
                var testUser2 = new UserAccount
                {
                    Id = testEmpId2,
                    FullName = "فني التشغيل عادل مراد - TEST",
                    Username = "test_operator",
                    Email = "test.operator@facestone.com",
                    PhoneNumber = "+201099990002",
                    Role = "Employee",
                    PasswordHash = HashPassword("TestPass123!"),
                    CreatedAt = DateTime.UtcNow.AddMonths(-4)
                };
                await db.Users.AddAsync(testUser2, cts.Token);

                await db.EmployeeFinancialRecords.AddAsync(new EmployeeFinancialRecord
                {
                    UserId = testEmpId2,
                    BasicSalary = 9500.00m,
                    NetSalary = 9100.00m,
                    Allowances = 600.00m,
                    InsuranceDeductions = 1000.00m,
                    UpdatedAt = DateTime.UtcNow.AddDays(-1)
                }, cts.Token);

                for (int i = 1; i <= 20; i++)
                {
                    await db.AttendanceRecords.AddAsync(new AttendanceRecord
                    {
                        UserId = testEmpId2,
                        Date = DateTime.UtcNow.Date.AddDays(-i),
                        Status = "Present",
                        CheckInTime = DateTime.UtcNow.Date.AddDays(-i).AddHours(8),
                        CheckOutTime = DateTime.UtcNow.Date.AddDays(-i).AddHours(16),
                        Notes = "حضور منتظم"
                    }, cts.Token);
                }
            }

            // ========================================================================
            // CONTROL & MONITORING TEST DATA (Requirement 18: Clearly identifiable TEST data)
            // - TEST-PO-CM-001
            // - One pending Transportation form (TEST-TRANSPORT-001)
            // - One pending Storage form (TEST-STORAGE-001)
            // - One approved form (TEST-TRANSPORT-002 / APPROVED)
            // - One rejected form (TEST-TRANSPORT-003 / REJECTED with reason)
            // ========================================================================
            if (!await db.PurchaseOrders.AnyAsync(p => p.PONumber == "TEST-PO-CM-001", cts.Token))
            {
                logger.LogInformation("Seeding Control & Monitoring test purchase order and verification records...");
                var cmPo = new PurchaseOrder
                {
                    PONumber = "TEST-PO-CM-001",
                    Supplier = "شركة السويس للركام والمحاجر (مورد تجريبي)",
                    OrderDate = DateTime.UtcNow.AddDays(-3),
                    Quantity = 500.000m,
                    RemainingQuantity = 475.000m,
                    Destination = "مصنع حلوان - قطاع ج",
                    PricePerTon = 1600.00m,
                    TotalValue = 800000.00m,
                    PaymentStatus = "غير مدفوع",
                    InvoiceNumber = "INV-CM-TEST-001",
                    Notes = "أمر شراء تجريبي مخصص للتحقق من تدفقات الرقابة والمتابعة",
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                };
                await db.PurchaseOrders.AddAsync(cmPo, cts.Token);
                await db.SaveChangesAsync(cts.Token);
            }

            // 1. One Pending Transportation Form (TEST-TRANSPORT-001)
            if (!await db.Transportations.AnyAsync(t => t.PONumber == "TEST-PO-CM-001" && t.TransactionNumber == 9901, cts.Token))
            {
                var transPending = new Transportation
                {
                    TransactionNumber = 9901,
                    TransactionDate = DateTime.UtcNow.AddHours(-3),
                    CarNumber = "ن ق ل 1001",
                    DriverName = "أحمد رضوان (سائق تجريبي)",
                    Source = "محجر السويس قطاع 2",
                    PONumber = "TEST-PO-CM-001",
                    CompanyName = "شركة السويس للركام والمحاجر (مورد تجريبي)",
                    MaterialType = "سن وركام خرساني تدرج 1",
                    ExpectedWeight = 25.000m,
                    WeightBeforeLoading = 15.000m,
                    WeightWhenEnteringFactory = 40.000m,
                    WeightWhenExitingFactory = 15.000m,
                    InternalWeight = 25.000m,
                    ExternalLoadedWeight = 40.000m,
                    ExternalEmptyWeight = 15.000m,
                    ExternalNetWeight = 25.000m,
                    NetWeight = 25.000m,
                    WasteQuantity = 0.000m,
                    Destination = "مصنع حلوان - قطاع ج",
                    TransactionStatus = "Pending",
                    ReceivingStatus = "قيد المراجعة",
                    DepartureTime = DateTime.UtcNow.AddHours(-4),
                    ArrivalTime = DateTime.UtcNow.AddHours(-3),
                    ShippingDurationMinutes = 60,
                    InternalWeighbridgeCode = "WB-INT-CM-01",
                    ExternalWeighbridgeCode = "WB-EXT-CM-01",
                    ApprovalStatus = "PENDING",
                    SubmittedBy = "transport_manager",
                    Notes = "نموذج نقل تجريبي قيد المراجعة (TEST-TRANSPORT-001)",
                    DateOfSubmit = DateTime.UtcNow.AddHours(-3)
                };
                await db.Transportations.AddAsync(transPending, cts.Token);
                await db.SaveChangesAsync(cts.Token);

                await db.FormApprovalHistories.AddAsync(new FormApprovalHistory
                {
                    FormType = "Transportation",
                    FormId = transPending.Id,
                    PONumber = transPending.PONumber,
                    TransactionOrMovementNumber = transPending.TransactionNumber.ToString(),
                    PreviousStatus = "NONE",
                    NewStatus = "PENDING",
                    Action = "SUBMITTED",
                    ActorName = "Transportation Manager",
                    ActorRole = "TRANSPORTATION_MANAGER",
                    Comment = "تم إرسال نموذج النقل بانتظار مراجعة قسم التحكم والمراقبة",
                    Timestamp = DateTime.UtcNow.AddHours(-3)
                }, cts.Token);
            }

            // 2. One Pending Storage Form (TEST-STORAGE-001)
            if (!await db.StorageTransactions.AnyAsync(s => s.PONumber == "TEST-PO-CM-001" && s.MovementNumber == 9902, cts.Token))
            {
                var storagePending = new StorageTransaction
                {
                    MovementNumber = 9902,
                    TransactionDate = DateTime.UtcNow.AddHours(-2),
                    Action = "Import",
                    ServiceType = "إضافة",
                    ReferenceNumber = 9902,
                    PONumber = "TEST-PO-CM-001",
                    Material = "سن وركام خرساني تدرج 1",
                    IncomingQuantity = 25.000m,
                    OutgoingQuantity = 0.000m,
                    OpeningBalance = 100.000m,
                    ClosingBalance = 125.000m,
                    StorageLocation = "مستودع الخامات الرئيسي - قطاع C",
                    ReceivingEmployee = "أمين المخزن التجريبي",
                    InternalWeighbridgeReference = "WB-REF-CM-02",
                    InternalLoadedWeight = 40.000m,
                    InternalEmptyWeight = 15.000m,
                    InternalNetWeight = 25.000m,
                    HasExternalWeighbridge = false,
                    IsExportRelatedToPO = false,
                    ApprovalStatus = "PENDING",
                    CreatedBy = "storage_manager",
                    Notes = "نموذج مخزن تجريبي قيد المراجعة (TEST-STORAGE-001)",
                    CreatedAt = DateTime.UtcNow.AddHours(-2)
                };
                await db.StorageTransactions.AddAsync(storagePending, cts.Token);
                await db.SaveChangesAsync(cts.Token);

                await db.FormApprovalHistories.AddAsync(new FormApprovalHistory
                {
                    FormType = "Storage",
                    FormId = storagePending.Id,
                    PONumber = storagePending.PONumber,
                    TransactionOrMovementNumber = storagePending.MovementNumber.ToString(),
                    PreviousStatus = "NONE",
                    NewStatus = "PENDING",
                    Action = "SUBMITTED",
                    ActorName = "Storage Manager",
                    ActorRole = "STORAGE_MANAGER",
                    Comment = "تم إرسال حركة المستودع بانتظار مراجعة قسم التحكم والمراقبة",
                    Timestamp = DateTime.UtcNow.AddHours(-2)
                }, cts.Token);
            }

            // 3. One Approved Form (Transportation)
            if (!await db.Transportations.AnyAsync(t => t.PONumber == "TEST-PO-CM-001" && t.TransactionNumber == 9903, cts.Token))
            {
                var transApproved = new Transportation
                {
                    TransactionNumber = 9903,
                    TransactionDate = DateTime.UtcNow.AddHours(-5),
                    CarNumber = "ن ق ل 1002",
                    DriverName = "طارق سليم (سائق تجريبي)",
                    Source = "محجر السويس قطاع 2",
                    PONumber = "TEST-PO-CM-001",
                    CompanyName = "شركة السويس للركام والمحاجر (مورد تجريبي)",
                    MaterialType = "سن وركام خرساني تدرج 1",
                    ExpectedWeight = 25.000m,
                    WeightBeforeLoading = 14.500m,
                    WeightWhenEnteringFactory = 39.500m,
                    WeightWhenExitingFactory = 14.500m,
                    InternalWeight = 25.000m,
                    ExternalLoadedWeight = 39.500m,
                    ExternalEmptyWeight = 14.500m,
                    ExternalNetWeight = 25.000m,
                    NetWeight = 25.000m,
                    WasteQuantity = 0.000m,
                    Destination = "مصنع حلوان - قطاع ج",
                    TransactionStatus = "Approved",
                    ReceivingStatus = "Approved / Accepted",
                    DepartureTime = DateTime.UtcNow.AddHours(-6),
                    ArrivalTime = DateTime.UtcNow.AddHours(-5),
                    ShippingDurationMinutes = 60,
                    InternalWeighbridgeCode = "WB-INT-CM-03",
                    ExternalWeighbridgeCode = "WB-EXT-CM-03",
                    ApprovalStatus = "APPROVED",
                    ReviewedBy = "Control & Monitoring Employee",
                    ReviewedAt = DateTime.UtcNow.AddHours(-4),
                    SubmittedBy = "transport_manager",
                    Notes = "نموذج نقل تجريبي معتمد مسبقاً (TEST-TRANSPORT-002)",
                    DateOfSubmit = DateTime.UtcNow.AddHours(-5)
                };
                await db.Transportations.AddAsync(transApproved, cts.Token);
                await db.SaveChangesAsync(cts.Token);

                await db.FormApprovalHistories.AddRangeAsync(new[]
                {
                    new FormApprovalHistory
                    {
                        FormType = "Transportation",
                        FormId = transApproved.Id,
                        PONumber = transApproved.PONumber,
                        TransactionOrMovementNumber = transApproved.TransactionNumber.ToString(),
                        PreviousStatus = "NONE",
                        NewStatus = "PENDING",
                        Action = "SUBMITTED",
                        ActorName = "Transportation Manager",
                        ActorRole = "TRANSPORTATION_MANAGER",
                        Comment = "تم إرسال نموذج النقل",
                        Timestamp = DateTime.UtcNow.AddHours(-5)
                    },
                    new FormApprovalHistory
                    {
                        FormType = "Transportation",
                        FormId = transApproved.Id,
                        PONumber = transApproved.PONumber,
                        TransactionOrMovementNumber = transApproved.TransactionNumber.ToString(),
                        PreviousStatus = "PENDING",
                        NewStatus = "APPROVED",
                        Action = "ACCEPTED",
                        ActorName = "Control & Monitoring Employee",
                        ActorRole = "CONTROL_MONITORING",
                        Comment = "تم التحقق ومطابقة أوزان التذاكر مع المرفقات والموافقة",
                        Timestamp = DateTime.UtcNow.AddHours(-4)
                    }
                }, cts.Token);
            }

            // 4. One Rejected Form (Transportation with rejection reason)
            if (!await db.Transportations.AnyAsync(t => t.PONumber == "TEST-PO-CM-001" && t.TransactionNumber == 9904, cts.Token))
            {
                var transRejected = new Transportation
                {
                    TransactionNumber = 9904,
                    TransactionDate = DateTime.UtcNow.AddHours(-4),
                    CarNumber = "ن ق ل 1003",
                    DriverName = "سالم فؤاد (سائق تجريبي)",
                    Source = "محجر السويس قطاع 2",
                    PONumber = "TEST-PO-CM-001",
                    CompanyName = "شركة السويس للركام والمحاجر (مورد تجريبي)",
                    MaterialType = "سن وركام خرساني تدرج 1",
                    ExpectedWeight = 30.000m,
                    WeightBeforeLoading = 16.000m,
                    WeightWhenEnteringFactory = 47.000m,
                    WeightWhenExitingFactory = 16.000m,
                    InternalWeight = 31.000m,
                    ExternalLoadedWeight = 46.000m,
                    ExternalEmptyWeight = 16.000m,
                    ExternalNetWeight = 30.000m,
                    NetWeight = 31.000m,
                    WasteQuantity = 1.000m,
                    Destination = "مصنع حلوان - قطاع ج",
                    TransactionStatus = "Rejected",
                    ReceivingStatus = "مرفوض",
                    DepartureTime = DateTime.UtcNow.AddHours(-5),
                    ArrivalTime = DateTime.UtcNow.AddHours(-4),
                    ShippingDurationMinutes = 60,
                    InternalWeighbridgeCode = "WB-INT-CM-04",
                    ExternalWeighbridgeCode = "WB-EXT-CM-04",
                    ApprovalStatus = "REJECTED",
                    ReviewedBy = "Control & Monitoring Employee",
                    ReviewedAt = DateTime.UtcNow.AddHours(-3),
                    RejectionReason = "وزن البوليصة لا يطابق المرفق.",
                    SubmittedBy = "transport_manager",
                    Notes = "نموذج نقل تجريبي مرفوض للتصحيح (TEST-TRANSPORT-003)",
                    DateOfSubmit = DateTime.UtcNow.AddHours(-4)
                };
                await db.Transportations.AddAsync(transRejected, cts.Token);
                await db.SaveChangesAsync(cts.Token);

                await db.FormApprovalHistories.AddRangeAsync(new[]
                {
                    new FormApprovalHistory
                    {
                        FormType = "Transportation",
                        FormId = transRejected.Id,
                        PONumber = transRejected.PONumber,
                        TransactionOrMovementNumber = transRejected.TransactionNumber.ToString(),
                        PreviousStatus = "NONE",
                        NewStatus = "PENDING",
                        Action = "SUBMITTED",
                        ActorName = "Transportation Manager",
                        ActorRole = "TRANSPORTATION_MANAGER",
                        Comment = "تم إرسال نموذج النقل",
                        Timestamp = DateTime.UtcNow.AddHours(-4)
                    },
                    new FormApprovalHistory
                    {
                        FormType = "Transportation",
                        FormId = transRejected.Id,
                        PONumber = transRejected.PONumber,
                        TransactionOrMovementNumber = transRejected.TransactionNumber.ToString(),
                        PreviousStatus = "PENDING",
                        NewStatus = "REJECTED",
                        Action = "REJECTED",
                        ActorName = "Control & Monitoring Employee",
                        ActorRole = "CONTROL_MONITORING",
                        Comment = "وزن البوليصة لا يطابق المرفق.",
                        Timestamp = DateTime.UtcNow.AddHours(-3)
                    }
                }, cts.Token);
            }
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

    /// <summary>
    /// Seeds safe, cohesive, idempotent test records across all departments prefixed with TEST-.
    /// Safe for production test runs - never overwrites real data and cleanly removable.
    /// <summary>
    /// Seeds safe, isolated, and completely idempotent test data prefixed with TEST-
    /// across all FaceStone operational departments.
    /// Can be called on startup or via admin API (/api/management/db/seed-test-data).
    /// </summary>
    public static async Task<Dictionary<string, object>> SeedSafeTestDataAsync(FaceStoneDbContext db, ILogger logger)
    {
        var summary = new Dictionary<string, object>();
        var now = DateTime.UtcNow;

        // 1. Raw Materials
        var rawLimestone = await db.RawMaterials.FirstOrDefaultAsync(r => r.Name == "TEST-حجر جيري نقي (Limestone Powder)");
        if (rawLimestone == null)
        {
            rawLimestone = new RawMaterial
            {
                Name = "TEST-حجر جيري نقي (Limestone Powder)",
                InitialQuantity = 450.500m,
                MaterialType = MaterialProcessType.Mechanical,
                CreatedAt = now,
                CreatedBy = "System Seeder"
            };
            await db.RawMaterials.AddAsync(rawLimestone);
        }

        var rawSilica = await db.RawMaterials.FirstOrDefaultAsync(r => r.Name == "TEST-سيليكا ميكرو باودر (Micro Silica)");
        if (rawSilica == null)
        {
            rawSilica = new RawMaterial
            {
                Name = "TEST-سيليكا ميكرو باودر (Micro Silica)",
                InitialQuantity = 180.250m,
                MaterialType = MaterialProcessType.Chemical,
                CreatedAt = now,
                CreatedBy = "System Seeder"
            };
            await db.RawMaterials.AddAsync(rawSilica);
        }
        await db.SaveChangesAsync();

        // 2. Storage Locations
        var locYard = await db.StorageLocations.FirstOrDefaultAsync(l => l.Code == "TEST-YARD-A1");
        if (locYard == null)
        {
            locYard = new StorageLocation
            {
                Code = "TEST-YARD-A1",
                Name = "ساحة التخزين الرئيسية - قطاع A1",
                Description = "TEST-موقع تخزين تجريبي",
                IsActive = true,
                CreatedAt = now
            };
            await db.StorageLocations.AddAsync(locYard);
        }

        var locSilo = await db.StorageLocations.FirstOrDefaultAsync(l => l.Code == "TEST-SILO-02");
        if (locSilo == null)
        {
            locSilo = new StorageLocation
            {
                Code = "TEST-SILO-02",
                Name = "صومعة التخزين الجاف 2",
                Description = "TEST-صومعة تخزين اسمنت ومواد ناعمة",
                IsActive = true,
                CreatedAt = now
            };
            await db.StorageLocations.AddAsync(locSilo);
        }
        await db.SaveChangesAsync();

        // 3. Storage Client & Destination
        var client = await db.StorageClients.Include(c => c.Destinations).FirstOrDefaultAsync(c => c.Name == "TEST-شركة المقاولون العرب للإنشاءات");
        if (client == null)
        {
            client = new StorageClient
            {
                Name = "TEST-شركة المقاولون العرب للإنشاءات",
                PhoneNumber = "01012345678",
                IsActive = true,
                CreatedAt = now,
                Destinations = new List<StorageClientDestination>
                {
                    new StorageClientDestination
                    {
                        DestinationName = "مشروع مونوريل العاصمة الإدارية",
                        IsActive = true,
                        CreatedAt = now
                    }
                }
            };
            await db.StorageClients.AddAsync(client);
            await db.SaveChangesAsync();
        }

        // 4. Purchase Order
        var po = await db.PurchaseOrders.FirstOrDefaultAsync(p => p.PONumber == "TEST-PO-2026-001");
        if (po == null)
        {
            po = new PurchaseOrder
            {
                PONumber = "TEST-PO-2026-001",
                Supplier = "TEST-شركة محاجر سيناء للمواد الخام",
                Quantity = 100.000m,
                RemainingQuantity = 65.500m,
                PricePerTon = 1450.00m,
                TonPrice = 1450.00m,
                TotalValue = 145000.00m,
                OrderDate = now.AddDays(-3),
                PaymentStatus = "غير مدفوع",
                Destination = "مصنع فيستون بدر",
                Notes = "TEST-أمر شراء تجريبي لاختبار دورة التوريد",
                CreatedAt = now.AddDays(-3)
            };
            await db.PurchaseOrders.AddAsync(po);
            await db.SaveChangesAsync();
        }

        // 5. Transportation Movement
        var trans = await db.Transportations.FirstOrDefaultAsync(t => t.TransactionNumber == 99001);
        if (trans == null)
        {
            trans = new Transportation
            {
                TransactionNumber = 99001,
                TransactionDate = now.AddDays(-2),
                CarNumber = "ق هـ د 4821",
                DriverName = "محمود أحمد حسن",
                Source = "محاجر السويس",
                PONumber = "TEST-PO-2026-001",
                MaterialType = "حجر جيري نقي",
                WeightBeforeLoading = 13.700m,
                WeightWhenEnteringFactory = 48.200m,
                WeightWhenExitingFactory = 13.700m,
                ExpectedWeight = 34.500m,
                NetWeight = 34.500m,
                InternalWeight = 34.500m,
                ExternalLoadedWeight = 48.200m,
                ExternalEmptyWeight = 13.700m,
                ExternalNetWeight = 34.500m,
                WasteQuantity = 0.000m,
                CompanyName = "TEST-شركة محاجر سيناء للمواد الخام",
                Destination = "مصنع فيستون بدر",
                TransactionStatus = "مكتملة",
                ReceivingStatus = "Approved / Accepted",
                Notes = "TEST-حركة نقل شاحنة تجريبية",
                ProductionReferenceNumber = 88001
            };
            await db.Transportations.AddAsync(trans);
            await db.SaveChangesAsync();
        }

        // 6. Storage Operation
        var storageOp = await db.StorageOperations.FirstOrDefaultAsync(o => o.TransactionNumber == 99001);
        if (storageOp == null)
        {
            storageOp = new StorageOperation
            {
                TransactionNumber = 99001,
                OperationType = "IMPORT",
                TransactionDate = now.AddDays(-2),
                MaterialType = MaterialProcessType.Mechanical,
                StorageLocationId = locYard.Id,
                StorageLocationName = locYard.Name,
                ClientId = client.Id,
                ClientName = client.Name,
                InternalNetWeight = 34.500m,
                InternalLoadedWeight = 48.200m,
                InternalEmptyWeight = 13.700m,
                ExternalReportedWeight = 34.500m,
                ProductionReferenceNumber = 88001,
                ApprovalStatus = "APPROVED",
                IsInventoryPosted = true,
                ResponsibleEmployee = "مسؤول استلام المخزن",
                Notes = "TEST-إذن استلام مخزني تجريبي",
                CreatedBy = "storage.employee@facestone.com",
                CreatedAt = now.AddDays(-2),
                Items = new List<StorageOperationItem>
                {
                    new StorageOperationItem
                    {
                        RawMaterialId = rawLimestone.Id,
                        MaterialName = rawLimestone.Name,
                        Quantity = 34.500m,
                        TrackingNumber = "TEST-TRK-88001",
                        Notes = "TEST-دفعة استلام توريد أولي"
                    }
                }
            };
            await db.StorageOperations.AddAsync(storageOp);
            await db.SaveChangesAsync();
        }

        // 7. Laboratory Raw Material Report
        var labRaw = await db.LaboratoryRawMaterialReports.FirstOrDefaultAsync(r => r.ProductionReferenceNumber == 88001);
        if (labRaw == null)
        {
            labRaw = new LaboratoryRawMaterialReport
            {
                ProductionReferenceNumber = 88001,
                SampleNumber = 99001,
                SampleName = "TEST-حجر جيري نقي (Limestone Powder)",
                SampleDescription = "عينة بودرة بيضاء ناعمة متجانسة",
                BatchNumber = "TEST-BATCH-2026-01",
                RawMaterialId = rawLimestone.Id,
                SampleReceivedDate = now.AddDays(-2),
                ReportIssueDate = now.AddDays(-2),
                LaboratorySection = "PHYSICAL",
                ComplyStatus = "COMPLY",
                InspectionDecision = "ACCEPTED",
                ReceivedQuantity = 34.500m,
                AcceptedQuantity = 34.500m,
                RejectedQuantity = 0.000m,
                Notes = "TEST-مطابقة للمواصفة القياسية المصرية ES 4756-1",
                LabManagerName = "كيميائي/ رامي إبراهيم",
                ResponsibleEmployee = "كيميائي/ رامي إبراهيم",
                ApprovalStatus = "APPROVED",
                CreatedBy = "phys.lab@facestone.com",
                CreatedAt = now.AddDays(-2)
            };
            await db.LaboratoryRawMaterialReports.AddAsync(labRaw);
            await db.SaveChangesAsync();
        }

        // 8. Production Order, Batch & Output
        var prodOrder = await db.ProductionOrders.FirstOrDefaultAsync(o => o.OrderNumber == "TEST-ORD-2026-001");
        if (prodOrder == null)
        {
            prodOrder = new ProductionOrder
            {
                OrderNumber = "TEST-ORD-2026-001",
                ProductionReferenceNumber = 88001,
                ProductName = "انترلوك سداسي رمادي 6سم",
                RequestedQuantity = 50.000m,
                Unit = "طن",
                StorageLocationId = locYard.Id,
                StorageLocationName = locYard.Name,
                Status = "COMPLETED",
                CurrentStage = "المرحلة النهائية",
                ResponsibleEmployee = "مهندس/ سامح عبد الله",
                PlannedStartDate = now.AddDays(-1),
                ActualStartDate = now.AddDays(-1),
                CompletedDate = now.AddDays(-1),
                Notes = "TEST-أمر إنتاج تجريبي مكتمل",
                CreatedAt = now.AddDays(-1),
                CreatedBy = "prod.manager@facestone.com"
            };
            await db.ProductionOrders.AddAsync(prodOrder);
            await db.SaveChangesAsync();

            var prodBatch = new ProductionBatch
            {
                ProductionOrderId = prodOrder.Id,
                BatchNumber = "TEST-BATCH-2026-01",
                ProductionReferenceNumber = 88001,
                CurrentStage = "المرحلة النهائية",
                Status = "COMPLETED",
                QualityHold = false,
                TotalInputQuantity = 34.500m,
                TotalOutputQuantity = 34.000m,
                TotalWasteQuantity = 0.500m,
                ResponsibleEmployee = "مهندس/ سامح عبد الله",
                StartedAt = now.AddDays(-1),
                CompletedAt = now.AddDays(-1),
                Notes = "TEST-تشغيلة إنتاج تجريبية",
                CreatedBy = "prod.manager@facestone.com",
                CreatedAt = now.AddDays(-1)
            };
            await db.ProductionBatches.AddAsync(prodBatch);
            await db.SaveChangesAsync();

            var prodOut = new ProductionOutput
            {
                OutputNumber = "TEST-OUT-2026-01",
                ProductionOrderId = prodOrder.Id,
                ProductionBatchId = prodBatch.Id,
                ProductName = "انترلوك سداسي رمادي 6سم",
                Classification = "FINISHED",
                Quantity = 34.000m,
                Unit = "طن",
                ProductionStage = "المرحلة النهائية",
                ResponsibleEmployee = "مهندس/ سامح عبد الله",
                LaboratoryConformityStatus = "CONFORMS",
                Notes = "TEST-مخرجات إنتاج تجريبية تامة الصنع",
                CreatedBy = "prod.manager@facestone.com",
                CreatedAt = now.AddDays(-1)
            };
            await db.ProductionOutputs.AddAsync(prodOut);
            await db.SaveChangesAsync();
        }

        // 9. Laboratory Final Certificate
        var labCert = await db.LaboratoryFinalConformityCertificates.FirstOrDefaultAsync(c => c.CertificateNumber == "TEST-CERT-2026-01");
        if (labCert == null)
        {
            labCert = new LaboratoryFinalConformityCertificate
            {
                CertificateNumber = "TEST-CERT-2026-01",
                ProductionReferenceNumber = 88001,
                FinalProductName = "انترلوك سداسي رمادي 6سم",
                ActualFinalProductQuantity = 34.000m,
                QuantityUnit = "طن",
                ProductionBatchNumber = "TEST-BATCH-2026-01",
                ActualRawMaterialInputQuantity = 34.500m,
                CalculatedDifference = 0.500m,
                ActualMeasuredWaste = 0.500m,
                WasteClassification = "ORDINARY_WASTE",
                ConformityDecision = "CONFORMS",
                LaboratorySection = "PHYSICAL",
                Observations = "TEST-شهادة مطابقة نهائية معتمدة",
                ResponsibleEmployee = "كيميائي/ رامي إبراهيم",
                ApprovalStatus = "APPROVED",
                CreatedBy = "phys.lab@facestone.com",
                CreatedAt = now.AddDays(-1)
            };
            await db.LaboratoryFinalConformityCertificates.AddAsync(labCert);
            await db.SaveChangesAsync();
        }

        // 10. Daily Production Submission
        var dps = await db.DailyProductionSubmissions.FirstOrDefaultAsync(s => s.SubmissionNumber == "TEST-DPS-2026-001");
        if (dps == null)
        {
            dps = new DailyProductionSubmission
            {
                SubmissionNumber = "TEST-DPS-2026-001",
                ProductionDate = now.Date,
                EmployeeId = Guid.NewGuid(),
                EmployeeName = "مهندس/ سامح عبد الله",
                EmployeeEmail = "prod.manager@facestone.com",
                Shift = "الوردية الأولى (الصباحية)",
                ShiftStart = now.Date.AddHours(8),
                ShiftEnd = now.Date.AddHours(16),
                DurationHours = 8.0m,
                TotalGrossQuantity = 75.000m,
                WasteQuantity = 1.250m,
                NetProductionQuantity = 73.750m,
                ProductionRatePerHour = 9.375m,
                WastePercentage = 1.667m,
                Status = "SUBMITTED",
                Notes = "TEST-تشغيل منتظم للخطوط وسلاسة في تغذية الركام",
                ProductionReferenceNumber = 88001,
                CreatedBy = "prod.manager@facestone.com",
                CreatedAt = now,
                Details = new List<DailyProductionDetail>
                {
                    new DailyProductionDetail
                    {
                        ProductName = "انترلوك سداسي رمادي 6سم",
                        Quantity = 75.000m,
                        ProductWaste = 1.250m,
                        NetQuantity = 73.750m,
                        ProductionRatePerHour = 9.375m,
                        Unit = "طن",
                        CreatedAt = now
                    }
                }
            };
            await db.DailyProductionSubmissions.AddAsync(dps);
            await db.SaveChangesAsync();
        }

        // 11. Attendance Submission & Record
        var attSub = await db.AttendanceSubmissions.FirstOrDefaultAsync(s => s.Notes != null && s.Notes == "TEST-إرسالية حضور وانصراف تجريبية");
        if (attSub == null)
        {
            attSub = new AttendanceSubmission
            {
                AttendanceDate = now.Date,
                TotalEmployeesCount = 15,
                PresentCount = 14,
                AbsentCount = 1,
                ApprovalStatus = "APPROVED",
                Notes = "TEST-إرسالية حضور وانصراف تجريبية",
                SubmittedBy = "attendance.employee@facestone.com",
                SubmittedAt = now.AddHours(-4)
            };
            await db.AttendanceSubmissions.AddAsync(attSub);
            await db.SaveChangesAsync();

            var attRecord = new AttendanceRecord
            {
                AttendanceSubmissionId = attSub.Id,
                Date = now.Date,
                EmployeeName = "سامح عبد الله",
                EmployeeRole = "مهندس إنتاج",
                Status = "Present",
                IsTestData = true,
                Notes = "TEST-سجل حضور تجريبي",
                CheckInTime = now.Date.AddHours(8),
                CheckOutTime = now.Date.AddHours(16),
                ApprovalStatus = "APPROVED"
            };
            await db.AttendanceRecords.AddAsync(attRecord);
            await db.SaveChangesAsync();
        }

        summary["RawMaterials"] = 2;
        summary["StorageLocations"] = 2;
        summary["StorageClients"] = 1;
        summary["PurchaseOrders"] = 1;
        summary["Transportation"] = 1;
        summary["StorageOperations"] = 1;
        summary["LaboratoryRawMaterialReports"] = 1;
        summary["ProductionOrders"] = 1;
        summary["ProductionBatches"] = 1;
        summary["ProductionOutputs"] = 1;
        summary["LaboratoryFinalConformityCertificates"] = 1;
        summary["DailyProductionSubmissions"] = 1;
        summary["AttendanceSubmissions"] = 1;
        summary["TotalSeededRecords"] = 15;
        summary["Status"] = "Completed successfully. All records marked with TEST-.";

        return summary;
    }

    /// <summary>
    /// Safely purges only records created by the test seeding mechanism (prefixed with TEST-).
    /// Real production records remain 100% untouched.
    /// </summary>
    public static async Task<Dictionary<string, int>> CleanupSafeTestDataAsync(FaceStoneDbContext db, ILogger logger)
    {
        var result = new Dictionary<string, int>();

        // 1. Daily Production
        var testDps = await db.DailyProductionSubmissions.Where(s => s.SubmissionNumber.StartsWith("TEST-")).ToListAsync();
        var dpsIds = testDps.Select(s => s.Id).ToList();
        var testDetails = await db.DailyProductionDetails.Where(d => dpsIds.Contains(d.DailyProductionSubmissionId)).ToListAsync();
        db.DailyProductionDetails.RemoveRange(testDetails);
        db.DailyProductionSubmissions.RemoveRange(testDps);
        result["DailyProductionDetails"] = testDetails.Count;
        result["DailyProductionSubmissions"] = testDps.Count;

        // 2. Lab Certs
        var testCerts = await db.LaboratoryFinalConformityCertificates.Where(c => c.CertificateNumber.StartsWith("TEST-")).ToListAsync();
        db.LaboratoryFinalConformityCertificates.RemoveRange(testCerts);
        result["LaboratoryFinalConformityCertificates"] = testCerts.Count;

        // 3. Production Outputs, Batches, Orders
        var testOutputs = await db.ProductionOutputs.Where(o => o.OutputNumber.StartsWith("TEST-")).ToListAsync();
        db.ProductionOutputs.RemoveRange(testOutputs);
        result["ProductionOutputs"] = testOutputs.Count;

        var testBatches = await db.ProductionBatches.Where(b => b.BatchNumber.StartsWith("TEST-")).ToListAsync();
        db.ProductionBatches.RemoveRange(testBatches);
        result["ProductionBatches"] = testBatches.Count;

        var testOrders = await db.ProductionOrders.Where(o => o.OrderNumber.StartsWith("TEST-")).ToListAsync();
        db.ProductionOrders.RemoveRange(testOrders);
        result["ProductionOrders"] = testOrders.Count;

        // 4. Lab Raw Material Reports
        var testLabRaw = await db.LaboratoryRawMaterialReports.Where(r => (r.BatchNumber != null && r.BatchNumber.StartsWith("TEST-")) || (r.Notes != null && r.Notes.StartsWith("TEST-"))).ToListAsync();
        db.LaboratoryRawMaterialReports.RemoveRange(testLabRaw);
        result["LaboratoryRawMaterialReports"] = testLabRaw.Count;

        // 5. Storage Operations & Items
        var testOps = await db.StorageOperations.Where(o => (o.Notes != null && o.Notes.StartsWith("TEST-")) || o.TransactionNumber == 99001).ToListAsync();
        var opIds = testOps.Select(o => o.Id).ToList();
        var testOpItems = await db.StorageOperationItems.Where(i => opIds.Contains(i.StorageOperationId) || i.TrackingNumber.StartsWith("TEST-")).ToListAsync();
        db.StorageOperationItems.RemoveRange(testOpItems);
        db.StorageOperations.RemoveRange(testOps);
        result["StorageOperationItems"] = testOpItems.Count;
        result["StorageOperations"] = testOps.Count;

        // 6. Transportation
        var testTrans = await db.Transportations.Where(t => t.TransactionNumber == 99001 || (t.Notes != null && t.Notes.StartsWith("TEST-"))).ToListAsync();
        db.Transportations.RemoveRange(testTrans);
        result["Transportation"] = testTrans.Count;

        // 7. Attendance
        var testAttRecs = await db.AttendanceRecords.Where(r => r.Notes != null && r.Notes.StartsWith("TEST-")).ToListAsync();
        db.AttendanceRecords.RemoveRange(testAttRecs);
        result["AttendanceRecords"] = testAttRecs.Count;

        var testAttSubs = await db.AttendanceSubmissions.Where(s => s.Notes != null && s.Notes.StartsWith("TEST-")).ToListAsync();
        db.AttendanceSubmissions.RemoveRange(testAttSubs);
        result["AttendanceSubmissions"] = testAttSubs.Count;

        // 8. Purchase Orders
        var testPOs = await db.PurchaseOrders.Where(p => p.PONumber.StartsWith("TEST-")).ToListAsync();
        db.PurchaseOrders.RemoveRange(testPOs);
        result["PurchaseOrders"] = testPOs.Count;

        // 9. Storage Clients & Locations
        var testClients = await db.StorageClients.Include(c => c.Destinations).Where(c => c.Name.StartsWith("TEST-")).ToListAsync();
        var testClientDests = testClients.SelectMany(c => c.Destinations).ToList();
        db.StorageClientDestinations.RemoveRange(testClientDests);
        db.StorageClients.RemoveRange(testClients);
        result["StorageClients"] = testClients.Count;

        var testLocs = await db.StorageLocations.Where(l => l.Code.StartsWith("TEST-")).ToListAsync();
        db.StorageLocations.RemoveRange(testLocs);
        result["StorageLocations"] = testLocs.Count;

        // 10. Raw Materials
        var testRaw = await db.RawMaterials.Where(r => r.Name.StartsWith("TEST-")).ToListAsync();
        db.RawMaterials.RemoveRange(testRaw);
        result["RawMaterials"] = testRaw.Count;

        await db.SaveChangesAsync();
        return result;
    }
}
