using Microsoft.Data.SqlClient;
using Procurement.DAL.PurchaseOrders;

namespace Procurement.Tests;

internal static class LiveReadTests
{
    public static async Task Run()
    {
        var server = Environment.GetEnvironmentVariable("EWMS_DATA_READ_TEST_SERVER");
        if (string.IsNullOrWhiteSpace(server)) return;
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = server, InitialCatalog = "ProcurementDB", IntegratedSecurity = true,
            TrustServerCertificate = true, ConnectTimeout = 5
        }.ConnectionString;
        var repository = new PurchaseOrderRepository(connection);
        var orders = await repository.ListAsync(default);
        foreach (var order in orders.Take(10))
            ProcurementTests.Check(await repository.FindAsync(order.PurchaseOrderId, default) is not null, "live PO detail");
        Console.WriteLine($"PASS live ProcurementDB read-only: {orders.Length} orders with lines, up to 10 details. No data changed.");
    }
}
