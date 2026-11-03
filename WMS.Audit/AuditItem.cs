using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace WMS.Audit;

public sealed record AuditItem(string Source, long AuditLogId, int UserId, string? ActorName,
    string BusinessGroup, string Action, string EntityName, string EntityId, string? DocumentCode,
    string Status, DateTimeOffset CreatedAt);
