using Inventory.DAL;
using Inventory.DAL.Inbound;
using Inventory.DAL.Receiving;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Inbound.Tests;

internal static class LiveReadTests
{
    public static async Task Run()
    {
        var server = Environment.GetEnvironmentVariable("EWMS_DATA_READ_TEST_SERVER");
        if (string.IsNullOrWhiteSpace(server)) return;
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = server, InitialCatalog = "InventoryDB", IntegratedSecurity = true,
            TrustServerCertificate = true, ConnectTimeout = 5
        }.ConnectionString;
        var factory = new InventoryContextFactory(connection);
        await using var db = factory.CreateDbContext();
        var warehouses = await db.Inbounds.AsNoTracking().Where(x => x.WarehouseId != null)
            .Select(x => x.WarehouseId!.Value).Distinct().ToArrayAsync();
        var repository = new SqlInboundRepository(factory, InboundTests.Policy);
        var queries = new ReceiptQueries(factory);
        var summaries = new ReceivingSummaryRepository(factory);
        var count = 0;
        foreach (var warehouse in warehouses)
        {
            var actor = InboundTests.Actor with { WarehouseId = warehouse };
            var receipts = await repository.ListAsync(actor, default);
            count += receipts.Length;
            foreach (var receipt in receipts.Take(5))
            {
                InboundTests.Check(await repository.FindAsync(receipt.InboundId, actor, default) is not null, "live receipt detail");
                await repository.ItemsAsync(receipt.InboundId, default);
                await summaries.GetAsync(receipt.PurchaseOrderId, default);
            }
            await queries.PendingAsync(warehouse, default);
        }
        Console.WriteLine($"PASS live InventoryDB read-only: {warehouses.Length} warehouses, {count} receipts, sampled details/items/receiving summaries/pending putaway. No data changed.");
    }
}
