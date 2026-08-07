using IK.Web.Models;

namespace IK.Web.Services;

public sealed record AuditLogSearchCriteria(
    int? ActorEmployeeId,
    AuditActionType? ActionType,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Text)
{
    public bool HasAny =>
        ActorEmployeeId.HasValue
        || ActionType.HasValue
        || StartDate.HasValue
        || EndDate.HasValue
        || !string.IsNullOrWhiteSpace(Text);

    public AuditLogSearchCriteria Normalize() =>
        this with
        {
            Text = NormalizeText(Text)
        };

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
