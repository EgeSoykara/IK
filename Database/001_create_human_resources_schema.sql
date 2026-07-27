SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE dbo.Departments
(
    DepartmentId int IDENTITY(1,1) NOT NULL,
    DepartmentName nvarchar(120) NOT NULL,
    ParentDepartmentId int NULL,
    RegionManagerEmployeeId int NULL,
    CONSTRAINT PK_Departments PRIMARY KEY CLUSTERED (DepartmentId),
    CONSTRAINT UQ_Departments_DepartmentName UNIQUE (DepartmentName),
    CONSTRAINT FK_Departments_Departments_ParentDepartmentId
        FOREIGN KEY (ParentDepartmentId) REFERENCES dbo.Departments(DepartmentId)
);
GO

CREATE TABLE dbo.Employees
(
    EmployeeId int IDENTITY(1,1) NOT NULL,
    SicilNo nvarchar(30) NOT NULL,
    FirstName nvarchar(80) NOT NULL,
    LastName nvarchar(80) NOT NULL,
    KKTC_KimlikNo nvarchar(10) NOT NULL,
    DepartmentId int NOT NULL,
    ManagerId int NULL,
    StartDate datetime2 NULL,
    Status int NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    UpdatedAt datetimeoffset NOT NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_Employees PRIMARY KEY CLUSTERED (EmployeeId),
    CONSTRAINT UQ_Employees_SicilNo UNIQUE (SicilNo),
    CONSTRAINT UQ_Employees_KKTC_KimlikNo UNIQUE (KKTC_KimlikNo),
    CONSTRAINT FK_Employees_Departments_DepartmentId
        FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments(DepartmentId),
    CONSTRAINT FK_Employees_Employees_ManagerId
        FOREIGN KEY (ManagerId) REFERENCES dbo.Employees(EmployeeId),
    CONSTRAINT CK_Employees_Status CHECK (Status IN (1, 2))
);
GO

ALTER TABLE dbo.Departments
ADD CONSTRAINT FK_Departments_Employees_RegionManagerEmployeeId
    FOREIGN KEY (RegionManagerEmployeeId) REFERENCES dbo.Employees(EmployeeId)
    ON DELETE SET NULL;
GO

CREATE TABLE dbo.EmployeeBankAccounts
(
    EmployeeBankAccountId int IDENTITY(1,1) NOT NULL,
    EmployeeId int NOT NULL,
    BankName nvarchar(120) NOT NULL,
    BranchName nvarchar(120) NULL,
    BranchCode nvarchar(30) NULL,
    AccountNumber nvarchar(50) NULL,
    Iban nvarchar(34) NOT NULL,
    IsPrimary bit NOT NULL,
    CONSTRAINT PK_EmployeeBankAccounts PRIMARY KEY CLUSTERED (EmployeeBankAccountId),
    CONSTRAINT FK_EmployeeBankAccounts_Employees_EmployeeId
        FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId)
);
GO

CREATE INDEX IX_EmployeeBankAccounts_EmployeeId
    ON dbo.EmployeeBankAccounts(EmployeeId);
CREATE UNIQUE INDEX UX_EmployeeBankAccounts_EmployeeId_Primary
    ON dbo.EmployeeBankAccounts(EmployeeId, IsPrimary)
    WHERE IsPrimary = 1;
CREATE UNIQUE INDEX IX_EmployeeBankAccounts_Iban
    ON dbo.EmployeeBankAccounts(Iban);
GO

CREATE TABLE dbo.EmployeeIdentityDocuments
(
    EmployeeIdentityDocumentId int IDENTITY(1,1) NOT NULL,
    EmployeeId int NOT NULL,
    DocumentType nvarchar(80) NOT NULL,
    DocumentNumber nvarchar(80) NOT NULL,
    IssuingAuthority nvarchar(120) NULL,
    IssueDate date NULL,
    ExpiryDate date NULL,
    Description nvarchar(500) NULL,
    CONSTRAINT PK_EmployeeIdentityDocuments PRIMARY KEY CLUSTERED (EmployeeIdentityDocumentId),
    CONSTRAINT AK_EmployeeIdentityDocuments_EmployeeId_RecordId
        UNIQUE NONCLUSTERED (EmployeeId, EmployeeIdentityDocumentId),
    CONSTRAINT FK_EmployeeIdentityDocuments_Employees_EmployeeId
        FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId)
);
GO

