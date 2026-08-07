using IK.Web.Database;
using IK.Web.Models;

namespace IK.Web.Services;

public sealed class AuditLogService(HumanResourcesDbContext dbContext)
{
    public Task AppendAsync(
        AuditActionType actionType,
        string entityName,
        string entityId,
        int actorEmployeeId,
        string? details,
        CancellationToken cancellationToken = default)
    {
        if (actorEmployeeId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actorEmployeeId),
                "Denetim kaydı için geçerli bir çalışan kimliği zorunludur.");
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            ActionType = actionType,
            EntityName = entityName,
            EntityId = entityId,
            ActorEmployeeId = actorEmployeeId,
            Details = details,
            ActionDate = DateTimeOffset.UtcNow
        });

        return Task.CompletedTask;
    }

    public Task AppendSystemAsync(
        AuditActionType actionType,
        string entityName,
        string entityId,
        string systemActorKey,
        string? details,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(systemActorKey)
            ? throw new ArgumentException(
                "Denetim kaydı için sistem aktörü anahtarı zorunludur.",
                nameof(systemActorKey))
            : systemActorKey.Trim();

        dbContext.AuditLogs.Add(new AuditLog
        {
            ActionType = actionType,
            EntityName = entityName,
            EntityId = entityId,
            SystemActorKey = normalizedKey,
            Details = details,
            ActionDate = DateTimeOffset.UtcNow
        });

        return Task.CompletedTask;
    }
}
