using Microsoft.EntityFrameworkCore;

namespace WMS.Audit;

// The audit reader owns no schema and writes nothing. SQL projections retain JSON extraction
// and source-specific document joins without coupling service DbContexts to one another.
internal sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options);