CREATE INDEX IX_EmployeeIdentityDocuments_EmployeeId
    ON dbo.EmployeeIdentityDocuments(EmployeeId);
CREATE UNIQUE INDEX IX_EmployeeIdentityDocuments_EmployeeId_DocumentType_DocumentNumber
    ON dbo.EmployeeIdentityDocuments(EmployeeId, DocumentType, DocumentNumber);
GO

CREATE TABLE dbo.EmployeePhones
(
    EmployeePhoneId int IDENTITY(1,1) NOT NULL,
    EmployeeId int NOT NULL,
    PhoneType nvarchar(40) NOT NULL,
    PhoneNumber nvarchar(30) NOT NULL,
    Extension nvarchar(10) NULL,
    IsPrimary bit NOT NULL,
    CONSTRAINT PK_EmployeePhones PRIMARY KEY CLUSTERED (EmployeePhoneId),
    CONSTRAINT FK_EmployeePhones_Employees_EmployeeId
        FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId)
);
GO

CREATE INDEX IX_EmployeePhones_EmployeeId
    ON dbo.EmployeePhones(EmployeeId);
CREATE UNIQUE INDEX UX_EmployeePhones_EmployeeId_Primary
    ON dbo.EmployeePhones(EmployeeId, IsPrimary)
    WHERE IsPrimary = 1;
GO

CREATE TABLE dbo.EmployeeAddresses
(
    EmployeeAddressId int IDENTITY(1,1) NOT NULL,
    EmployeeId int NOT NULL,
    AddressType nvarchar(40) NOT NULL,
    AddressLine nvarchar(300) NOT NULL,
    District nvarchar(100) NULL,
    City nvarchar(100) NULL,
    Country nvarchar(100) NULL,
    PostalCode nvarchar(20) NULL,
    IsPrimary bit NOT NULL,
    CONSTRAINT PK_EmployeeAddresses PRIMARY KEY CLUSTERED (EmployeeAddressId),
    CONSTRAINT FK_EmployeeAddresses_Employees_EmployeeId
        FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId)
);
GO

CREATE INDEX IX_EmployeeAddresses_EmployeeId
    ON dbo.EmployeeAddresses(EmployeeId);
CREATE UNIQUE INDEX UX_EmployeeAddresses_EmployeeId_Primary
    ON dbo.EmployeeAddresses(EmployeeId, IsPrimary)
    WHERE IsPrimary = 1;
GO

CREATE TABLE dbo.EmployeeEducations
(
    EmployeeEducationId int IDENTITY(1,1) NOT NULL,
    EmployeeId int NOT NULL,
    InstitutionName nvarchar(200) NOT NULL,
    DepartmentName nvarchar(160) NULL,
    Degree nvarchar(120) NULL,
    EducationLevel nvarchar(80) NULL,
    StartDate date NULL,
    GraduationDate date NULL,
    IsGraduated bit NOT NULL,
    CONSTRAINT PK_EmployeeEducations PRIMARY KEY CLUSTERED (EmployeeEducationId),
    CONSTRAINT AK_EmployeeEducations_EmployeeId_RecordId
        UNIQUE NONCLUSTERED (EmployeeId, EmployeeEducationId),
    CONSTRAINT FK_EmployeeEducations_Employees_EmployeeId
        FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId)
);
GO

CREATE INDEX IX_EmployeeEducations_EmployeeId
    ON dbo.EmployeeEducations(EmployeeId);
GO

