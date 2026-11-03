namespace Procurement.Models.Entities;

public sealed class AuditLog
{
    public long AuditLogId { get; set; }
    public int UserId { get; set; }
    public string Action { get; set; } = "";
    public string EntityName { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string Status { get; set; } = "SUCCESS";
    public DateTime CreatedAt { get; set; }
}
