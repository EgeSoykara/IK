namespace IK.Web.Models;

public enum AuditActionType
{
    Login = 1,
    EmployeeCreated = 2,
    EmployeeUpdated = 3,
    LeaveBalanceRenewed = 4,
    LeaveCarryOverUpdated = 5,
    LeaveRequestCreated = 6,
    LeaveRequestManagerApproved = 7,
    LeaveRequestManagerRejected = 8,
    LeaveRequestHumanResourcesApproved = 9,
    LeaveRequestHumanResourcesRejected = 10,
    EmployeeDeleted = 11,
    DepartmentCreated = 12,
    DepartmentUpdated = 13,
    DepartmentDeleted = 14,
    LeaveTypeCreated = 15,
    LeaveTypeUpdated = 16,
    LeaveTypeDeleted = 17,
    LeaveRequestUpdated = 18,
    LeaveRequestDeleted = 19,
    LeaveBalanceUpdated = 20,
    LeaveBalanceDeleted = 21,
    LeaveApprovalDeleted = 22,
}