CREATE TABLE dbo.EmployeeCourseCertificates
(
    EmployeeCourseCertificateId int IDENTITY(1,1) NOT NULL,
    EmployeeId int NOT NULL,
    Name nvarchar(200) NOT NULL,
    IssuingOrganization nvarchar(160) NULL,
    StartDate date NULL,
    EndDate date NULL,
    CertificateNumber nvarchar(100) NULL,
    ExpiryDate date NULL,
    CONSTRAINT PK_EmployeeCourseCertificates PRIMARY KEY CLUSTERED (EmployeeCourseCertificateId),
    CONSTRAINT AK_EmployeeCourseCertificates_EmployeeId_RecordId
        UNIQUE NONCLUSTERED (EmployeeId, EmployeeCourseCertificateId),
    CONSTRAINT FK_EmployeeCourseCertificates_Employees_EmployeeId
        FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId)
);
GO

CREATE INDEX IX_EmployeeCourseCertificates_EmployeeId
    ON dbo.EmployeeCourseCertificates(EmployeeId);
GO

CREATE TABLE dbo.EmployeeTerminations
(
    EmployeeTerminationId int IDENTITY(1,1) NOT NULL,
    EmployeeId int NOT NULL,
    TerminationDate date NOT NULL,
    Reason nvarchar(160) NOT NULL,
    Description nvarchar(500) NULL,
    CONSTRAINT PK_EmployeeTerminations PRIMARY KEY CLUSTERED (EmployeeTerminationId),
    CONSTRAINT FK_EmployeeTerminations_Employees_EmployeeId
        FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId)
);
GO

CREATE UNIQUE INDEX IX_EmployeeTerminations_EmployeeId
    ON dbo.EmployeeTerminations(EmployeeId);
GO

CREATE TABLE dbo.EmployeeDocumentCategories
(
    CanonicalKey nvarchar(64) NOT NULL,
    DisplayName nvarchar(120) NOT NULL,
    SortOrder int NOT NULL,
    IsActive bit NOT NULL,
    CONSTRAINT PK_EmployeeDocumentCategories PRIMARY KEY CLUSTERED (CanonicalKey),
    CONSTRAINT CK_EmployeeDocumentCategories_CanonicalKey CHECK
    (
        CanonicalKey <> N''
        AND CanonicalKey COLLATE Latin1_General_100_BIN2 = LOWER(CanonicalKey)
        AND CanonicalKey COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^a-z0-9-]%'
        AND CanonicalKey NOT LIKE '-%'
        AND CanonicalKey NOT LIKE '%-'
        AND CanonicalKey NOT LIKE '%--%'
    )
);
GO

INSERT dbo.EmployeeDocumentCategories (CanonicalKey, DisplayName, SortOrder, IsActive)
VALUES
    (N'identity', N'Kimlik Belgeleri', 10, 1),
    (N'employment', N'İş ve Sözleşme Belgeleri', 20, 1),
    (N'education', N'Eğitim ve Sertifika Belgeleri', 30, 1),
    (N'health', N'Sağlık Belgeleri', 40, 1),
    (N'other', N'Diğer Belgeler', 50, 1);
GO

CREATE TABLE dbo.EmployeeProfilePhotos
(
    EmployeeId int NOT NULL,
    ContentType nvarchar(100) NOT NULL,
    StorageKey nvarchar(500) NOT NULL,
    SizeBytes bigint NOT NULL,
    UploadedAt datetimeoffset NOT NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_EmployeeProfilePhotos PRIMARY KEY CLUSTERED (EmployeeId),
    CONSTRAINT FK_EmployeeProfilePhotos_Employees_EmployeeId
        FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId),
    CONSTRAINT CK_EmployeeProfilePhotos_SizeBytes CHECK (SizeBytes > 0)
);
GO

