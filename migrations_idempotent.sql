IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927125002_AddTransportationTable'
)
BEGIN
    CREATE TABLE [Transportation] (
        [Id] int NOT NULL IDENTITY,
        [TransactionNumber] nvarchar(100) NOT NULL,
        [Date] datetime2 NOT NULL,
        [CarPlatesNumber] nvarchar(50) NOT NULL,
        [DriverName] nvarchar(150) NOT NULL,
        [Source] nvarchar(200) NOT NULL,
        [PO] nvarchar(100) NULL,
        [MaterialType] nvarchar(100) NOT NULL,
        [WeightBeforeLoading] decimal(18,3) NOT NULL,
        [WeightWhenEnterFactory] decimal(18,3) NOT NULL,
        [WeightWhenExitFactory] decimal(18,3) NOT NULL,
        [NetWeight] decimal(18,3) NOT NULL,
        [DeliveryStatus] nvarchar(50) NOT NULL,
        [ShippingStatus] nvarchar(50) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [Destination] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_Transportation] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927125002_AddTransportationTable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Transportation_TransactionNumber] ON [Transportation] ([TransactionNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927125002_AddTransportationTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260927125002_AddTransportationTable', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927133035_AddManagementTable'
)
BEGIN
    CREATE TABLE [Management] (
        [TransactionNumber] nvarchar(100) NOT NULL,
        [Date] datetime2 NOT NULL,
        [PO] nvarchar(100) NULL,
        [MaterialType] nvarchar(100) NOT NULL,
        [NumberOfTransactions] int NOT NULL,
        [TotalReceivedAmount] decimal(18,3) NOT NULL,
        [CarPlatesNumber] nvarchar(50) NOT NULL,
        [AcceptedQuantity] decimal(18,3) NOT NULL,
        [UnacceptedPendingQuantity] decimal(18,3) NOT NULL,
        [DeliveryStatus] nvarchar(50) NOT NULL,
        [Destination] nvarchar(200) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        CONSTRAINT [PK_Management] PRIMARY KEY ([TransactionNumber])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927133035_AddManagementTable'
)
BEGIN
    CREATE INDEX [IX_Management_Date] ON [Management] ([Date]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927133035_AddManagementTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260927133035_AddManagementTable', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Transportation]') AND [c].[name] = N'PO');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Transportation] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Transportation] DROP COLUMN [PO];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    EXEC sp_rename N'[Transportation].[WeightWhenExitFactory]', N'WeightWhenExitingFactory', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    EXEC sp_rename N'[Transportation].[WeightWhenEnterFactory]', N'WeightWhenEnteringFactory', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    EXEC sp_rename N'[Transportation].[ShippingStatus]', N'TransactionStatus', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    EXEC sp_rename N'[Transportation].[DeliveryStatus]', N'ReceivingStatus', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    EXEC sp_rename N'[Transportation].[Date]', N'TransactionDate', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    EXEC sp_rename N'[Transportation].[CarPlatesNumber]', N'CarNumber', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    DROP INDEX [IX_Transportation_TransactionNumber] ON [Transportation];
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Transportation]') AND [c].[name] = N'TransactionNumber');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Transportation] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Transportation] ALTER COLUMN [TransactionNumber] int NOT NULL;
    CREATE UNIQUE INDEX [IX_Transportation_TransactionNumber] ON [Transportation] ([TransactionNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    ALTER TABLE [Transportation] ADD [DateOfSubmit] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    ALTER TABLE [Transportation] ADD [ExpectedWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    ALTER TABLE [Transportation] ADD [PONumber] nvarchar(100) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE TABLE [PurchaseOrders] (
        [Id] int NOT NULL IDENTITY,
        [PONumber] nvarchar(100) NOT NULL,
        [Supplier] nvarchar(200) NOT NULL,
        [OrderDate] datetime2 NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [PricePerTon] decimal(18,2) NOT NULL,
        [TotalValue] decimal(18,2) NOT NULL,
        [InvoiceNumber] nvarchar(100) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAt] datetime2 NULL,
        [Destination] nvarchar(200) NOT NULL DEFAULT N'Cairo Factory',
        [RemainingQuantity] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE TABLE [StorageTransactions] (
        [Id] int NOT NULL IDENTITY,
        [MovementNumber] int NOT NULL,
        [TransactionDate] datetime2 NOT NULL,
        [ServiceType] nvarchar(50) NOT NULL,
        [ReferenceNumber] int NOT NULL,
        [PONumber] nvarchar(100) NOT NULL,
        [IncomingQuantity] decimal(18,3) NOT NULL,
        [OutgoingQuantity] decimal(18,3) NOT NULL,
        [OpeningBalance] decimal(18,3) NOT NULL,
        [ClosingBalance] decimal(18,3) NOT NULL,
        [StorageLocation] nvarchar(200) NOT NULL,
        [DestinationStorageLocation] nvarchar(200) NULL,
        [ReceivingEmployee] nvarchar(150) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_StorageTransactions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE TABLE [FinancePayments] (
        [Id] int NOT NULL IDENTITY,
        [PurchaseOrderId] int NULL,
        [PONumber] nvarchar(100) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentDate] datetime2 NOT NULL,
        [PaymentMethod] nvarchar(50) NOT NULL DEFAULT N'Bank Transfer',
        [ReferenceNumber] nvarchar(100) NULL,
        [PaidBy] nvarchar(150) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_FinancePayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FinancePayments_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE TABLE [FinanceTransactions] (
        [Id] int NOT NULL IDENTITY,
        [TransactionNumber] nvarchar(100) NOT NULL,
        [PurchaseOrderId] int NULL,
        [PONumber] nvarchar(100) NOT NULL,
        [Destination] nvarchar(200) NOT NULL,
        [Supplier] nvarchar(200) NULL,
        [WeightBeforeLoading] decimal(18,3) NOT NULL,
        [FactoryExitWeight] decimal(18,3) NOT NULL,
        [EntryWeight] decimal(18,3) NOT NULL,
        [ExpectedWeight] decimal(18,3) NOT NULL,
        [ActualWeight] decimal(18,3) NOT NULL,
        [Ikramiya] decimal(18,2) NOT NULL DEFAULT 0.0,
        [InvoiceAttachmentPath] nvarchar(500) NOT NULL,
        [InvoiceFileName] nvarchar(260) NOT NULL,
        [InvoiceFileType] nvarchar(50) NOT NULL,
        [InvoiceFileSize] bigint NOT NULL,
        [SubmittedBy] nvarchar(150) NOT NULL,
        [SubmittedByUserId] uniqueidentifier NULL,
        [SubmissionDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [Status] nvarchar(50) NOT NULL DEFAULT N'Submitted',
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_FinanceTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FinanceTransactions_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE TABLE [StorageTransactionMovements] (
        [Id] int NOT NULL IDENTITY,
        [StorageTransactionId] int NOT NULL,
        [TransportationMovementNumber] int NOT NULL,
        [TransportationId] int NULL,
        CONSTRAINT [PK_StorageTransactionMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StorageTransactionMovements_StorageTransactions_StorageTransactionId] FOREIGN KEY ([StorageTransactionId]) REFERENCES [StorageTransactions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_StorageTransactionMovements_Transportation_TransportationId] FOREIGN KEY ([TransportationId]) REFERENCES [Transportation] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_FinancePayments_PaymentDate] ON [FinancePayments] ([PaymentDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_FinancePayments_PONumber] ON [FinancePayments] ([PONumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_FinancePayments_PurchaseOrderId] ON [FinancePayments] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_FinanceTransactions_PONumber] ON [FinanceTransactions] ([PONumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_FinanceTransactions_PurchaseOrderId] ON [FinanceTransactions] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_FinanceTransactions_SubmissionDate] ON [FinanceTransactions] ([SubmissionDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_FinanceTransactions_SubmittedBy] ON [FinanceTransactions] ([SubmittedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FinanceTransactions_TransactionNumber] ON [FinanceTransactions] ([TransactionNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_InvoiceNumber] ON [PurchaseOrders] ([InvoiceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_OrderDate] ON [PurchaseOrders] ([OrderDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseOrders_PONumber] ON [PurchaseOrders] ([PONumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_Supplier] ON [PurchaseOrders] ([Supplier]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_StorageTransactionMovements_StorageTransactionId] ON [StorageTransactionMovements] ([StorageTransactionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_StorageTransactionMovements_TransportationId] ON [StorageTransactionMovements] ([TransportationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_StorageTransactionMovements_TransportationMovementNumber] ON [StorageTransactionMovements] ([TransportationMovementNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_StorageTransactions_MovementNumber] ON [StorageTransactions] ([MovementNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StorageTransactions_ReferenceNumber] ON [StorageTransactions] ([ReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_StorageTransactions_StorageLocation] ON [StorageTransactions] ([StorageLocation]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    CREATE INDEX [IX_StorageTransactions_TransactionDate] ON [StorageTransactions] ([TransactionDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001105431_AddFinanceTransactionsAndPoEnhancements'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001105431_AddFinanceTransactionsAndPoEnhancements', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [ArrivalTime] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [CompanyName] nvarchar(200) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [DepartmentSupervisor] nvarchar(100) NOT NULL DEFAULT N'ازهر محمد';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [DepartureTime] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [DriverPhone] nvarchar(30) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [ExternalEmptyWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [ExternalEmptyWeightTime] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [ExternalLoadedWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [ExternalLoadedWeightTime] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [ExternalNetWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [ExternalWeighbridgeCode] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [InternalEmptyWeightTime] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [InternalLoadedWeightTime] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [InternalWeighbridgeCode] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [InternalWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [ReceivedDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [RecipientName] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [RecipientTitle] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [ShippingDurationMinutes] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [ShippingSupervisor] nvarchar(100) NOT NULL DEFAULT N'محمد ناجح';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [WasteQuantity] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003082328_UpdateTransportationDepartment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003082328_UpdateTransportationDepartment', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    DROP INDEX [IX_Transportation_TransactionNumber] ON [Transportation];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    ALTER TABLE [Transportation] ADD [BillOfLadingAttachmentPath] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    ALTER TABLE [Transportation] ADD [BillOfLadingFileName] nvarchar(260) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    ALTER TABLE [Transportation] ADD [BillOfLadingFileSize] bigint NOT NULL DEFAULT CAST(0 AS bigint);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    ALTER TABLE [Transportation] ADD [BillOfLadingFileType] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    ALTER TABLE [Transportation] ADD [DailySupplyAttachmentPath] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    ALTER TABLE [Transportation] ADD [DailySupplyFileName] nvarchar(260) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    ALTER TABLE [Transportation] ADD [DailySupplyFileSize] bigint NOT NULL DEFAULT CAST(0 AS bigint);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    ALTER TABLE [Transportation] ADD [DailySupplyFileType] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    ALTER TABLE [Transportation] ADD [WeighbridgeAttachmentPath] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    ALTER TABLE [Transportation] ADD [WeighbridgeFileName] nvarchar(260) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    ALTER TABLE [Transportation] ADD [WeighbridgeFileSize] bigint NOT NULL DEFAULT CAST(0 AS bigint);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    ALTER TABLE [Transportation] ADD [WeighbridgeFileType] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Transportation_PONumber_TransactionNumber] ON [Transportation] ([PONumber], [TransactionNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003103405_AddSectionAttachmentsAndPerPoTransactionNumberIndex', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003113538_AddCustodyAmountToTransportation'
)
BEGIN
    ALTER TABLE [Transportation] ADD [CustodyAmount] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003113538_AddCustodyAmountToTransportation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003113538_AddCustodyAmountToTransportation', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    DROP INDEX [IX_StorageTransactions_ReferenceNumber] ON [StorageTransactions];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StorageTransactions]') AND [c].[name] = N'StorageLocation');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [StorageTransactions] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [StorageTransactions] ADD DEFAULT N'مستودع الخامات الرئيسي' FOR [StorageLocation];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StorageTransactions]') AND [c].[name] = N'ServiceType');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [StorageTransactions] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [StorageTransactions] ADD DEFAULT N'إيداع' FOR [ServiceType];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StorageTransactions]') AND [c].[name] = N'PONumber');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [StorageTransactions] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [StorageTransactions] ALTER COLUMN [PONumber] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StorageTransactions]') AND [c].[name] = N'OutgoingQuantity');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [StorageTransactions] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [StorageTransactions] ADD DEFAULT 0.0 FOR [OutgoingQuantity];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StorageTransactions]') AND [c].[name] = N'OpeningBalance');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [StorageTransactions] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [StorageTransactions] ADD DEFAULT 0.0 FOR [OpeningBalance];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StorageTransactions]') AND [c].[name] = N'IncomingQuantity');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [StorageTransactions] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [StorageTransactions] ADD DEFAULT 0.0 FOR [IncomingQuantity];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StorageTransactions]') AND [c].[name] = N'ClosingBalance');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [StorageTransactions] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [StorageTransactions] ADD DEFAULT 0.0 FOR [ClosingBalance];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [Action] nvarchar(50) NOT NULL DEFAULT N'Import';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [CreatedBy] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [ExternalEmptyWeight] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [ExternalEmptyWeightTime] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [ExternalLoadedWeight] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [ExternalLoadedWeightTime] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [ExternalNetWeight] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [ExternalWeighbridgeReference] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [HasExternalWeighbridge] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [InternalEmptyWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [InternalEmptyWeightTime] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [InternalLoadedWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [InternalLoadedWeightTime] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [InternalNetWeight] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [InternalWeighbridgeReference] nvarchar(100) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [IsExportRelatedToPO] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [Material] nvarchar(100) NOT NULL DEFAULT N'powder';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [PurchaseOrderId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [WeighbridgeAttachmentPath] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [WeighbridgeFileName] nvarchar(260) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [WeighbridgeFileSize] bigint NOT NULL DEFAULT CAST(0 AS bigint);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD [WeighbridgeFileType] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    CREATE INDEX [IX_StorageTransactions_Action] ON [StorageTransactions] ([Action]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    CREATE INDEX [IX_StorageTransactions_Material] ON [StorageTransactions] ([Material]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    CREATE INDEX [IX_StorageTransactions_PONumber] ON [StorageTransactions] ([PONumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    CREATE INDEX [IX_StorageTransactions_PurchaseOrderId] ON [StorageTransactions] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    CREATE INDEX [IX_StorageTransactions_ReferenceNumber] ON [StorageTransactions] ([ReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    ALTER TABLE [StorageTransactions] ADD CONSTRAINT [FK_StorageTransactions_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125029_RedesignStorageDepartment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004125029_RedesignStorageDepartment', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005075013_AddTonPriceToPurchaseOrders'
)
BEGIN
    ALTER TABLE [PurchaseOrders] ADD [TonPrice] decimal(18,3) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005075013_AddTonPriceToPurchaseOrders'
)
BEGIN
    UPDATE PurchaseOrders SET TonPrice = PricePerTon WHERE TonPrice = 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005075013_AddTonPriceToPurchaseOrders'
)
BEGIN
    UPDATE t SET t.PurchaseOrderId = po.Id FROM Transportation t INNER JOIN PurchaseOrders po ON t.PONumber = po.PONumber WHERE t.PurchaseOrderId IS NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005075013_AddTonPriceToPurchaseOrders'
)
BEGIN
    UPDATE s SET s.PurchaseOrderId = po.Id FROM StorageTransactions s INNER JOIN PurchaseOrders po ON s.PONumber = po.PONumber WHERE s.PurchaseOrderId IS NULL AND s.PONumber IS NOT NULL AND s.PONumber <> '';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005075013_AddTonPriceToPurchaseOrders'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005075013_AddTonPriceToPurchaseOrders', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012065711_AddStorageItemCardAndRawMaterials'
)
BEGIN
    CREATE TABLE [RawMaterials] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [InitialQuantity] decimal(18,3) NOT NULL DEFAULT 0.0,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [CreatedBy] nvarchar(150) NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        CONSTRAINT [PK_RawMaterials] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012065711_AddStorageItemCardAndRawMaterials'
)
BEGIN
    CREATE TABLE [StorageItemTransactions] (
        [Id] int NOT NULL IDENTITY,
        [RawMaterialId] int NOT NULL,
        [TransactionDate] datetime2 NOT NULL,
        [TransactionType] nvarchar(50) NOT NULL DEFAULT N'Incoming',
        [Description] nvarchar(100) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL DEFAULT 0.0,
        [TotalValue] decimal(18,2) NOT NULL DEFAULT 0.0,
        [DestinationType] nvarchar(50) NULL,
        [Destination] nvarchar(200) NULL,
        [SourceTransportationId] int NULL,
        [SourceTransportationNumber] int NULL,
        [RunningQuantityBalance] decimal(18,3) NOT NULL DEFAULT 0.0,
        [RunningValueBalance] decimal(18,2) NOT NULL DEFAULT 0.0,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [CreatedBy] nvarchar(150) NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        CONSTRAINT [PK_StorageItemTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StorageItemTransactions_RawMaterials_RawMaterialId] FOREIGN KEY ([RawMaterialId]) REFERENCES [RawMaterials] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_StorageItemTransactions_Transportation_SourceTransportationId] FOREIGN KEY ([SourceTransportationId]) REFERENCES [Transportation] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012065711_AddStorageItemCardAndRawMaterials'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RawMaterials_Name] ON [RawMaterials] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012065711_AddStorageItemCardAndRawMaterials'
)
BEGIN
    CREATE INDEX [IX_StorageItemTransactions_RawMaterialId] ON [StorageItemTransactions] ([RawMaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012065711_AddStorageItemCardAndRawMaterials'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StorageItemTransactions_SourceTransportationId] ON [StorageItemTransactions] ([SourceTransportationId]) WHERE [SourceTransportationId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012065711_AddStorageItemCardAndRawMaterials'
)
BEGIN
    CREATE INDEX [IX_StorageItemTransactions_TransactionDate] ON [StorageItemTransactions] ([TransactionDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012065711_AddStorageItemCardAndRawMaterials'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012065711_AddStorageItemCardAndRawMaterials', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE TABLE [StorageClients] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [PhoneNumber] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_StorageClients] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE TABLE [StorageLocations] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Description] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_StorageLocations] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE TABLE [StorageSequences] (
        [SequenceType] nvarchar(50) NOT NULL,
        [NextValue] int NOT NULL,
        CONSTRAINT [PK_StorageSequences] PRIMARY KEY ([SequenceType])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE TABLE [StorageClientDestinations] (
        [Id] int NOT NULL IDENTITY,
        [StorageClientId] int NOT NULL,
        [DestinationName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_StorageClientDestinations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StorageClientDestinations_StorageClients_StorageClientId] FOREIGN KEY ([StorageClientId]) REFERENCES [StorageClients] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE TABLE [StorageOperations] (
        [Id] int NOT NULL IDENTITY,
        [OperationType] nvarchar(50) NOT NULL,
        [TransactionNumber] int NOT NULL,
        [TransactionDate] datetime2 NOT NULL,
        [StorageLocationId] int NOT NULL,
        [StorageLocationName] nvarchar(200) NOT NULL,
        [Source] nvarchar(max) NULL,
        [SourcePhone] nvarchar(max) NULL,
        [ClientId] int NULL,
        [ClientName] nvarchar(max) NULL,
        [DestinationId] int NULL,
        [DestinationName] nvarchar(max) NULL,
        [ClientPhone] nvarchar(max) NULL,
        [DestinationDepartment] nvarchar(max) NULL,
        [ResponsibleEmployee] nvarchar(150) NOT NULL,
        [ResponsibleUserId] uniqueidentifier NULL,
        [Notes] nvarchar(max) NULL,
        [ApprovalStatus] nvarchar(50) NOT NULL DEFAULT N'PENDING',
        [ReviewedBy] nvarchar(max) NULL,
        [ReviewedByUserId] uniqueidentifier NULL,
        [ReviewedAt] datetime2 NULL,
        [RejectionReason] nvarchar(max) NULL,
        [IsInventoryPosted] bit NOT NULL,
        [InventoryPostedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [CreatedBy] nvarchar(max) NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        CONSTRAINT [PK_StorageOperations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StorageOperations_StorageClientDestinations_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [StorageClientDestinations] ([Id]),
        CONSTRAINT [FK_StorageOperations_StorageClients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [StorageClients] ([Id]),
        CONSTRAINT [FK_StorageOperations_StorageLocations_StorageLocationId] FOREIGN KEY ([StorageLocationId]) REFERENCES [StorageLocations] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE TABLE [StorageOperationItems] (
        [Id] int NOT NULL IDENTITY,
        [StorageOperationId] int NOT NULL,
        [RawMaterialId] int NOT NULL,
        [MaterialName] nvarchar(200) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [TrackingNumber] nvarchar(100) NOT NULL,
        [Notes] nvarchar(max) NULL,
        CONSTRAINT [PK_StorageOperationItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StorageOperationItems_RawMaterials_RawMaterialId] FOREIGN KEY ([RawMaterialId]) REFERENCES [RawMaterials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StorageOperationItems_StorageOperations_StorageOperationId] FOREIGN KEY ([StorageOperationId]) REFERENCES [StorageOperations] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE TABLE [ProductionWorkInProcess] (
        [Id] int NOT NULL IDENTITY,
        [StorageOperationId] int NOT NULL,
        [StorageOperationItemId] int NOT NULL,
        [RawMaterialId] int NOT NULL,
        [TrackingNumber] nvarchar(100) NOT NULL,
        [MaterialQuantityIssued] decimal(18,3) NOT NULL,
        [SourceStorageLocation] nvarchar(200) NOT NULL,
        [TransferDate] datetime2 NOT NULL,
        [Status] nvarchar(50) NOT NULL DEFAULT N'قيد التشغيل',
        [FinalProductName] nvarchar(max) NULL,
        [FinalProductQuantity] decimal(18,3) NULL,
        [WasteQuantity] decimal(18,3) NULL,
        [WastePercentage] decimal(18,2) NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [CreatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductionWorkInProcess] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductionWorkInProcess_RawMaterials_RawMaterialId] FOREIGN KEY ([RawMaterialId]) REFERENCES [RawMaterials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductionWorkInProcess_StorageOperationItems_StorageOperationItemId] FOREIGN KEY ([StorageOperationItemId]) REFERENCES [StorageOperationItems] ([Id]),
        CONSTRAINT [FK_ProductionWorkInProcess_StorageOperations_StorageOperationId] FOREIGN KEY ([StorageOperationId]) REFERENCES [StorageOperations] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE TABLE [StorageInventoryLedger] (
        [Id] int NOT NULL IDENTITY,
        [StorageLocationId] int NOT NULL,
        [RawMaterialId] int NOT NULL,
        [StorageOperationId] int NULL,
        [StorageOperationItemId] int NULL,
        [MovementType] nvarchar(50) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [RunningBalance] decimal(18,3) NOT NULL,
        [MovementDate] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [CreatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_StorageInventoryLedger] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StorageInventoryLedger_RawMaterials_RawMaterialId] FOREIGN KEY ([RawMaterialId]) REFERENCES [RawMaterials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StorageInventoryLedger_StorageLocations_StorageLocationId] FOREIGN KEY ([StorageLocationId]) REFERENCES [StorageLocations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StorageInventoryLedger_StorageOperationItems_StorageOperationItemId] FOREIGN KEY ([StorageOperationItemId]) REFERENCES [StorageOperationItems] ([Id]),
        CONSTRAINT [FK_StorageInventoryLedger_StorageOperations_StorageOperationId] FOREIGN KEY ([StorageOperationId]) REFERENCES [StorageOperations] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_ProductionWorkInProcess_RawMaterialId] ON [ProductionWorkInProcess] ([RawMaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_ProductionWorkInProcess_Status] ON [ProductionWorkInProcess] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_ProductionWorkInProcess_StorageOperationId] ON [ProductionWorkInProcess] ([StorageOperationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_ProductionWorkInProcess_StorageOperationItemId] ON [ProductionWorkInProcess] ([StorageOperationItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductionWorkInProcess_TrackingNumber] ON [ProductionWorkInProcess] ([TrackingNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_StorageClientDestinations_StorageClientId] ON [StorageClientDestinations] ([StorageClientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StorageClients_Name] ON [StorageClients] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_StorageInventoryLedger_MovementDate] ON [StorageInventoryLedger] ([MovementDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_StorageInventoryLedger_RawMaterialId] ON [StorageInventoryLedger] ([RawMaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_StorageInventoryLedger_StorageLocationId_RawMaterialId] ON [StorageInventoryLedger] ([StorageLocationId], [RawMaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_StorageInventoryLedger_StorageOperationId] ON [StorageInventoryLedger] ([StorageOperationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_StorageInventoryLedger_StorageOperationItemId] ON [StorageInventoryLedger] ([StorageOperationItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StorageLocations_Name] ON [StorageLocations] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_StorageOperationItems_RawMaterialId] ON [StorageOperationItems] ([RawMaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_StorageOperationItems_StorageOperationId] ON [StorageOperationItems] ([StorageOperationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_StorageOperations_ApprovalStatus] ON [StorageOperations] ([ApprovalStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_StorageOperations_ClientId] ON [StorageOperations] ([ClientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_StorageOperations_DestinationId] ON [StorageOperations] ([DestinationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StorageOperations_OperationType_TransactionNumber] ON [StorageOperations] ([OperationType], [TransactionNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_StorageOperations_StorageLocationId] ON [StorageOperations] ([StorageLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    CREATE INDEX [IX_StorageOperations_TransactionDate] ON [StorageOperations] ([TransactionDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013115024_AddStorageRedesignThreeCards'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013115024_AddStorageRedesignThreeCards', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [DiscrepancyExplanation] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [ExternalReportedWeight] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [InternalEmptyWeight] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [InternalLoadedWeight] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [InternalNetWeight] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [InternalWeighingTime] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [IsDiscrepancyFlagged] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [MaterialType] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [TransportationId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [WeighbridgeReference] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [WeighingResponsibleEmployee] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [WeightDifference] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [WeightDifferencePercentage] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [RawMaterials] ADD [MaterialType] nvarchar(50) NOT NULL DEFAULT N'MECHANICAL';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [ProductionWorkInProcess] ADD [ClassificationHistory] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [ProductionWorkInProcess] ADD [DestinationStorageLocationId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [ProductionWorkInProcess] ADD [DestinationStorageLocationName] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [ProductionWorkInProcess] ADD [IsRecoveredStockPosted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [ProductionWorkInProcess] ADD [OutputResponsibleEmployee] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [ProductionWorkInProcess] ADD [RecoverableMaterialId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [ProductionWorkInProcess] ADD [RecoverableProductName] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [ProductionWorkInProcess] ADD [RecoverableQuantity] decimal(18,3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [ProductionWorkInProcess] ADD [RecoverableUnit] nvarchar(50) NOT NULL DEFAULT N'طن';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [ProductionWorkInProcess] ADD [RecoveredStockPostedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [ProductionWorkInProcess] ADD [WasteClassification] nvarchar(50) NOT NULL DEFAULT N'ORDINARY_WASTE';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    CREATE INDEX [IX_StorageOperations_TransportationId] ON [StorageOperations] ([TransportationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    CREATE INDEX [IX_ProductionWorkInProcess_DestinationStorageLocationId] ON [ProductionWorkInProcess] ([DestinationStorageLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    CREATE INDEX [IX_ProductionWorkInProcess_RecoverableMaterialId] ON [ProductionWorkInProcess] ([RecoverableMaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [ProductionWorkInProcess] ADD CONSTRAINT [FK_ProductionWorkInProcess_RawMaterials_RecoverableMaterialId] FOREIGN KEY ([RecoverableMaterialId]) REFERENCES [RawMaterials] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [ProductionWorkInProcess] ADD CONSTRAINT [FK_ProductionWorkInProcess_StorageLocations_DestinationStorageLocationId] FOREIGN KEY ([DestinationStorageLocationId]) REFERENCES [StorageLocations] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD CONSTRAINT [FK_StorageOperations_Transportation_TransportationId] FOREIGN KEY ([TransportationId]) REFERENCES [Transportation] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019122421_AddStorageEnhancementsWeighingAndWaste'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019122421_AddStorageEnhancementsWeighingAndWaste', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    ALTER TABLE [Transportation] ADD [ProductionReferenceNumber] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [ProductionReferenceNumber] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    ALTER TABLE [ProductionWorkInProcess] ADD [ProductionReferenceNumber] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE TABLE [LaboratoryProductionSequences] (
        [SequenceName] nvarchar(50) NOT NULL,
        [NextValue] int NOT NULL DEFAULT 1,
        CONSTRAINT [PK_LaboratoryProductionSequences] PRIMARY KEY ([SequenceName])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE TABLE [LaboratoryRawMaterialReports] (
        [Id] int NOT NULL IDENTITY,
        [ProductionReferenceNumber] int NOT NULL,
        [LaboratorySection] nvarchar(20) NOT NULL,
        [AnalysisDateTime] datetime2 NOT NULL,
        [RawMaterialDescription] nvarchar(250) NOT NULL,
        [RawMaterialId] int NULL,
        [SupplierName] nvarchar(200) NULL,
        [SourceTransportationId] int NULL,
        [TransportationTransactionNumber] int NULL,
        [PurchaseOrderId] int NULL,
        [PONumber] nvarchar(100) NULL,
        [ReceivedQuantity] decimal(18,3) NOT NULL,
        [AcceptedQuantity] decimal(18,3) NOT NULL,
        [RejectedQuantity] decimal(18,3) NOT NULL,
        [AnalysisResultsJson] nvarchar(max) NULL,
        [InspectionDecision] nvarchar(50) NOT NULL,
        [Notes] nvarchar(max) NULL,
        [AttachmentPath] nvarchar(max) NULL,
        [AttachmentFileName] nvarchar(max) NULL,
        [AttachmentFileType] nvarchar(max) NULL,
        [AttachmentFileSize] bigint NOT NULL,
        [ResponsibleEmployee] nvarchar(150) NOT NULL,
        [ResponsibleUserId] uniqueidentifier NULL,
        [ApprovalStatus] nvarchar(50) NOT NULL DEFAULT N'PENDING',
        [ReviewedBy] nvarchar(150) NULL,
        [ReviewedByUserId] uniqueidentifier NULL,
        [ReviewedAt] datetime2 NULL,
        [RejectionReason] nvarchar(500) NULL,
        [IsStorageReceived] bit NOT NULL,
        [StorageReceivedAt] datetime2 NULL,
        [StorageOperationId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(150) NOT NULL,
        CONSTRAINT [PK_LaboratoryRawMaterialReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LaboratoryRawMaterialReports_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_LaboratoryRawMaterialReports_RawMaterials_RawMaterialId] FOREIGN KEY ([RawMaterialId]) REFERENCES [RawMaterials] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_LaboratoryRawMaterialReports_Transportation_SourceTransportationId] FOREIGN KEY ([SourceTransportationId]) REFERENCES [Transportation] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE TABLE [LaboratoryFinalConformityCertificates] (
        [Id] int NOT NULL IDENTITY,
        [CertificateNumber] nvarchar(50) NOT NULL,
        [ProductionReferenceNumber] int NOT NULL,
        [RawMaterialReportId] int NULL,
        [LaboratorySection] nvarchar(20) NOT NULL,
        [FinalProductName] nvarchar(200) NOT NULL,
        [ActualFinalProductQuantity] decimal(18,3) NOT NULL,
        [QuantityUnit] nvarchar(50) NOT NULL DEFAULT N'طن',
        [ProductionBatchNumber] nvarchar(max) NOT NULL,
        [ProductionWorkInProcessId] int NULL,
        [ProductionCompletionDateTime] datetime2 NOT NULL,
        [ActualRawMaterialInputQuantity] decimal(18,3) NOT NULL,
        [CalculatedDifference] decimal(18,3) NOT NULL,
        [ActualMeasuredWaste] decimal(18,3) NULL,
        [WasteClassification] nvarchar(50) NOT NULL DEFAULT N'ORDINARY_WASTE',
        [ConformityDecision] nvarchar(50) NOT NULL,
        [NonConformityAction] nvarchar(50) NULL,
        [ClassifiedProductType] nvarchar(100) NULL,
        [ReworkInstructions] nvarchar(max) NULL,
        [SpecificationResultsJson] nvarchar(max) NULL,
        [Observations] nvarchar(max) NULL,
        [AttachmentPath] nvarchar(max) NULL,
        [AttachmentFileName] nvarchar(max) NULL,
        [AttachmentFileType] nvarchar(max) NULL,
        [AttachmentFileSize] bigint NOT NULL,
        [ResponsibleEmployee] nvarchar(150) NOT NULL,
        [ResponsibleUserId] uniqueidentifier NULL,
        [ApprovalStatus] nvarchar(50) NOT NULL DEFAULT N'PENDING',
        [ReviewedBy] nvarchar(150) NULL,
        [ReviewedByUserId] uniqueidentifier NULL,
        [ReviewedAt] datetime2 NULL,
        [RejectionReason] nvarchar(500) NULL,
        [IsStoragePosted] bit NOT NULL,
        [StoragePostedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(150) NOT NULL,
        CONSTRAINT [PK_LaboratoryFinalConformityCertificates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LaboratoryFinalConformityCertificates_LaboratoryRawMaterialReports_RawMaterialReportId] FOREIGN KEY ([RawMaterialReportId]) REFERENCES [LaboratoryRawMaterialReports] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_LaboratoryFinalConformityCertificates_ProductionWorkInProcess_ProductionWorkInProcessId] FOREIGN KEY ([ProductionWorkInProcessId]) REFERENCES [ProductionWorkInProcess] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE TABLE [LaboratoryInProcessReports] (
        [Id] int NOT NULL IDENTITY,
        [ProductionReferenceNumber] int NOT NULL,
        [RawMaterialReportId] int NULL,
        [ProductionBatchNumber] nvarchar(100) NOT NULL,
        [ProductionWorkInProcessId] int NULL,
        [LaboratorySection] nvarchar(20) NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [TestDateTime] datetime2 NOT NULL,
        [ProductionStage] nvarchar(100) NOT NULL,
        [AnalysisResultsJson] nvarchar(max) NULL,
        [SpecificationCriteria] nvarchar(max) NULL,
        [InspectionDecision] nvarchar(50) NOT NULL,
        [Notes] nvarchar(max) NULL,
        [AttachmentPath] nvarchar(max) NULL,
        [AttachmentFileName] nvarchar(max) NULL,
        [AttachmentFileType] nvarchar(max) NULL,
        [AttachmentFileSize] bigint NOT NULL,
        [ResponsibleEmployee] nvarchar(150) NOT NULL,
        [ResponsibleUserId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(150) NOT NULL,
        CONSTRAINT [PK_LaboratoryInProcessReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LaboratoryInProcessReports_LaboratoryRawMaterialReports_RawMaterialReportId] FOREIGN KEY ([RawMaterialReportId]) REFERENCES [LaboratoryRawMaterialReports] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_LaboratoryInProcessReports_ProductionWorkInProcess_ProductionWorkInProcessId] FOREIGN KEY ([ProductionWorkInProcessId]) REFERENCES [ProductionWorkInProcess] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_Transportation_ProductionReferenceNumber] ON [Transportation] ([ProductionReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_StorageOperations_ProductionReferenceNumber] ON [StorageOperations] ([ProductionReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionWorkInProcess_ProductionReferenceNumber] ON [ProductionWorkInProcess] ([ProductionReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryFinalConformityCertificates_ApprovalStatus] ON [LaboratoryFinalConformityCertificates] ([ApprovalStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LaboratoryFinalConformityCertificates_CertificateNumber] ON [LaboratoryFinalConformityCertificates] ([CertificateNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryFinalConformityCertificates_ConformityDecision] ON [LaboratoryFinalConformityCertificates] ([ConformityDecision]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryFinalConformityCertificates_LaboratorySection] ON [LaboratoryFinalConformityCertificates] ([LaboratorySection]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryFinalConformityCertificates_ProductionReferenceNumber] ON [LaboratoryFinalConformityCertificates] ([ProductionReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryFinalConformityCertificates_ProductionWorkInProcessId] ON [LaboratoryFinalConformityCertificates] ([ProductionWorkInProcessId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryFinalConformityCertificates_RawMaterialReportId] ON [LaboratoryFinalConformityCertificates] ([RawMaterialReportId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryInProcessReports_LaboratorySection] ON [LaboratoryInProcessReports] ([LaboratorySection]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryInProcessReports_ProductionReferenceNumber] ON [LaboratoryInProcessReports] ([ProductionReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryInProcessReports_ProductionStage] ON [LaboratoryInProcessReports] ([ProductionStage]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryInProcessReports_ProductionWorkInProcessId] ON [LaboratoryInProcessReports] ([ProductionWorkInProcessId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryInProcessReports_RawMaterialReportId] ON [LaboratoryInProcessReports] ([RawMaterialReportId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryInProcessReports_TestDateTime] ON [LaboratoryInProcessReports] ([TestDateTime]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryRawMaterialReports_AnalysisDateTime] ON [LaboratoryRawMaterialReports] ([AnalysisDateTime]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryRawMaterialReports_ApprovalStatus] ON [LaboratoryRawMaterialReports] ([ApprovalStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryRawMaterialReports_InspectionDecision] ON [LaboratoryRawMaterialReports] ([InspectionDecision]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryRawMaterialReports_LaboratorySection] ON [LaboratoryRawMaterialReports] ([LaboratorySection]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LaboratoryRawMaterialReports_ProductionReferenceNumber] ON [LaboratoryRawMaterialReports] ([ProductionReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryRawMaterialReports_PurchaseOrderId] ON [LaboratoryRawMaterialReports] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryRawMaterialReports_RawMaterialId] ON [LaboratoryRawMaterialReports] ([RawMaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    CREATE INDEX [IX_LaboratoryRawMaterialReports_SourceTransportationId] ON [LaboratoryRawMaterialReports] ([SourceTransportationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024105637_AddLaboratoryDepartment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261024105637_AddLaboratoryDepartment', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE TABLE [ProductionOrders] (
        [Id] int NOT NULL IDENTITY,
        [OrderNumber] nvarchar(50) NOT NULL,
        [ProductionReferenceNumber] int NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [RequestedQuantity] decimal(18,3) NOT NULL,
        [Unit] nvarchar(50) NOT NULL DEFAULT N'طن',
        [StorageLocationId] int NOT NULL,
        [StorageLocationName] nvarchar(150) NOT NULL,
        [Status] nvarchar(50) NOT NULL DEFAULT N'DRAFT',
        [CurrentStage] nvarchar(100) NOT NULL DEFAULT N'الخلط الأولي',
        [ResponsibleEmployee] nvarchar(150) NOT NULL,
        [ResponsibleUserId] uniqueidentifier NULL,
        [PlannedStartDate] datetime2 NULL,
        [ActualStartDate] datetime2 NULL,
        [CompletedDate] datetime2 NULL,
        [Notes] nvarchar(max) NULL,
        [AttachmentPath] nvarchar(max) NULL,
        [AttachmentFileName] nvarchar(max) NULL,
        [AttachmentFileType] nvarchar(max) NULL,
        [AttachmentFileSize] bigint NOT NULL,
        [StorageOperationId] int NULL,
        [IsStorageApproved] bit NOT NULL,
        [StorageApprovedAt] datetime2 NULL,
        [StorageApprovedBy] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(150) NOT NULL,
        CONSTRAINT [PK_ProductionOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductionOrders_StorageLocations_StorageLocationId] FOREIGN KEY ([StorageLocationId]) REFERENCES [StorageLocations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductionOrders_StorageOperations_StorageOperationId] FOREIGN KEY ([StorageOperationId]) REFERENCES [StorageOperations] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE TABLE [ProductionReworks] (
        [Id] int NOT NULL IDENTITY,
        [ReworkNumber] nvarchar(50) NOT NULL,
        [OriginalProductionOrderId] int NOT NULL,
        [OriginalProductionBatchId] int NOT NULL,
        [OriginalProductionOutputId] int NOT NULL,
        [ProductionReferenceNumber] int NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Instructions] nvarchar(max) NOT NULL,
        [Status] nvarchar(50) NOT NULL DEFAULT N'PENDING',
        [NewProductionOrderId] int NULL,
        [NewProductionBatchId] int NULL,
        [ResponsibleEmployee] nvarchar(150) NOT NULL,
        [ResponsibleUserId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(150) NOT NULL,
        CONSTRAINT [PK_ProductionReworks] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE TABLE [ProductionSequences] (
        [SequenceType] nvarchar(50) NOT NULL,
        [NextValue] int NOT NULL DEFAULT 1,
        CONSTRAINT [PK_ProductionSequences] PRIMARY KEY ([SequenceType])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE TABLE [ProductionBatches] (
        [Id] int NOT NULL IDENTITY,
        [BatchNumber] nvarchar(50) NOT NULL,
        [ProductionOrderId] int NOT NULL,
        [ProductionReferenceNumber] int NULL,
        [ProductionWorkInProcessId] int NULL,
        [CurrentStage] nvarchar(100) NOT NULL,
        [Status] nvarchar(50) NOT NULL DEFAULT N'IN_PROGRESS',
        [QualityHold] bit NOT NULL,
        [QualityHoldReason] nvarchar(max) NULL,
        [QualityHoldReportId] int NULL,
        [QualityHoldAt] datetime2 NULL,
        [QualityHoldReleasedAt] datetime2 NULL,
        [QualityHoldReleasedBy] nvarchar(max) NULL,
        [TotalInputQuantity] decimal(18,3) NOT NULL,
        [TotalOutputQuantity] decimal(18,3) NOT NULL,
        [TotalWasteQuantity] decimal(18,3) NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [ResponsibleEmployee] nvarchar(150) NOT NULL,
        [ResponsibleUserId] uniqueidentifier NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(150) NOT NULL,
        CONSTRAINT [PK_ProductionBatches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductionBatches_ProductionOrders_ProductionOrderId] FOREIGN KEY ([ProductionOrderId]) REFERENCES [ProductionOrders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductionBatches_ProductionWorkInProcess_ProductionWorkInProcessId] FOREIGN KEY ([ProductionWorkInProcessId]) REFERENCES [ProductionWorkInProcess] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE TABLE [ProductionOrderItems] (
        [Id] int NOT NULL IDENTITY,
        [ProductionOrderId] int NOT NULL,
        [RawMaterialId] int NOT NULL,
        [MaterialName] nvarchar(150) NOT NULL,
        [RequestedQuantity] decimal(18,3) NOT NULL,
        [IssuedQuantity] decimal(18,3) NOT NULL,
        [ConsumedQuantity] decimal(18,3) NOT NULL,
        [Unit] nvarchar(50) NOT NULL DEFAULT N'طن',
        [StorageLocationId] int NULL,
        [LaboratoryRawMaterialReportId] int NULL,
        [Notes] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductionOrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductionOrderItems_ProductionOrders_ProductionOrderId] FOREIGN KEY ([ProductionOrderId]) REFERENCES [ProductionOrders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductionOrderItems_RawMaterials_RawMaterialId] FOREIGN KEY ([RawMaterialId]) REFERENCES [RawMaterials] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE TABLE [ProductionOutputs] (
        [Id] int NOT NULL IDENTITY,
        [OutputNumber] nvarchar(50) NOT NULL,
        [ProductionOrderId] int NOT NULL,
        [ProductionBatchId] int NOT NULL,
        [ProductionReferenceNumber] int NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [Classification] nvarchar(50) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [Unit] nvarchar(50) NOT NULL DEFAULT N'طن',
        [ProductionStage] nvarchar(100) NOT NULL,
        [OutputDate] datetime2 NOT NULL,
        [ResponsibleEmployee] nvarchar(150) NOT NULL,
        [ResponsibleUserId] uniqueidentifier NULL,
        [NextStage] nvarchar(max) NULL,
        [DestinationStorageLocationId] int NULL,
        [DestinationStorageLocationName] nvarchar(max) NULL,
        [LaboratoryConformityStatus] nvarchar(50) NOT NULL DEFAULT N'PENDING_INSPECTION',
        [LaboratoryCertificateId] int NULL,
        [IsStorageReceived] bit NOT NULL,
        [StorageReceivedQuantity] decimal(18,3) NOT NULL,
        [StorageReceivedAt] datetime2 NULL,
        [StorageReceivedBy] nvarchar(max) NULL,
        [StorageOperationId] int NULL,
        [NonConformityAction] nvarchar(max) NULL,
        [IsReworkCreated] bit NOT NULL,
        [ReworkOrderId] int NULL,
        [Notes] nvarchar(max) NULL,
        [AttachmentPath] nvarchar(max) NULL,
        [AttachmentFileName] nvarchar(max) NULL,
        [AttachmentFileType] nvarchar(max) NULL,
        [AttachmentFileSize] bigint NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(150) NOT NULL,
        CONSTRAINT [PK_ProductionOutputs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductionOutputs_LaboratoryFinalConformityCertificates_LaboratoryCertificateId] FOREIGN KEY ([LaboratoryCertificateId]) REFERENCES [LaboratoryFinalConformityCertificates] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ProductionOutputs_ProductionBatches_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatches] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductionOutputs_ProductionOrders_ProductionOrderId] FOREIGN KEY ([ProductionOrderId]) REFERENCES [ProductionOrders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductionOutputs_StorageLocations_DestinationStorageLocationId] FOREIGN KEY ([DestinationStorageLocationId]) REFERENCES [StorageLocations] ([Id]),
        CONSTRAINT [FK_ProductionOutputs_StorageOperations_StorageOperationId] FOREIGN KEY ([StorageOperationId]) REFERENCES [StorageOperations] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductionBatches_BatchNumber] ON [ProductionBatches] ([BatchNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionBatches_ProductionOrderId] ON [ProductionBatches] ([ProductionOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionBatches_ProductionReferenceNumber] ON [ProductionBatches] ([ProductionReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionBatches_ProductionWorkInProcessId] ON [ProductionBatches] ([ProductionWorkInProcessId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionBatches_QualityHold] ON [ProductionBatches] ([QualityHold]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionBatches_Status] ON [ProductionBatches] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOrderItems_ProductionOrderId] ON [ProductionOrderItems] ([ProductionOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOrderItems_RawMaterialId] ON [ProductionOrderItems] ([RawMaterialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOrders_CreatedAt] ON [ProductionOrders] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductionOrders_OrderNumber] ON [ProductionOrders] ([OrderNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOrders_ProductionReferenceNumber] ON [ProductionOrders] ([ProductionReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOrders_Status] ON [ProductionOrders] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOrders_StorageLocationId] ON [ProductionOrders] ([StorageLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOrders_StorageOperationId] ON [ProductionOrders] ([StorageOperationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOutputs_Classification] ON [ProductionOutputs] ([Classification]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOutputs_DestinationStorageLocationId] ON [ProductionOutputs] ([DestinationStorageLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOutputs_IsStorageReceived] ON [ProductionOutputs] ([IsStorageReceived]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOutputs_LaboratoryCertificateId] ON [ProductionOutputs] ([LaboratoryCertificateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOutputs_LaboratoryConformityStatus] ON [ProductionOutputs] ([LaboratoryConformityStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductionOutputs_OutputNumber] ON [ProductionOutputs] ([OutputNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOutputs_ProductionBatchId] ON [ProductionOutputs] ([ProductionBatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOutputs_ProductionOrderId] ON [ProductionOutputs] ([ProductionOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOutputs_ProductionReferenceNumber] ON [ProductionOutputs] ([ProductionReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionOutputs_StorageOperationId] ON [ProductionOutputs] ([StorageOperationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionReworks_OriginalProductionBatchId] ON [ProductionReworks] ([OriginalProductionBatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionReworks_OriginalProductionOrderId] ON [ProductionReworks] ([OriginalProductionOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionReworks_OriginalProductionOutputId] ON [ProductionReworks] ([OriginalProductionOutputId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionReworks_ProductionReferenceNumber] ON [ProductionReworks] ([ProductionReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductionReworks_ReworkNumber] ON [ProductionReworks] ([ReworkNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    CREATE INDEX [IX_ProductionReworks_Status] ON [ProductionReworks] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025065700_AddProductionDepartment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025065700_AddProductionDepartment', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    DROP INDEX [IX_LaboratoryRawMaterialReports_ProductionReferenceNumber] ON [LaboratoryRawMaterialReports];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    ALTER TABLE [LaboratoryRawMaterialReports] ADD [BatchNumber] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    ALTER TABLE [LaboratoryRawMaterialReports] ADD [ComplyStatus] nvarchar(50) NOT NULL DEFAULT N'COMPLY';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    ALTER TABLE [LaboratoryRawMaterialReports] ADD [LabManagerDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    ALTER TABLE [LaboratoryRawMaterialReports] ADD [LabManagerName] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    ALTER TABLE [LaboratoryRawMaterialReports] ADD [LabManagerSignature] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    ALTER TABLE [LaboratoryRawMaterialReports] ADD [ReportIssueDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    ALTER TABLE [LaboratoryRawMaterialReports] ADD [SampleDescription] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    ALTER TABLE [LaboratoryRawMaterialReports] ADD [SampleName] nvarchar(250) NOT NULL DEFAULT N'سيليكا باودر (Micro Silica Powder)';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    ALTER TABLE [LaboratoryRawMaterialReports] ADD [SampleNumber] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    ALTER TABLE [LaboratoryRawMaterialReports] ADD [SampleReceivedDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    CREATE TABLE [LaboratoryUserSampleSequences] (
        [UserId] uniqueidentifier NOT NULL,
        [NextSampleNumber] int NOT NULL DEFAULT 1,
        [UpdatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_LaboratoryUserSampleSequences] PRIMARY KEY ([UserId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    CREATE INDEX [IX_LaboratoryRawMaterialReports_BatchNumber] ON [LaboratoryRawMaterialReports] ([BatchNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    CREATE INDEX [IX_LaboratoryRawMaterialReports_ComplyStatus] ON [LaboratoryRawMaterialReports] ([ComplyStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    CREATE INDEX [IX_LaboratoryRawMaterialReports_ProductionReferenceNumber] ON [LaboratoryRawMaterialReports] ([ProductionReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_LaboratoryRawMaterialReports_ResponsibleUserId_SampleNumber] ON [LaboratoryRawMaterialReports] ([ResponsibleUserId], [SampleNumber]) WHERE [ResponsibleUserId] IS NOT NULL AND [SampleNumber] > 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    CREATE INDEX [IX_LaboratoryRawMaterialReports_SampleNumber] ON [LaboratoryRawMaterialReports] ([SampleNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026084230_UpdateLaboratoryDepartmentForm'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261026084230_UpdateLaboratoryDepartmentForm', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028172943_AddDailyProductionTracking'
)
BEGIN
    CREATE TABLE [DailyProductionSubmissions] (
        [Id] int NOT NULL IDENTITY,
        [SubmissionNumber] nvarchar(50) NOT NULL,
        [ProductionDate] datetime2 NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [EmployeeName] nvarchar(150) NOT NULL,
        [EmployeeEmail] nvarchar(150) NOT NULL,
        [Shift] nvarchar(100) NOT NULL,
        [ShiftStart] datetime2 NOT NULL,
        [ShiftEnd] datetime2 NOT NULL,
        [DurationHours] decimal(18,3) NOT NULL,
        [TotalGrossQuantity] decimal(18,3) NOT NULL,
        [WasteQuantity] decimal(18,3) NOT NULL,
        [NetProductionQuantity] decimal(18,3) NOT NULL,
        [ProductionRatePerHour] decimal(18,3) NOT NULL,
        [WastePercentage] decimal(18,3) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [ProductionReferenceNumber] int NULL,
        [Status] nvarchar(50) NOT NULL DEFAULT N'SUBMITTED',
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(150) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(150) NULL,
        CONSTRAINT [PK_DailyProductionSubmissions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028172943_AddDailyProductionTracking'
)
BEGIN
    CREATE TABLE [DailyProductionDetails] (
        [Id] int NOT NULL IDENTITY,
        [DailyProductionSubmissionId] int NOT NULL,
        [ProductId] int NULL,
        [ProductName] nvarchar(250) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [Unit] nvarchar(50) NOT NULL DEFAULT N'طن',
        [ProductionRatePerHour] decimal(18,3) NOT NULL,
        [ProductWaste] decimal(18,3) NULL,
        [NetQuantity] decimal(18,3) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DailyProductionDetails] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DailyProductionDetails_DailyProductionSubmissions_DailyProductionSubmissionId] FOREIGN KEY ([DailyProductionSubmissionId]) REFERENCES [DailyProductionSubmissions] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028172943_AddDailyProductionTracking'
)
BEGIN
    CREATE INDEX [IX_DailyProductionDetails_DailyProductionSubmissionId] ON [DailyProductionDetails] ([DailyProductionSubmissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028172943_AddDailyProductionTracking'
)
BEGIN
    CREATE INDEX [IX_DailyProductionDetails_ProductName] ON [DailyProductionDetails] ([ProductName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028172943_AddDailyProductionTracking'
)
BEGIN
    CREATE INDEX [IX_DailyProductionSubmissions_EmployeeEmail] ON [DailyProductionSubmissions] ([EmployeeEmail]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028172943_AddDailyProductionTracking'
)
BEGIN
    CREATE INDEX [IX_DailyProductionSubmissions_ProductionDate] ON [DailyProductionSubmissions] ([ProductionDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028172943_AddDailyProductionTracking'
)
BEGIN
    CREATE INDEX [IX_DailyProductionSubmissions_ProductionReferenceNumber] ON [DailyProductionSubmissions] ([ProductionReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028172943_AddDailyProductionTracking'
)
BEGIN
    CREATE INDEX [IX_DailyProductionSubmissions_Shift] ON [DailyProductionSubmissions] ([Shift]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028172943_AddDailyProductionTracking'
)
BEGIN
    CREATE INDEX [IX_DailyProductionSubmissions_Status] ON [DailyProductionSubmissions] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028172943_AddDailyProductionTracking'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DailyProductionSubmissions_SubmissionNumber] ON [DailyProductionSubmissions] ([SubmissionNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028172943_AddDailyProductionTracking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028172943_AddDailyProductionTracking', N'8.0.8');
END;
GO

COMMIT;
GO

