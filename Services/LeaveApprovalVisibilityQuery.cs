using System.Security.Claims;
using IK.Web.Models;

namespace IK.Web.Services;

public static class LeaveApprovalVisibilityQuery
{
    public static IQueryable<LeaveApproval> VisibleTo(
        this IQueryable<LeaveApproval> query,
        ClaimsPrincipal? user)
    {
        if (user?.IsInRole("HumanResources") == true)
        {
            return query.Where(approval => approval.ApproverRole == LeaveApproverRole.HumanResources);
        }

        var employeeId = user.GetEmployeeId();
        return employeeId.HasValue
            ? query.Where(approval =>
                approval.ApproverRole == LeaveApproverRole.Manager
                && approval.Request.ManagerApproverEmployeeId == employeeId.Value)
            : query.Where(_ => false);
    }
}
