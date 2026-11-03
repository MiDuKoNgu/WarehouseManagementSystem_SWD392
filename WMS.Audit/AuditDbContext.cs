using Microsoft.EntityFrameworkCore;

namespace WMS.Audit;

// The audit reader owns no schema and writes nothing. SQL projections retain JSON extraction
// and source-specific document joins without coupling service DbContexts to one another.
internal sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options);

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
