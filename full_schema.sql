CREATE TABLE [AttendanceSubmissions] (
    [Id] int NOT NULL IDENTITY,
    [AttendanceDate] datetime2 NOT NULL,
    [SubmittedByUserId] uniqueidentifier NULL,
    [SubmittedBy] nvarchar(150) NOT NULL,
    [SubmittedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [ApprovalStatus] nvarchar(50) NOT NULL DEFAULT N'PENDING',
    [ReviewedByUserId] uniqueidentifier NULL,
    [ReviewedBy] nvarchar(150) NULL,
    [ReviewedAt] datetime2 NULL,
    [RejectionReason] nvarchar(1000) NULL,
    [Notes] nvarchar(1000) NULL,
    [TotalEmployeesCount] int NOT NULL,
    [PresentCount] int NOT NULL,
    [AbsentCount] int NOT NULL,
    CONSTRAINT [PK_AttendanceSubmissions] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [BankTransactions] (
    [Id] int NOT NULL IDENTITY,
    [TransactionDate] datetime2 NOT NULL,
    [TransactionType] nvarchar(50) NOT NULL,
    [Direction] nvarchar(20) NOT NULL DEFAULT N'In',
    [Amount] decimal(18,2) NOT NULL,
    [BankName] nvarchar(100) NOT NULL,
    [ReferenceNumber] nvarchar(100) NULL,
    [Description] nvarchar(500) NOT NULL,
    [CreatedBy] nvarchar(150) NOT NULL,
    [CreatedByUserId] uniqueidentifier NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_BankTransactions] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ChatMessages] (
    [Id] uniqueidentifier NOT NULL,
    [SenderId] uniqueidentifier NULL,
    [SenderName] nvarchar(150) NOT NULL,
    [SenderUsername] nvarchar(50) NULL,
    [SenderRole] nvarchar(50) NOT NULL,
    [RecipientId] uniqueidentifier NULL,
    [RecipientName] nvarchar(150) NOT NULL,
    [RecipientUsername] nvarchar(50) NULL,
    [RecipientRole] nvarchar(50) NULL,
    [MessageText] nvarchar(max) NOT NULL,
    [HasAttachment] bit NOT NULL,
    [IsFromAccountant] bit NOT NULL,
    [IsReadByAccountant] bit NOT NULL,
    [IsReadByUser] bit NOT NULL,
    [SentAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [ClientMessageId] nvarchar(64) NULL,
    [MessageType] nvarchar(50) NOT NULL DEFAULT N'Text',
    [Status] nvarchar(max) NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [LiveSessionId] uniqueidentifier NULL,
    CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id])
);
GO


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
GO


CREATE TABLE [Expenses] (
    [Id] int NOT NULL IDENTITY,
    [ExpenseDate] datetime2 NOT NULL,
    [Category] nvarchar(100) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Title] nvarchar(300) NOT NULL,
    [Beneficiary] nvarchar(200) NOT NULL,
    [ReferenceNumber] nvarchar(100) NULL,
    [PaymentMethod] nvarchar(50) NOT NULL DEFAULT N'Bank Transfer',
    [CreatedBy] nvarchar(150) NOT NULL,
    [CreatedByUserId] uniqueidentifier NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_Expenses] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [FormApprovalHistories] (
    [Id] int NOT NULL IDENTITY,
    [FormType] nvarchar(50) NOT NULL,
    [FormId] int NOT NULL,
    [PONumber] nvarchar(100) NULL,
    [TransactionOrMovementNumber] nvarchar(50) NULL,
    [PreviousStatus] nvarchar(50) NOT NULL,
    [NewStatus] nvarchar(50) NOT NULL,
    [Action] nvarchar(50) NOT NULL,
    [ActorName] nvarchar(150) NULL,
    [ActorUserId] uniqueidentifier NULL,
    [ActorRole] nvarchar(50) NULL,
    [Comment] nvarchar(1000) NULL,
    [Timestamp] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_FormApprovalHistories] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [LaboratoryProductionSequences] (
    [SequenceName] nvarchar(50) NOT NULL,
    [NextValue] int NOT NULL DEFAULT 1,
    CONSTRAINT [PK_LaboratoryProductionSequences] PRIMARY KEY ([SequenceName])
);
GO


CREATE TABLE [LaboratoryUserSampleSequences] (
    [UserId] uniqueidentifier NOT NULL,
    [NextSampleNumber] int NOT NULL DEFAULT 1,
    [UpdatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_LaboratoryUserSampleSequences] PRIMARY KEY ([UserId])
);
GO


CREATE TABLE [LiveLocationSessions] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [MessageId] uniqueidentifier NOT NULL,
    [StartedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [ExpiresAt] datetime2 NOT NULL,
    [EndedAt] datetime2 NULL,
    [IsActive] bit NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [LastLatitude] float NOT NULL,
    [LastLongitude] float NOT NULL,
    [DurationMinutes] int NOT NULL,
    [UpdatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_LiveLocationSessions] PRIMARY KEY ([Id])
);
GO


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
GO


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
GO


CREATE TABLE [ProductionSequences] (
    [SequenceType] nvarchar(50) NOT NULL,
    [NextValue] int NOT NULL DEFAULT 1,
    CONSTRAINT [PK_ProductionSequences] PRIMARY KEY ([SequenceType])
);
GO


