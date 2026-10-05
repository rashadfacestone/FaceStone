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
