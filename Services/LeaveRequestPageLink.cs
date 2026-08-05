namespace IK.Web.Services;

public static class LeaveRequestPageLink
{
    public static string ForEmployee(int employeeId, bool create = false)
    {
        if (employeeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(employeeId));
        }

        return create
            ? $"/LeaveRequests?employeeId={employeeId}&create=true"
            : $"/LeaveRequests?employeeId={employeeId}";
    }
}
