namespace IK.Web.Models;

public enum LeaveRequestCategory
{
    AnnualLeave = 1,
    SicknessLeave = 2
}

public static class LeaveRequestCategoryExtensions
{
    public static string DisplayName(this LeaveRequestCategory category) =>
        category switch
        {
            LeaveRequestCategory.AnnualLeave => "Yıllık İzin",
            LeaveRequestCategory.SicknessLeave => "Hastalık İzni",
            _ => throw new InvalidOperationException("Geçersiz izin talebi kategorisi.")
        };
}
