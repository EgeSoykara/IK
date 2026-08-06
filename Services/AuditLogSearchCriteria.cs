using IK.Web.Models;

namespace IK.Web.Services;

public sealed record AuditLogSearchCriteria(
    string? UserId,
    AuditActionType? ActionType,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Text)
{
    public bool HasAny =>
        !string.IsNullOrWhiteSpace(UserId)
        || ActionType.HasValue
        || StartDate.HasValue
        || EndDate.HasValue
        || !string.IsNullOrWhiteSpace(Text);

    public AuditLogSearchCriteria Normalize() =>
        this with
        {
            UserId = NormalizeText(UserId),
            Text = NormalizeText(Text)
        };

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
