using System.Security.Claims;
using IK.Web.Models;

namespace IK.Web.Services;

public static class LeaveApprovalFilterOptionQuery
{
    public static IQueryable<string> VisibleRequesterNames(
        this IQueryable<LeaveApproval> query,
        ClaimsPrincipal? user)
    {
        return query
            .VisibleTo(user)
            .Select(approval => approval.Request.Employee.FirstName + " " + approval.Request.Employee.LastName)
            .Distinct()
            .OrderBy(name => name);
    }

    public static IQueryable<string> VisibleApproverNames(
        this IQueryable<LeaveApproval> query,
        ClaimsPrincipal? user)
    {
        return query
            .VisibleTo(user)
            .Where(approval => approval.ApproverEmployee != null)
            .Select(approval => approval.ApproverEmployee!.FirstName + " " + approval.ApproverEmployee.LastName)
            .Distinct()
            .OrderBy(name => name);
    }

    public static IQueryable<string> VisibleLeaveTypeNames(
        this IQueryable<LeaveApproval> query,
        ClaimsPrincipal? user)
    {
        return query
            .VisibleTo(user)
            .Select(approval =>
                approval.Request.Category == LeaveRequestCategory.AnnualLeave
                    ? "Yıllık İzin"
                    : approval.Request.LeaveType!.Name)
            .Distinct()
            .OrderBy(name => name);
    }
}
