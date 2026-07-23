using IK.Web.Database;
using IK.Web.Models;

namespace IK.Web.Services;

public sealed class AuditLogService(HumanResourcesDbContext dbContext)
{
    public Task AppendAsync(
        AuditActionType actionType,
        string entityName,
        string entityId,
        string actorUserId,
        string? details,
        CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActionType = actionType,
            EntityName = entityName,
            EntityId = entityId,
            UserId = actorUserId,
            Details = details,
            ActionDate = DateTimeOffset.UtcNow
        });

        return Task.CompletedTask;
    }
}
