using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WMS.Audit.Source.Tests;

internal static class QueryCompilationTests
{
    public static async Task Run()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true")
            .AddInterceptors(new ConnectionProbe()).Options;
        foreach (var source in new[] { "inventory", "procurement" })
        {
            var reader = new SqlAuditReader(new ContextFactory(options), source);
            await Compiles(async () => { await reader.PageAsync(new(), default); });
            await Compiles(async () => { await reader.DetailAsync(1, default); });
            await Compiles(async () => { await reader.ActionsAsync(default); });
        }
        Console.WriteLine("PASS: both audit sources compile page, detail and action SQL before opening a database connection.");
    }

    private static async Task Compiles(Func<Task> query)
    {
        try { await query(); }
        catch (QueryCompiledException) { return; }
        throw new Exception("FAIL: expected connection probe after SQL compilation.");
    }
    private sealed class QueryCompiledException : Exception { }
    private sealed class ConnectionProbe : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default) => throw new QueryCompiledException();
    }
    private sealed class ContextFactory(DbContextOptions<AuditDbContext> options) : IDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext() => new(options);
    }
}
