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
    KKTC_KimlikNo nvarchar(20) NOT NULL,
    DepartmentId int NOT NULL,
    ManagerId int NULL,
    Title nvarchar(120) NOT NULL,
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
    CONSTRAINT CK_AuditLogs_ActionType CHECK (ActionType BETWEEN 1 AND 10)
);
GO

CREATE INDEX IX_Departments_ParentDepartmentId ON dbo.Departments(ParentDepartmentId);
CREATE INDEX IX_Departments_RegionManagerEmployeeId ON dbo.Departments(RegionManagerEmployeeId);
CREATE INDEX IX_Employees_DepartmentId ON dbo.Employees(DepartmentId);
CREATE INDEX IX_Employees_ManagerId ON dbo.Employees(ManagerId);
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
