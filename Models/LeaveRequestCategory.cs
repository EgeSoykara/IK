namespace IK.Web.Models;

public enum LeaveRequestCategory
{
    AnnualLeave = 1,
    SpecificLeaveType = 2
}

public static class LeaveRequestCategoryExtensions
{
    public static string DisplayName(this LeaveRequestCategory category) =>
        category switch
        {
            LeaveRequestCategory.AnnualLeave => "Yıllık İzin",
            LeaveRequestCategory.SpecificLeaveType => "Seçili İzin Türü",
            _ => throw new InvalidOperationException("Geçersiz izin talebi kategorisi.")
        };

    public static string DisplayName(this LeaveRequest request) =>
        request.Category switch
        {
            LeaveRequestCategory.AnnualLeave => "Yıllık İzin",
            LeaveRequestCategory.SpecificLeaveType when request.LeaveType is not null =>
                request.LeaveType.Name,
            _ => throw new InvalidOperationException("İzin talebinin izin türü geçersiz.")
        };
}