CREATE TABLE dbo.EmployeeDocuments
(
    EmployeeDocumentId bigint IDENTITY(1,1) NOT NULL,
    EmployeeId int NOT NULL,
    CategoryCanonicalKey nvarchar(64) NOT NULL,
    EmployeeIdentityDocumentId int NULL,
    EmployeeEducationId int NULL,
    EmployeeCourseCertificateId int NULL,
    OriginalFileName nvarchar(255) NOT NULL,
    ContentType nvarchar(100) NOT NULL,
    StorageKey nvarchar(500) NOT NULL,
    SizeBytes bigint NOT NULL,
    UploadedAt datetimeoffset NOT NULL,
    CONSTRAINT PK_EmployeeDocuments PRIMARY KEY CLUSTERED (EmployeeDocumentId),
    CONSTRAINT FK_EmployeeDocuments_Employees_EmployeeId
        FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId),
    CONSTRAINT FK_EmployeeDocuments_EmployeeDocumentCategories_CategoryCanonicalKey
        FOREIGN KEY (CategoryCanonicalKey) REFERENCES dbo.EmployeeDocumentCategories(CanonicalKey),
    CONSTRAINT FK_EmployeeDocuments_EmployeeIdentityDocuments_EmployeeId_EmployeeIdentityDocumentId
        FOREIGN KEY (EmployeeId, EmployeeIdentityDocumentId)
        REFERENCES dbo.EmployeeIdentityDocuments(EmployeeId, EmployeeIdentityDocumentId),
    CONSTRAINT FK_EmployeeDocuments_EmployeeEducations_EmployeeId_EmployeeEducationId
        FOREIGN KEY (EmployeeId, EmployeeEducationId)
        REFERENCES dbo.EmployeeEducations(EmployeeId, EmployeeEducationId),
    CONSTRAINT FK_EmployeeDocuments_EmployeeCourseCertificates_EmployeeId_EmployeeCourseCertificateId
        FOREIGN KEY (EmployeeId, EmployeeCourseCertificateId)
        REFERENCES dbo.EmployeeCourseCertificates(EmployeeId, EmployeeCourseCertificateId),
    CONSTRAINT CK_EmployeeDocuments_SizeBytes CHECK (SizeBytes > 0),
    CONSTRAINT CK_EmployeeDocuments_SingleRelatedRecord CHECK
    (
        (CASE WHEN EmployeeIdentityDocumentId IS NULL THEN 0 ELSE 1 END)
        + (CASE WHEN EmployeeEducationId IS NULL THEN 0 ELSE 1 END)
        + (CASE WHEN EmployeeCourseCertificateId IS NULL THEN 0 ELSE 1 END)
        <= 1
    )
);
GO

CREATE TABLE dbo.LeaveTypes
(
    LeaveTypeId int IDENTITY(1,1) NOT NULL,
    Name nvarchar(80) NOT NULL,
    AnnualQuota decimal(7,2) NOT NULL,
    CarryOverRule bit NOT NULL CONSTRAINT DF_LeaveTypes_CarryOverRule DEFAULT (1),
    MaxAccrualDays decimal(7,2) NOT NULL CONSTRAINT DF_LeaveTypes_MaxAccrualDays DEFAULT (50),
    CONSTRAINT PK_LeaveTypes PRIMARY KEY CLUSTERED (LeaveTypeId),
    CONSTRAINT UQ_LeaveTypes_Name UNIQUE (Name),
    CONSTRAINT CK_LeaveTypes_AnnualQuota CHECK (AnnualQuota >= 0),
    CONSTRAINT CK_LeaveTypes_MaxAccrualDays CHECK (MaxAccrualDays > 0)
);
GO

