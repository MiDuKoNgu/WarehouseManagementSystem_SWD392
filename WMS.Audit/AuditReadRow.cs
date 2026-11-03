using Microsoft.EntityFrameworkCore;

namespace WMS.Audit;

internal sealed class AuditReadRow
{
    public long AuditLogID { get; set; }
    public int UserID { get; set; }
    public string? ActorName { get; set; }
    public string BusinessGroup { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityName { get; set; } = "";
    public string EntityID { get; set; } = "";
    public string? DocumentCode { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}