CREATE TABLE [PurchaseOrders] (
    [Id] int NOT NULL IDENTITY,
    [PONumber] nvarchar(100) NOT NULL,
    [Supplier] nvarchar(200) NOT NULL,
    [OrderDate] datetime2 NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [PricePerTon] decimal(18,2) NOT NULL,
    [TonPrice] decimal(18,3) NOT NULL DEFAULT 0.0,
    [TotalValue] decimal(18,2) NOT NULL,
    [InvoiceNumber] nvarchar(100) NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [UpdatedAt] datetime2 NULL,
    [Destination] nvarchar(200) NOT NULL DEFAULT N'Cairo Factory',
    [RemainingQuantity] decimal(18,3) NOT NULL,
    [PaymentStatus] nvarchar(50) NOT NULL DEFAULT N'غير مدفوع',
    [PaymentStatusUpdatedAt] datetime2 NULL,
    [PaymentStatusUpdatedBy] nvarchar(150) NULL,
    CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [RawMaterials] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [InitialQuantity] decimal(18,3) NOT NULL DEFAULT 0.0,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [CreatedBy] nvarchar(150) NULL,
    [CreatedByUserId] uniqueidentifier NULL,
    [MaterialType] nvarchar(50) NOT NULL DEFAULT N'MECHANICAL',
    CONSTRAINT [PK_RawMaterials] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [StorageClients] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [PhoneNumber] nvarchar(50) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_StorageClients] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [StorageLocations] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_StorageLocations] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [StorageSequences] (
    [SequenceType] nvarchar(50) NOT NULL,
    [NextValue] int NOT NULL,
    CONSTRAINT [PK_StorageSequences] PRIMARY KEY ([SequenceType])
);
GO


CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [FullName] nvarchar(150) NOT NULL,
    [Username] nvarchar(50) NOT NULL,
    [Email] nvarchar(150) NOT NULL,
    [CountryCode] nvarchar(10) NOT NULL DEFAULT N'+20',
    [PhoneNumber] nvarchar(30) NOT NULL,
    [PasswordHash] nvarchar(256) NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [Role] nvarchar(50) NOT NULL DEFAULT N'Employee',
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AttendanceAttachments] (
    [Id] int NOT NULL IDENTITY,
    [AttendanceSubmissionId] int NOT NULL,
    [FileName] nvarchar(255) NOT NULL,
    [OriginalFileName] nvarchar(255) NOT NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [FileType] nvarchar(100) NOT NULL,
    [FileSize] bigint NOT NULL,
    [UploadedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_AttendanceAttachments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AttendanceAttachments_AttendanceSubmissions_AttendanceSubmissionId] FOREIGN KEY ([AttendanceSubmissionId]) REFERENCES [AttendanceSubmissions] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ChatAttachments] (
    [Id] uniqueidentifier NOT NULL,
    [MessageId] uniqueidentifier NOT NULL,
    [SenderName] nvarchar(150) NOT NULL,
    [RecipientName] nvarchar(150) NOT NULL,
    [FileName] nvarchar(260) NOT NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [FileType] nvarchar(50) NOT NULL,
    [FileSizeBytes] bigint NOT NULL,
    [UploadedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_ChatAttachments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ChatAttachments_ChatMessages_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [ChatMessages] ([Id]) ON DELETE CASCADE
);
GO


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
GO


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
GO


CREATE TABLE [PurchaseOrderPaymentAudits] (
    [Id] int NOT NULL IDENTITY,
    [PurchaseOrderId] int NULL,
    [PONumber] nvarchar(100) NOT NULL,
    [PreviousStatus] nvarchar(50) NOT NULL,
    [NewStatus] nvarchar(50) NOT NULL,
    [ChangedBy] nvarchar(150) NOT NULL,
    [ChangedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [Notes] nvarchar(500) NULL,
    CONSTRAINT [PK_PurchaseOrderPaymentAudits] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseOrderPaymentAudits_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [StorageTransactions] (
    [Id] int NOT NULL IDENTITY,
    [MovementNumber] int NOT NULL,
    [TransactionDate] datetime2 NOT NULL,
    [Material] nvarchar(100) NOT NULL DEFAULT N'powder',
    [Action] nvarchar(50) NOT NULL DEFAULT N'Import',
    [ServiceType] nvarchar(50) NOT NULL DEFAULT N'إيداع',
    [ReferenceNumber] int NOT NULL,
    [IsExportRelatedToPO] bit NOT NULL DEFAULT CAST(0 AS bit),
    [PONumber] nvarchar(100) NULL,
    [PurchaseOrderId] int NULL,
    [IncomingQuantity] decimal(18,3) NOT NULL DEFAULT 0.0,
    [OutgoingQuantity] decimal(18,3) NOT NULL DEFAULT 0.0,
    [OpeningBalance] decimal(18,3) NOT NULL DEFAULT 0.0,
    [ClosingBalance] decimal(18,3) NOT NULL DEFAULT 0.0,
    [StorageLocation] nvarchar(200) NOT NULL DEFAULT N'مستودع الخامات الرئيسي',
    [DestinationStorageLocation] nvarchar(200) NULL,
    [InternalWeighbridgeReference] nvarchar(100) NOT NULL DEFAULT N'',
    [InternalLoadedWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
    [InternalEmptyWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
    [InternalNetWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
    [InternalEmptyWeightTime] datetime2 NULL,
    [InternalLoadedWeightTime] datetime2 NULL,
    [HasExternalWeighbridge] bit NOT NULL DEFAULT CAST(0 AS bit),
    [ExternalWeighbridgeReference] nvarchar(100) NULL,
    [ExternalLoadedWeight] decimal(18,3) NULL,
    [ExternalEmptyWeight] decimal(18,3) NULL,
    [ExternalNetWeight] decimal(18,3) NULL,
    [ExternalEmptyWeightTime] datetime2 NULL,
    [ExternalLoadedWeightTime] datetime2 NULL,
    [WeighbridgeAttachmentPath] nvarchar(500) NULL,
    [WeighbridgeFileName] nvarchar(260) NULL,
    [WeighbridgeFileType] nvarchar(50) NULL,
    [WeighbridgeFileSize] bigint NOT NULL DEFAULT CAST(0 AS bigint),
    [ReceivingEmployee] nvarchar(150) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedBy] nvarchar(150) NULL,
    [CreatedByUserId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [ApprovalStatus] nvarchar(50) NOT NULL DEFAULT N'PENDING',
    [ReviewedBy] nvarchar(150) NULL,
    [ReviewedByUserId] uniqueidentifier NULL,
    [ReviewedAt] datetime2 NULL,
    [RejectionReason] nvarchar(1000) NULL,
    CONSTRAINT [PK_StorageTransactions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StorageTransactions_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE SET NULL
);
GO


CREATE TABLE [Transportation] (
    [Id] int NOT NULL IDENTITY,
    [TransactionNumber] int NOT NULL,
    [TransactionDate] datetime2 NOT NULL,
    [CarNumber] nvarchar(50) NOT NULL,
    [DriverName] nvarchar(150) NOT NULL,
    [Source] nvarchar(200) NOT NULL,
    [PONumber] nvarchar(100) NOT NULL,
    [MaterialType] nvarchar(100) NOT NULL,
    [WeightBeforeLoading] decimal(18,3) NOT NULL,
    [WeightWhenEnteringFactory] decimal(18,3) NOT NULL,
    [WeightWhenExitingFactory] decimal(18,3) NOT NULL,
    [ExpectedWeight] decimal(18,3) NOT NULL,
    [NetWeight] decimal(18,3) NOT NULL,
    [CompanyName] nvarchar(200) NOT NULL DEFAULT N'',
    [InternalWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
    [ExternalLoadedWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
    [ExternalEmptyWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
    [ExternalNetWeight] decimal(18,3) NOT NULL DEFAULT 0.0,
    [WasteQuantity] decimal(18,3) NOT NULL DEFAULT 0.0,
    [DriverPhone] nvarchar(30) NULL,
    [DepartureTime] datetime2 NULL,
    [ArrivalTime] datetime2 NULL,
    [ShippingSupervisor] nvarchar(100) NOT NULL DEFAULT N'محمد ناجح',
    [DepartmentSupervisor] nvarchar(100) NOT NULL DEFAULT N'ازهر محمد',
    [RecipientName] nvarchar(150) NULL,
    [RecipientTitle] nvarchar(100) NULL,
    [ReceivedDate] datetime2 NULL,
    [InternalWeighbridgeCode] nvarchar(100) NULL,
    [InternalEmptyWeightTime] datetime2 NULL,
    [InternalLoadedWeightTime] datetime2 NULL,
    [ExternalWeighbridgeCode] nvarchar(100) NULL,
    [ExternalEmptyWeightTime] datetime2 NULL,
    [ExternalLoadedWeightTime] datetime2 NULL,
    [ShippingDurationMinutes] int NULL,
    [Destination] nvarchar(200) NOT NULL,
    [TransactionStatus] nvarchar(50) NOT NULL,
    [ReceivingStatus] nvarchar(50) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [ProductionReferenceNumber] int NULL,
    [Ikramiya] decimal(18,3) NOT NULL DEFAULT 0.0,
    [CustodyAmount] decimal(18,3) NOT NULL DEFAULT 0.0,
    [InvoiceAttachmentPath] nvarchar(500) NULL,
    [InvoiceFileName] nvarchar(260) NULL,
    [InvoiceFileType] nvarchar(50) NULL,
    [InvoiceFileSize] bigint NOT NULL DEFAULT CAST(0 AS bigint),
    [WeighbridgeAttachmentPath] nvarchar(500) NULL,
    [WeighbridgeFileName] nvarchar(260) NULL,
    [WeighbridgeFileType] nvarchar(50) NULL,
    [WeighbridgeFileSize] bigint NOT NULL DEFAULT CAST(0 AS bigint),
    [DailySupplyAttachmentPath] nvarchar(500) NULL,
    [DailySupplyFileName] nvarchar(260) NULL,
    [DailySupplyFileType] nvarchar(50) NULL,
    [DailySupplyFileSize] bigint NOT NULL DEFAULT CAST(0 AS bigint),
    [BillOfLadingAttachmentPath] nvarchar(500) NULL,
    [BillOfLadingFileName] nvarchar(260) NULL,
    [BillOfLadingFileType] nvarchar(50) NULL,
    [BillOfLadingFileSize] bigint NOT NULL DEFAULT CAST(0 AS bigint),
    [SubmittedBy] nvarchar(150) NULL,
    [SubmittedByUserId] uniqueidentifier NULL,
    [PurchaseOrderId] int NULL,
    [DateOfSubmit] datetime2 NOT NULL,
    [ApprovalStatus] nvarchar(50) NOT NULL DEFAULT N'PENDING',
    [ReviewedBy] nvarchar(150) NULL,
    [ReviewedByUserId] uniqueidentifier NULL,
    [ReviewedAt] datetime2 NULL,
    [RejectionReason] nvarchar(1000) NULL,
    CONSTRAINT [PK_Transportation] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Transportation_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE SET NULL
);
GO


CREATE TABLE [StorageClientDestinations] (
    [Id] int NOT NULL IDENTITY,
    [StorageClientId] int NOT NULL,
    [DestinationName] nvarchar(200) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_StorageClientDestinations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StorageClientDestinations_StorageClients_StorageClientId] FOREIGN KEY ([StorageClientId]) REFERENCES [StorageClients] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AttendanceRecords] (
    [Id] int NOT NULL IDENTITY,
    [UserId] uniqueidentifier NULL,
    [EmployeeName] nvarchar(150) NULL,
    [EmployeeRole] nvarchar(100) NULL,
    [IsTestData] bit NOT NULL DEFAULT CAST(1 AS bit),
    [Date] datetime2 NOT NULL,
    [Status] nvarchar(50) NOT NULL DEFAULT N'Present',
    [CheckInTime] datetime2 NULL,
    [CheckOutTime] datetime2 NULL,
    [Notes] nvarchar(500) NULL,
    [AttendanceSubmissionId] int NULL,
    [ApprovalStatus] nvarchar(50) NOT NULL DEFAULT N'PENDING',
    [RejectionReason] nvarchar(1000) NULL,
    [ReviewedByUserId] uniqueidentifier NULL,
    [ReviewedBy] nvarchar(150) NULL,
    [ReviewedAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_AttendanceRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AttendanceRecords_AttendanceSubmissions_AttendanceSubmissionId] FOREIGN KEY ([AttendanceSubmissionId]) REFERENCES [AttendanceSubmissions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AttendanceRecords_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
);
GO


CREATE TABLE [EmployeeFinancialRecords] (
    [Id] int NOT NULL IDENTITY,
    [UserId] uniqueidentifier NOT NULL,
    [BasicSalary] decimal(18,2) NOT NULL DEFAULT 0.0,
    [Allowances] decimal(18,2) NOT NULL DEFAULT 0.0,
    [InsuranceDeductions] decimal(18,2) NOT NULL DEFAULT 0.0,
    [NetSalary] decimal(18,2) NOT NULL DEFAULT 0.0,
    [EffectiveFrom] datetime2 NOT NULL,
    [PaymentMethod] nvarchar(50) NOT NULL DEFAULT N'Bank Transfer',
    [BankAccountNumber] nvarchar(100) NULL,
    [Notes] nvarchar(500) NULL,
    [UpdatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_EmployeeFinancialRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeFinancialRecords_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [EmployeeLendings] (
    [Id] int NOT NULL IDENTITY,
    [UserId] uniqueidentifier NOT NULL,
    [LendingAmount] decimal(18,2) NOT NULL,
    [PaidAmount] decimal(18,2) NOT NULL DEFAULT 0.0,
    [RemainingAmount] decimal(18,2) NOT NULL,
    [RequestDate] datetime2 NOT NULL,
    [ApprovalStatus] nvarchar(50) NOT NULL DEFAULT N'معتمد',
    [ApprovedBy] nvarchar(150) NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_EmployeeLendings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeLendings_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [LaboratoryRawMaterialReports] (
    [Id] int NOT NULL IDENTITY,
    [ProductionReferenceNumber] int NOT NULL,
    [SampleNumber] int NOT NULL,
    [SampleName] nvarchar(250) NOT NULL DEFAULT N'سيليكا باودر (Micro Silica Powder)',
    [SampleDescription] nvarchar(500) NULL,
    [BatchNumber] nvarchar(100) NULL,
    [SampleReceivedDate] datetime2 NULL,
    [ReportIssueDate] datetime2 NULL,
    [LabManagerName] nvarchar(150) NULL,
    [LabManagerSignature] nvarchar(150) NULL,
    [LabManagerDate] datetime2 NULL,
    [ComplyStatus] nvarchar(50) NOT NULL DEFAULT N'COMPLY',
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
GO


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
GO


CREATE TABLE [StorageTransactionMovements] (
    [Id] int NOT NULL IDENTITY,
    [StorageTransactionId] int NOT NULL,
    [TransportationMovementNumber] int NOT NULL,
    [TransportationId] int NULL,
    CONSTRAINT [PK_StorageTransactionMovements] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StorageTransactionMovements_StorageTransactions_StorageTransactionId] FOREIGN KEY ([StorageTransactionId]) REFERENCES [StorageTransactions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_StorageTransactionMovements_Transportation_TransportationId] FOREIGN KEY ([TransportationId]) REFERENCES [Transportation] ([Id])
);
GO


CREATE TABLE [StorageOperations] (
    [Id] int NOT NULL IDENTITY,
    [OperationType] nvarchar(50) NOT NULL,
    [TransactionNumber] int NOT NULL,
    [TransactionDate] datetime2 NOT NULL,
    [StorageLocationId] int NOT NULL,
    [StorageLocationName] nvarchar(200) NOT NULL,
    [MaterialType] nvarchar(50) NULL,
    [ExternalReportedWeight] decimal(18,3) NULL,
    [InternalLoadedWeight] decimal(18,3) NULL,
    [InternalEmptyWeight] decimal(18,3) NULL,
    [InternalNetWeight] decimal(18,3) NULL,
    [InternalWeighingTime] datetime2 NULL,
    [WeighbridgeReference] nvarchar(100) NULL,
    [WeighingResponsibleEmployee] nvarchar(150) NULL,
    [WeightDifference] decimal(18,3) NULL,
    [WeightDifferencePercentage] decimal(18,2) NULL,
    [IsDiscrepancyFlagged] bit NOT NULL,
    [DiscrepancyExplanation] nvarchar(max) NULL,
    [TransportationId] int NULL,
    [ProductionReferenceNumber] int NULL,
    [Source] nvarchar(max) NULL,
    [SourcePhone] nvarchar(max) NULL,
    [ClientId] int NULL,
    [ClientName] nvarchar(max) NULL,
    [DestinationId] int NULL,
    [DestinationName] nvarchar(max) NULL,
    [ClientPhone] nvarchar(max) NULL,
    [DestinationDepartment] nvarchar(max) NULL,
    [WeighbridgeAttachmentPath] nvarchar(max) NULL,
    [WeighbridgeFileName] nvarchar(max) NULL,
    [WeighbridgeFileType] nvarchar(max) NULL,
    [WeighbridgeFileSize] bigint NOT NULL,
    [QocAttachmentPath] nvarchar(max) NULL,
    [QocFileName] nvarchar(max) NULL,
    [QocFileType] nvarchar(max) NULL,
    [QocFileSize] bigint NOT NULL,
    [DocumentAttachmentPath] nvarchar(max) NULL,
    [DocumentFileName] nvarchar(max) NULL,
    [DocumentFileType] nvarchar(max) NULL,
    [DocumentFileSize] bigint NOT NULL,
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
    CONSTRAINT [FK_StorageOperations_StorageLocations_StorageLocationId] FOREIGN KEY ([StorageLocationId]) REFERENCES [StorageLocations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StorageOperations_Transportation_TransportationId] FOREIGN KEY ([TransportationId]) REFERENCES [Transportation] ([Id]) ON DELETE SET NULL
);
GO


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
GO


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
GO


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
GO


CREATE TABLE [ProductionWorkInProcess] (
    [Id] int NOT NULL IDENTITY,
    [StorageOperationId] int NOT NULL,
    [StorageOperationItemId] int NOT NULL,
    [RawMaterialId] int NOT NULL,
    [ProductionReferenceNumber] int NULL,
    [TrackingNumber] nvarchar(100) NOT NULL,
    [MaterialQuantityIssued] decimal(18,3) NOT NULL,
    [SourceStorageLocation] nvarchar(200) NOT NULL,
    [TransferDate] datetime2 NOT NULL,
    [Status] nvarchar(50) NOT NULL DEFAULT N'قيد التشغيل',
    [FinalProductName] nvarchar(max) NULL,
    [FinalProductQuantity] decimal(18,3) NULL,
    [WasteQuantity] decimal(18,3) NULL,
    [WastePercentage] decimal(18,2) NULL,
    [WasteClassification] nvarchar(50) NOT NULL DEFAULT N'ORDINARY_WASTE',
    [RecoverableProductName] nvarchar(200) NULL,
    [RecoverableMaterialId] int NULL,
    [RecoverableQuantity] decimal(18,3) NULL,
    [RecoverableUnit] nvarchar(50) NOT NULL DEFAULT N'طن',
    [DestinationStorageLocationId] int NULL,
    [DestinationStorageLocationName] nvarchar(200) NULL,
    [IsRecoveredStockPosted] bit NOT NULL,
    [RecoveredStockPostedAt] datetime2 NULL,
    [OutputResponsibleEmployee] nvarchar(150) NULL,
    [ClassificationHistory] nvarchar(max) NULL,
    [Notes] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [CreatedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_ProductionWorkInProcess] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductionWorkInProcess_RawMaterials_RawMaterialId] FOREIGN KEY ([RawMaterialId]) REFERENCES [RawMaterials] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ProductionWorkInProcess_RawMaterials_RecoverableMaterialId] FOREIGN KEY ([RecoverableMaterialId]) REFERENCES [RawMaterials] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ProductionWorkInProcess_StorageLocations_DestinationStorageLocationId] FOREIGN KEY ([DestinationStorageLocationId]) REFERENCES [StorageLocations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ProductionWorkInProcess_StorageOperationItems_StorageOperationItemId] FOREIGN KEY ([StorageOperationItemId]) REFERENCES [StorageOperationItems] ([Id]),
    CONSTRAINT [FK_ProductionWorkInProcess_StorageOperations_StorageOperationId] FOREIGN KEY ([StorageOperationId]) REFERENCES [StorageOperations] ([Id])
);
GO


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
GO


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
GO


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
GO


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
GO


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
GO


CREATE INDEX [IX_AttendanceAttachments_AttendanceSubmissionId] ON [AttendanceAttachments] ([AttendanceSubmissionId]);
GO


CREATE INDEX [IX_AttendanceRecords_AttendanceSubmissionId] ON [AttendanceRecords] ([AttendanceSubmissionId]);
GO


CREATE INDEX [IX_AttendanceRecords_Date] ON [AttendanceRecords] ([Date]);
GO


CREATE INDEX [IX_AttendanceRecords_UserId] ON [AttendanceRecords] ([UserId]);
GO


CREATE INDEX [IX_AttendanceSubmissions_ApprovalStatus] ON [AttendanceSubmissions] ([ApprovalStatus]);
GO


CREATE INDEX [IX_AttendanceSubmissions_AttendanceDate] ON [AttendanceSubmissions] ([AttendanceDate]);
GO


CREATE INDEX [IX_AttendanceSubmissions_SubmittedAt] ON [AttendanceSubmissions] ([SubmittedAt]);
GO


CREATE INDEX [IX_BankTransactions_BankName] ON [BankTransactions] ([BankName]);
GO


CREATE INDEX [IX_BankTransactions_Direction] ON [BankTransactions] ([Direction]);
GO


CREATE INDEX [IX_BankTransactions_TransactionDate] ON [BankTransactions] ([TransactionDate]);
GO


CREATE INDEX [IX_ChatAttachments_MessageId] ON [ChatAttachments] ([MessageId]);
GO


CREATE INDEX [IX_ChatMessages_LiveSessionId] ON [ChatMessages] ([LiveSessionId]);
GO


CREATE INDEX [IX_ChatMessages_RecipientId_IsFromAccountant_IsReadByUser] ON [ChatMessages] ([RecipientId], [IsFromAccountant], [IsReadByUser]);
GO


CREATE INDEX [IX_ChatMessages_RecipientId_SentAt] ON [ChatMessages] ([RecipientId], [SentAt]);
GO


CREATE INDEX [IX_ChatMessages_SenderId_ClientMessageId] ON [ChatMessages] ([SenderId], [ClientMessageId]);
GO


CREATE INDEX [IX_ChatMessages_SenderId_IsFromAccountant_IsReadByAccountant] ON [ChatMessages] ([SenderId], [IsFromAccountant], [IsReadByAccountant]);
GO


CREATE INDEX [IX_ChatMessages_SenderId_SentAt] ON [ChatMessages] ([SenderId], [SentAt]);
GO


CREATE INDEX [IX_DailyProductionDetails_DailyProductionSubmissionId] ON [DailyProductionDetails] ([DailyProductionSubmissionId]);
GO


CREATE INDEX [IX_DailyProductionDetails_ProductName] ON [DailyProductionDetails] ([ProductName]);
GO


CREATE INDEX [IX_DailyProductionSubmissions_EmployeeEmail] ON [DailyProductionSubmissions] ([EmployeeEmail]);
GO


CREATE INDEX [IX_DailyProductionSubmissions_ProductionDate] ON [DailyProductionSubmissions] ([ProductionDate]);
GO


CREATE INDEX [IX_DailyProductionSubmissions_ProductionReferenceNumber] ON [DailyProductionSubmissions] ([ProductionReferenceNumber]);
GO


CREATE INDEX [IX_DailyProductionSubmissions_Shift] ON [DailyProductionSubmissions] ([Shift]);
GO


CREATE INDEX [IX_DailyProductionSubmissions_Status] ON [DailyProductionSubmissions] ([Status]);
GO


CREATE UNIQUE INDEX [IX_DailyProductionSubmissions_SubmissionNumber] ON [DailyProductionSubmissions] ([SubmissionNumber]);
GO


CREATE INDEX [IX_EmployeeFinancialRecords_UserId] ON [EmployeeFinancialRecords] ([UserId]);
GO


CREATE INDEX [IX_EmployeeLendings_RequestDate] ON [EmployeeLendings] ([RequestDate]);
GO


CREATE INDEX [IX_EmployeeLendings_UserId] ON [EmployeeLendings] ([UserId]);
GO


CREATE INDEX [IX_Expenses_Category] ON [Expenses] ([Category]);
GO


CREATE INDEX [IX_Expenses_ExpenseDate] ON [Expenses] ([ExpenseDate]);
GO


CREATE INDEX [IX_FinancePayments_PaymentDate] ON [FinancePayments] ([PaymentDate]);
GO


CREATE INDEX [IX_FinancePayments_PONumber] ON [FinancePayments] ([PONumber]);
GO


CREATE INDEX [IX_FinancePayments_PurchaseOrderId] ON [FinancePayments] ([PurchaseOrderId]);
GO


CREATE INDEX [IX_FormApprovalHistories_FormType_FormId] ON [FormApprovalHistories] ([FormType], [FormId]);
GO


CREATE INDEX [IX_FormApprovalHistories_Timestamp] ON [FormApprovalHistories] ([Timestamp]);
GO


CREATE INDEX [IX_LaboratoryFinalConformityCertificates_ApprovalStatus] ON [LaboratoryFinalConformityCertificates] ([ApprovalStatus]);
GO


CREATE UNIQUE INDEX [IX_LaboratoryFinalConformityCertificates_CertificateNumber] ON [LaboratoryFinalConformityCertificates] ([CertificateNumber]);
GO


CREATE INDEX [IX_LaboratoryFinalConformityCertificates_ConformityDecision] ON [LaboratoryFinalConformityCertificates] ([ConformityDecision]);
GO


CREATE INDEX [IX_LaboratoryFinalConformityCertificates_LaboratorySection] ON [LaboratoryFinalConformityCertificates] ([LaboratorySection]);
GO


CREATE INDEX [IX_LaboratoryFinalConformityCertificates_ProductionReferenceNumber] ON [LaboratoryFinalConformityCertificates] ([ProductionReferenceNumber]);
GO


CREATE INDEX [IX_LaboratoryFinalConformityCertificates_ProductionWorkInProcessId] ON [LaboratoryFinalConformityCertificates] ([ProductionWorkInProcessId]);
GO


CREATE INDEX [IX_LaboratoryFinalConformityCertificates_RawMaterialReportId] ON [LaboratoryFinalConformityCertificates] ([RawMaterialReportId]);
GO


CREATE INDEX [IX_LaboratoryInProcessReports_LaboratorySection] ON [LaboratoryInProcessReports] ([LaboratorySection]);
GO


CREATE INDEX [IX_LaboratoryInProcessReports_ProductionReferenceNumber] ON [LaboratoryInProcessReports] ([ProductionReferenceNumber]);
GO


CREATE INDEX [IX_LaboratoryInProcessReports_ProductionStage] ON [LaboratoryInProcessReports] ([ProductionStage]);
GO


CREATE INDEX [IX_LaboratoryInProcessReports_ProductionWorkInProcessId] ON [LaboratoryInProcessReports] ([ProductionWorkInProcessId]);
GO


CREATE INDEX [IX_LaboratoryInProcessReports_RawMaterialReportId] ON [LaboratoryInProcessReports] ([RawMaterialReportId]);
GO


CREATE INDEX [IX_LaboratoryInProcessReports_TestDateTime] ON [LaboratoryInProcessReports] ([TestDateTime]);
GO


CREATE INDEX [IX_LaboratoryRawMaterialReports_AnalysisDateTime] ON [LaboratoryRawMaterialReports] ([AnalysisDateTime]);
GO


CREATE INDEX [IX_LaboratoryRawMaterialReports_ApprovalStatus] ON [LaboratoryRawMaterialReports] ([ApprovalStatus]);
GO


CREATE INDEX [IX_LaboratoryRawMaterialReports_BatchNumber] ON [LaboratoryRawMaterialReports] ([BatchNumber]);
GO


CREATE INDEX [IX_LaboratoryRawMaterialReports_ComplyStatus] ON [LaboratoryRawMaterialReports] ([ComplyStatus]);
GO


CREATE INDEX [IX_LaboratoryRawMaterialReports_InspectionDecision] ON [LaboratoryRawMaterialReports] ([InspectionDecision]);
GO


CREATE INDEX [IX_LaboratoryRawMaterialReports_LaboratorySection] ON [LaboratoryRawMaterialReports] ([LaboratorySection]);
GO


CREATE INDEX [IX_LaboratoryRawMaterialReports_ProductionReferenceNumber] ON [LaboratoryRawMaterialReports] ([ProductionReferenceNumber]);
GO


CREATE INDEX [IX_LaboratoryRawMaterialReports_PurchaseOrderId] ON [LaboratoryRawMaterialReports] ([PurchaseOrderId]);
GO


CREATE INDEX [IX_LaboratoryRawMaterialReports_RawMaterialId] ON [LaboratoryRawMaterialReports] ([RawMaterialId]);
GO


CREATE UNIQUE INDEX [IX_LaboratoryRawMaterialReports_ResponsibleUserId_SampleNumber] ON [LaboratoryRawMaterialReports] ([ResponsibleUserId], [SampleNumber]) WHERE [ResponsibleUserId] IS NOT NULL AND [SampleNumber] > 0;
GO


CREATE INDEX [IX_LaboratoryRawMaterialReports_SampleNumber] ON [LaboratoryRawMaterialReports] ([SampleNumber]);
GO


CREATE INDEX [IX_LaboratoryRawMaterialReports_SourceTransportationId] ON [LaboratoryRawMaterialReports] ([SourceTransportationId]);
GO


CREATE INDEX [IX_LiveLocationSessions_EmployeeId] ON [LiveLocationSessions] ([EmployeeId]);
GO


CREATE INDEX [IX_LiveLocationSessions_EmployeeId_IsActive] ON [LiveLocationSessions] ([EmployeeId], [IsActive]);
GO


CREATE INDEX [IX_LiveLocationSessions_MessageId] ON [LiveLocationSessions] ([MessageId]);
GO


CREATE INDEX [IX_LiveLocationSessions_UserId] ON [LiveLocationSessions] ([UserId]);
GO


CREATE INDEX [IX_Management_Date] ON [Management] ([Date]);
GO


CREATE UNIQUE INDEX [IX_ProductionBatches_BatchNumber] ON [ProductionBatches] ([BatchNumber]);
GO


CREATE INDEX [IX_ProductionBatches_ProductionOrderId] ON [ProductionBatches] ([ProductionOrderId]);
GO


CREATE INDEX [IX_ProductionBatches_ProductionReferenceNumber] ON [ProductionBatches] ([ProductionReferenceNumber]);
GO


CREATE INDEX [IX_ProductionBatches_ProductionWorkInProcessId] ON [ProductionBatches] ([ProductionWorkInProcessId]);
GO


CREATE INDEX [IX_ProductionBatches_QualityHold] ON [ProductionBatches] ([QualityHold]);
GO


CREATE INDEX [IX_ProductionBatches_Status] ON [ProductionBatches] ([Status]);
GO


CREATE INDEX [IX_ProductionOrderItems_ProductionOrderId] ON [ProductionOrderItems] ([ProductionOrderId]);
GO


CREATE INDEX [IX_ProductionOrderItems_RawMaterialId] ON [ProductionOrderItems] ([RawMaterialId]);
GO


CREATE INDEX [IX_ProductionOrders_CreatedAt] ON [ProductionOrders] ([CreatedAt]);
GO


CREATE UNIQUE INDEX [IX_ProductionOrders_OrderNumber] ON [ProductionOrders] ([OrderNumber]);
GO


CREATE INDEX [IX_ProductionOrders_ProductionReferenceNumber] ON [ProductionOrders] ([ProductionReferenceNumber]);
GO


CREATE INDEX [IX_ProductionOrders_Status] ON [ProductionOrders] ([Status]);
GO


CREATE INDEX [IX_ProductionOrders_StorageLocationId] ON [ProductionOrders] ([StorageLocationId]);
GO


CREATE INDEX [IX_ProductionOrders_StorageOperationId] ON [ProductionOrders] ([StorageOperationId]);
GO


CREATE INDEX [IX_ProductionOutputs_Classification] ON [ProductionOutputs] ([Classification]);
GO


CREATE INDEX [IX_ProductionOutputs_DestinationStorageLocationId] ON [ProductionOutputs] ([DestinationStorageLocationId]);
GO


CREATE INDEX [IX_ProductionOutputs_IsStorageReceived] ON [ProductionOutputs] ([IsStorageReceived]);
GO


CREATE INDEX [IX_ProductionOutputs_LaboratoryCertificateId] ON [ProductionOutputs] ([LaboratoryCertificateId]);
GO


CREATE INDEX [IX_ProductionOutputs_LaboratoryConformityStatus] ON [ProductionOutputs] ([LaboratoryConformityStatus]);
GO


CREATE UNIQUE INDEX [IX_ProductionOutputs_OutputNumber] ON [ProductionOutputs] ([OutputNumber]);
GO


CREATE INDEX [IX_ProductionOutputs_ProductionBatchId] ON [ProductionOutputs] ([ProductionBatchId]);
GO


CREATE INDEX [IX_ProductionOutputs_ProductionOrderId] ON [ProductionOutputs] ([ProductionOrderId]);
GO


CREATE INDEX [IX_ProductionOutputs_ProductionReferenceNumber] ON [ProductionOutputs] ([ProductionReferenceNumber]);
GO


CREATE INDEX [IX_ProductionOutputs_StorageOperationId] ON [ProductionOutputs] ([StorageOperationId]);
GO


CREATE INDEX [IX_ProductionReworks_OriginalProductionBatchId] ON [ProductionReworks] ([OriginalProductionBatchId]);
GO


CREATE INDEX [IX_ProductionReworks_OriginalProductionOrderId] ON [ProductionReworks] ([OriginalProductionOrderId]);
GO


CREATE INDEX [IX_ProductionReworks_OriginalProductionOutputId] ON [ProductionReworks] ([OriginalProductionOutputId]);
GO


CREATE INDEX [IX_ProductionReworks_ProductionReferenceNumber] ON [ProductionReworks] ([ProductionReferenceNumber]);
GO


CREATE UNIQUE INDEX [IX_ProductionReworks_ReworkNumber] ON [ProductionReworks] ([ReworkNumber]);
GO


CREATE INDEX [IX_ProductionReworks_Status] ON [ProductionReworks] ([Status]);
GO


CREATE INDEX [IX_ProductionWorkInProcess_DestinationStorageLocationId] ON [ProductionWorkInProcess] ([DestinationStorageLocationId]);
GO


CREATE INDEX [IX_ProductionWorkInProcess_ProductionReferenceNumber] ON [ProductionWorkInProcess] ([ProductionReferenceNumber]);
GO


CREATE INDEX [IX_ProductionWorkInProcess_RawMaterialId] ON [ProductionWorkInProcess] ([RawMaterialId]);
GO


CREATE INDEX [IX_ProductionWorkInProcess_RecoverableMaterialId] ON [ProductionWorkInProcess] ([RecoverableMaterialId]);
GO


CREATE INDEX [IX_ProductionWorkInProcess_Status] ON [ProductionWorkInProcess] ([Status]);
GO


CREATE INDEX [IX_ProductionWorkInProcess_StorageOperationId] ON [ProductionWorkInProcess] ([StorageOperationId]);
GO


CREATE INDEX [IX_ProductionWorkInProcess_StorageOperationItemId] ON [ProductionWorkInProcess] ([StorageOperationItemId]);
GO


CREATE UNIQUE INDEX [IX_ProductionWorkInProcess_TrackingNumber] ON [ProductionWorkInProcess] ([TrackingNumber]);
GO


CREATE INDEX [IX_PurchaseOrderPaymentAudits_ChangedAt] ON [PurchaseOrderPaymentAudits] ([ChangedAt]);
GO


CREATE INDEX [IX_PurchaseOrderPaymentAudits_PONumber] ON [PurchaseOrderPaymentAudits] ([PONumber]);
GO


CREATE INDEX [IX_PurchaseOrderPaymentAudits_PurchaseOrderId] ON [PurchaseOrderPaymentAudits] ([PurchaseOrderId]);
GO


CREATE INDEX [IX_PurchaseOrders_InvoiceNumber] ON [PurchaseOrders] ([InvoiceNumber]);
GO


CREATE INDEX [IX_PurchaseOrders_OrderDate] ON [PurchaseOrders] ([OrderDate]);
GO


CREATE INDEX [IX_PurchaseOrders_PaymentStatus] ON [PurchaseOrders] ([PaymentStatus]);
GO


CREATE UNIQUE INDEX [IX_PurchaseOrders_PONumber] ON [PurchaseOrders] ([PONumber]);
GO


CREATE INDEX [IX_PurchaseOrders_Supplier] ON [PurchaseOrders] ([Supplier]);
GO


CREATE UNIQUE INDEX [IX_RawMaterials_Name] ON [RawMaterials] ([Name]);
GO


CREATE INDEX [IX_StorageClientDestinations_StorageClientId] ON [StorageClientDestinations] ([StorageClientId]);
GO


CREATE UNIQUE INDEX [IX_StorageClients_Name] ON [StorageClients] ([Name]);
GO


CREATE INDEX [IX_StorageInventoryLedger_MovementDate] ON [StorageInventoryLedger] ([MovementDate]);
GO


CREATE INDEX [IX_StorageInventoryLedger_RawMaterialId] ON [StorageInventoryLedger] ([RawMaterialId]);
GO


CREATE INDEX [IX_StorageInventoryLedger_StorageLocationId_RawMaterialId] ON [StorageInventoryLedger] ([StorageLocationId], [RawMaterialId]);
GO


CREATE INDEX [IX_StorageInventoryLedger_StorageOperationId] ON [StorageInventoryLedger] ([StorageOperationId]);
GO


CREATE INDEX [IX_StorageInventoryLedger_StorageOperationItemId] ON [StorageInventoryLedger] ([StorageOperationItemId]);
GO


CREATE INDEX [IX_StorageItemTransactions_RawMaterialId] ON [StorageItemTransactions] ([RawMaterialId]);
GO


CREATE UNIQUE INDEX [IX_StorageItemTransactions_SourceTransportationId] ON [StorageItemTransactions] ([SourceTransportationId]) WHERE [SourceTransportationId] IS NOT NULL;
GO


CREATE INDEX [IX_StorageItemTransactions_TransactionDate] ON [StorageItemTransactions] ([TransactionDate]);
GO


CREATE UNIQUE INDEX [IX_StorageLocations_Name] ON [StorageLocations] ([Name]);
GO


CREATE INDEX [IX_StorageOperationItems_RawMaterialId] ON [StorageOperationItems] ([RawMaterialId]);
GO


CREATE INDEX [IX_StorageOperationItems_StorageOperationId] ON [StorageOperationItems] ([StorageOperationId]);
GO


CREATE INDEX [IX_StorageOperations_ApprovalStatus] ON [StorageOperations] ([ApprovalStatus]);
GO


CREATE INDEX [IX_StorageOperations_ClientId] ON [StorageOperations] ([ClientId]);
GO


CREATE INDEX [IX_StorageOperations_DestinationId] ON [StorageOperations] ([DestinationId]);
GO


CREATE UNIQUE INDEX [IX_StorageOperations_OperationType_TransactionNumber] ON [StorageOperations] ([OperationType], [TransactionNumber]);
GO


CREATE INDEX [IX_StorageOperations_ProductionReferenceNumber] ON [StorageOperations] ([ProductionReferenceNumber]);
GO


CREATE INDEX [IX_StorageOperations_StorageLocationId] ON [StorageOperations] ([StorageLocationId]);
GO


CREATE INDEX [IX_StorageOperations_TransactionDate] ON [StorageOperations] ([TransactionDate]);
GO


CREATE INDEX [IX_StorageOperations_TransportationId] ON [StorageOperations] ([TransportationId]);
GO


CREATE INDEX [IX_StorageTransactionMovements_StorageTransactionId] ON [StorageTransactionMovements] ([StorageTransactionId]);
GO


CREATE INDEX [IX_StorageTransactionMovements_TransportationId] ON [StorageTransactionMovements] ([TransportationId]);
GO


CREATE INDEX [IX_StorageTransactionMovements_TransportationMovementNumber] ON [StorageTransactionMovements] ([TransportationMovementNumber]);
GO


CREATE INDEX [IX_StorageTransactions_Action] ON [StorageTransactions] ([Action]);
GO


CREATE INDEX [IX_StorageTransactions_Material] ON [StorageTransactions] ([Material]);
GO


CREATE INDEX [IX_StorageTransactions_MovementNumber] ON [StorageTransactions] ([MovementNumber]);
GO


CREATE INDEX [IX_StorageTransactions_PONumber] ON [StorageTransactions] ([PONumber]);
GO


CREATE INDEX [IX_StorageTransactions_PurchaseOrderId] ON [StorageTransactions] ([PurchaseOrderId]);
GO


CREATE INDEX [IX_StorageTransactions_ReferenceNumber] ON [StorageTransactions] ([ReferenceNumber]);
GO


CREATE INDEX [IX_StorageTransactions_StorageLocation] ON [StorageTransactions] ([StorageLocation]);
GO


CREATE INDEX [IX_StorageTransactions_TransactionDate] ON [StorageTransactions] ([TransactionDate]);
GO


CREATE UNIQUE INDEX [IX_Transportation_PONumber_TransactionNumber] ON [Transportation] ([PONumber], [TransactionNumber]);
GO


CREATE INDEX [IX_Transportation_ProductionReferenceNumber] ON [Transportation] ([ProductionReferenceNumber]);
GO


CREATE INDEX [IX_Transportation_PurchaseOrderId] ON [Transportation] ([PurchaseOrderId]);
GO


CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
GO


CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
GO


