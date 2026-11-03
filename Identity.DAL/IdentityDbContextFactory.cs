using Microsoft.EntityFrameworkCore;

namespace Identity.DAL;

// Used by command-line tools and existing repository callers; API uses AddDbContextFactory.
internal sealed class IdentityDbContextFactory(string connectionString) : IDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext() => IdentityDbContext.Create(connectionString);
}