CREATE TABLE dbo.LeaveBalances
(
    BalanceId int IDENTITY(1,1) NOT NULL,
    EmployeeId int NOT NULL,
    LeaveTypeId int NOT NULL,
    [Year] int NOT NULL,
    EntitledDays decimal(7,2) NOT NULL,
    CarryOverDays decimal(7,2) NOT NULL,
    UsedDays decimal(7,2) NOT NULL,
    RemainingDays decimal(7,2) NOT NULL,
    CarryOverLimitWarningConfirmed bit NOT NULL CONSTRAINT DF_LeaveBalances_CarryOverLimitWarningConfirmed DEFAULT (0),
    CarryOverLimitWarningConfirmedAt datetimeoffset NULL,
    CarryOverLimitWarningConfirmedBy nvarchar(100) NULL,
    CreatedAt datetimeoffset NOT NULL,
    UpdatedAt datetimeoffset NOT NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_LeaveBalances PRIMARY KEY CLUSTERED (BalanceId),
    CONSTRAINT UQ_LeaveBalances_Employee_LeaveType_Year UNIQUE (EmployeeId, LeaveTypeId, [Year]),
    CONSTRAINT FK_LeaveBalances_Employees_EmployeeId
        FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId) ON DELETE CASCADE,
    CONSTRAINT FK_LeaveBalances_LeaveTypes_LeaveTypeId
        FOREIGN KEY (LeaveTypeId) REFERENCES dbo.LeaveTypes(LeaveTypeId),
    CONSTRAINT CK_LeaveBalances_Year CHECK ([Year] >= 2000),
    CONSTRAINT CK_LeaveBalances_Days CHECK (EntitledDays >= 0 AND CarryOverDays >= 0 AND UsedDays >= 0),
    CONSTRAINT CK_LeaveBalances_RemainingDays CHECK (RemainingDays = EntitledDays + CarryOverDays - UsedDays)
);
GO

CREATE TABLE dbo.LeaveRequests
(
    RequestId int IDENTITY(1,1) NOT NULL,
    EmployeeId int NOT NULL,
    LeaveTypeId int NOT NULL,
    StartDate date NOT NULL,
    EndDate date NOT NULL,
    RequestedDays decimal(7,2) NOT NULL,
    Reason nvarchar(500) NOT NULL,
    CurrentStatus int NOT NULL,
    ManagerApproverEmployeeId int NULL,
    CreatedAt datetimeoffset NOT NULL,
    UpdatedAt datetimeoffset NOT NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_LeaveRequests PRIMARY KEY CLUSTERED (RequestId),
    CONSTRAINT FK_LeaveRequests_Employees_EmployeeId
        FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId) ON DELETE CASCADE,
    CONSTRAINT FK_LeaveRequests_LeaveTypes_LeaveTypeId
        FOREIGN KEY (LeaveTypeId) REFERENCES dbo.LeaveTypes(LeaveTypeId),
    CONSTRAINT FK_LeaveRequests_Employees_ManagerApproverEmployeeId
        FOREIGN KEY (ManagerApproverEmployeeId) REFERENCES dbo.Employees(EmployeeId),
    CONSTRAINT CK_LeaveRequests_DateRange CHECK (EndDate >= StartDate),
    CONSTRAINT CK_LeaveRequests_RequestedDays CHECK (RequestedDays > 0),
    CONSTRAINT CK_LeaveRequests_CurrentStatus CHECK (CurrentStatus IN (1, 2, 3, 4))
);
GO

CREATE TABLE dbo.LeaveApprovals
(
    ApprovalId int IDENTITY(1,1) NOT NULL,
    RequestId int NOT NULL,
    ApproverRole int NOT NULL,
    ApproverEmployeeId int NULL,
    Decision int NOT NULL,
    DecisionDate datetimeoffset NULL,
    Comment nvarchar(500) NULL,
    CreatedAt datetimeoffset NOT NULL,
    CONSTRAINT PK_LeaveApprovals PRIMARY KEY CLUSTERED (ApprovalId),
    CONSTRAINT UQ_LeaveApprovals_Request_ApproverRole UNIQUE (RequestId, ApproverRole),
    CONSTRAINT FK_LeaveApprovals_LeaveRequests_RequestId
        FOREIGN KEY (RequestId) REFERENCES dbo.LeaveRequests(RequestId) ON DELETE CASCADE,
    CONSTRAINT FK_LeaveApprovals_Employees_ApproverEmployeeId
        FOREIGN KEY (ApproverEmployeeId) REFERENCES dbo.Employees(EmployeeId),
    CONSTRAINT CK_LeaveApprovals_ApproverRole CHECK (ApproverRole IN (1, 2)),
    CONSTRAINT CK_LeaveApprovals_Decision CHECK (Decision IN (1, 2, 3)),
    CONSTRAINT CK_LeaveApprovals_RejectionComment CHECK (Decision <> 3 OR NULLIF(LTRIM(RTRIM(Comment)), '') IS NOT NULL)
);
GO

