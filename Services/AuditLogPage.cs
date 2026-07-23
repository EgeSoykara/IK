using IK.Web.Models;

namespace IK.Web.Services;

public sealed record AuditLogPage(IReadOnlyList<AuditLog> Items, int TotalItems);
