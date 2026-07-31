using System.ComponentModel.DataAnnotations;

namespace IK.Web.Services;

public sealed class AnnualLeaveEntitlementWorkerOptions
{
    public const string SectionName = "AnnualLeaveEntitlementWorker";

    public bool Enabled { get; set; } = true;

    [Range(1, 1440)]
    public int RetryDelayMinutes { get; set; } = 30;
}