CREATE TABLE dbo.AuditLogs
(
    AuditLogId bigint IDENTITY(1,1) NOT NULL,
    UserId nvarchar(100) NOT NULL,
    ActionType int NOT NULL,
    EntityName nvarchar(100) NOT NULL,
    EntityId nvarchar(64) NOT NULL,
    ActionDate datetimeoffset NOT NULL,
    Details nvarchar(1000) NULL,
    CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (AuditLogId),
    CONSTRAINT CK_AuditLogs_ActionType CHECK (ActionType BETWEEN 1 AND 24)
);
GO

CREATE INDEX IX_Departments_ParentDepartmentId ON dbo.Departments(ParentDepartmentId);
CREATE INDEX IX_Departments_RegionManagerEmployeeId ON dbo.Departments(RegionManagerEmployeeId);
CREATE INDEX IX_Employees_DepartmentId ON dbo.Employees(DepartmentId);
CREATE INDEX IX_Employees_ManagerId ON dbo.Employees(ManagerId);
CREATE UNIQUE INDEX IX_EmployeeProfilePhotos_StorageKey ON dbo.EmployeeProfilePhotos(StorageKey);
CREATE INDEX IX_EmployeeDocuments_CategoryCanonicalKey ON dbo.EmployeeDocuments(CategoryCanonicalKey);
CREATE INDEX IX_EmployeeDocuments_EmployeeId_EmployeeIdentityDocumentId
    ON dbo.EmployeeDocuments(EmployeeId, EmployeeIdentityDocumentId);
CREATE INDEX IX_EmployeeDocuments_EmployeeId_EmployeeEducationId
    ON dbo.EmployeeDocuments(EmployeeId, EmployeeEducationId);
CREATE INDEX IX_EmployeeDocuments_EmployeeId_EmployeeCourseCertificateId
    ON dbo.EmployeeDocuments(EmployeeId, EmployeeCourseCertificateId);
CREATE INDEX IX_EmployeeDocuments_EmployeeId_CategoryCanonicalKey_UploadedAt
    ON dbo.EmployeeDocuments(EmployeeId, CategoryCanonicalKey, UploadedAt);
CREATE UNIQUE INDEX IX_EmployeeDocuments_StorageKey ON dbo.EmployeeDocuments(StorageKey);
CREATE INDEX IX_LeaveBalances_Year ON dbo.LeaveBalances([Year]);
CREATE INDEX IX_LeaveBalances_LeaveTypeId ON dbo.LeaveBalances(LeaveTypeId);
CREATE INDEX IX_LeaveRequests_Employee_Start_End ON dbo.LeaveRequests(EmployeeId, StartDate, EndDate);
CREATE INDEX IX_LeaveRequests_CurrentStatus ON dbo.LeaveRequests(CurrentStatus);
CREATE INDEX IX_LeaveRequests_LeaveTypeId ON dbo.LeaveRequests(LeaveTypeId);
CREATE INDEX IX_LeaveRequests_ManagerApproverEmployeeId ON dbo.LeaveRequests(ManagerApproverEmployeeId);
CREATE INDEX IX_LeaveApprovals_ApproverEmployeeId ON dbo.LeaveApprovals(ApproverEmployeeId);
CREATE INDEX IX_AuditLogs_ActionDate ON dbo.AuditLogs(ActionDate);
CREATE INDEX IX_AuditLogs_ActionType ON dbo.AuditLogs(ActionType);
CREATE INDEX IX_AuditLogs_Entity ON dbo.AuditLogs(EntityName, EntityId);
GO
