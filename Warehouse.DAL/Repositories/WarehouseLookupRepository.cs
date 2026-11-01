using Microsoft.Data.SqlClient;
using Warehouse.Models.Lookups;

namespace Warehouse.DAL.Repositories;

public sealed class WarehouseLookupRepository(string connectionString)
{
    public async Task<List<WarehouseOption>> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT WarehouseID, WarehouseName, Address FROM dbo.Warehouse WHERE Status = 'ACTIVE' ORDER BY WarehouseName", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var warehouses = new List<WarehouseOption>();
        while (await reader.ReadAsync(cancellationToken)) warehouses.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        return warehouses;
    }
}
