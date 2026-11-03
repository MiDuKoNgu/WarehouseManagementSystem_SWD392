using System.Data.Common;
using Inventory.DAL;
using Inventory.DAL.Inbound;
using Inventory.DAL.Receiving;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Inventory.Inbound.Tests;

internal static class QueryTranslationTests
{
    public static async Task Run()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=unused;Database=TranslationOnly;Integrated Security=true;TrustServerCertificate=true")
            .AddInterceptors(new ConnectionProbe()).Options;
        var factory = new ContextFactory(options);
        var repository = new SqlInboundRepository(factory, InboundTests.Policy);
        var queries = new ReceiptQueries(factory);
        await Check(() => repository.ListAsync(InboundTests.Actor, default), "receipt list");
        await Check(() => repository.FindAsync(1, InboundTests.Actor, default), "receipt detail");
        await Check(() => repository.FindByTokenAsync(Guid.NewGuid(), InboundTests.Actor, default), "receipt retry token");
        await Check(() => repository.ItemsAsync(1, default), "receipt serials");
        await Check(() => repository.ProgressAsync(InboundTests.Order(), 1, default), "receiving progress aggregates");
        await Check(() => repository.ProgressAsync(InboundTests.Order(), null, default), "receiving progress without current receipt");
        await Check(() => queries.PendingAsync(1, default), "pending putaway");
        Console.WriteLine("PASS Inventory SQL query translation: list/detail, retry, serials, grouped progress and pending putaway. No SQL connection opened.");
    }

    private static async Task Check(Func<Task> query, string name)
    {
        try { await query(); }
        catch (TranslationCompleteException) { return; }
        throw new Exception("FAIL expected SQL connection probe: " + name);
    }

    private sealed class TranslationCompleteException : Exception { }
    private sealed class ConnectionProbe : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default) => throw new TranslationCompleteException();
    }

    private sealed class ContextFactory(DbContextOptions<InventoryDbContext> options) : IDbContextFactory<InventoryDbContext>
    {
        public InventoryDbContext CreateDbContext() => new(options);
    }
}
