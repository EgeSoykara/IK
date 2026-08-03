using System.ComponentModel.DataAnnotations;

namespace IK.Web.Services;

public sealed class DailyLeaveEntitlementWorkerOptions
{
    public const string SectionName = "DailyLeaveEntitlementWorker";

    public bool Enabled { get; set; } = true;

    [Range(1, 1440)]
    public int RetryDelayMinutes { get; set; } = 30;
}
