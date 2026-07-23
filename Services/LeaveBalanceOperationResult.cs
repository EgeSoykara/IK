using IK.Web.Models;

namespace IK.Web.Services;

public sealed record LeaveBalanceOperationResult(
    bool Succeeded,
    bool RequiresConfirmation,
    decimal ProjectedTotalDays,
    decimal WarningLimitDays,
    string Message,
    LeaveBalance? Balance)
{
    public static LeaveBalanceOperationResult ConfirmationRequired(decimal projectedTotalDays, decimal warningLimitDays)
    {
        return new LeaveBalanceOperationResult(
            false,
            true,
            projectedTotalDays,
            warningLimitDays,
            $"Projeksiyon izin bakiyesi {projectedTotalDays:0.##} gündür ve {warningLimitDays:0.##} günlük uyarı sınırını aşmaktadır. Onay gereklidir.",
            null);
    }

    public static LeaveBalanceOperationResult Completed(decimal projectedTotalDays, decimal warningLimitDays, LeaveBalance balance)
    {
        return new LeaveBalanceOperationResult(
            true,
            false,
            projectedTotalDays,
            warningLimitDays,
            "İzin bakiyesi güncellendi.",
            balance);
    }
}
