using System.Data;
using Microsoft.Data.SqlClient;
using Warehouse.Models.Lookups;

namespace Warehouse.DAL.Repositories;

public sealed class PutawayLayoutRepository(string connectionString)
{
    public async Task<PutawayLayout?> GetAsync(int warehouseId, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        // A snapshot only: Inventory revalidates these rows in its committing transaction.
        await using var command = new SqlCommand("""
            SELECT WarehouseID,WarehouseName,WarehouseType,RentalStartDate,RentalEndDate,Status,RowVersion
            FROM dbo.Warehouse WHERE WarehouseID=@warehouseId;
            SELECT LocationID,WarehouseID,ParentLocationID,LocationName,LocationType,ZoneType,StorageType,
                ConditionPolicy,CapacityMode,LengthCm,WidthCm,HeightCm,MaxWeightKg,Status,RowVersion
            FROM dbo.StorageLocation WHERE WarehouseID=@warehouseId ORDER BY LocationID;
            SELECT r.ZoneLocationID,r.CategoryID,r.BrandID
            FROM dbo.ZoneStorageRule r
            JOIN dbo.StorageLocation z ON z.LocationID=r.ZoneLocationID
            WHERE z.WarehouseID=@warehouseId ORDER BY r.ZoneLocationID,r.CategoryID,r.BrandID;
            """, connection);
        command.Parameters.Add("@warehouseId", SqlDbType.Int).Value = warehouseId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var warehouse = new PutawayWarehouse(reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetDateTime(3), reader.IsDBNull(4) ? null : reader.GetDateTime(4),
            reader.GetString(5), Convert.ToBase64String((byte[])reader[6]));
        await reader.NextResultAsync(ct);
        var locations = new List<PutawayLocation>();
        while (await reader.ReadAsync(ct))
            locations.Add(new(reader.GetInt32(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.GetString(3), reader.GetString(4), Text(reader, 5), Text(reader, 6), Text(reader, 7), Text(reader, 8),
                Number(reader, 9), Number(reader, 10), Number(reader, 11), Number(reader, 12), reader.GetString(13),
                Convert.ToBase64String((byte[])reader[14])));
        await reader.NextResultAsync(ct);
        var rules = new List<PutawayZoneRule>();
        while (await reader.ReadAsync(ct))
            rules.Add(new(reader.GetInt32(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetInt32(2)));
        return new(warehouse, locations.ToArray(), rules.ToArray());
    }

    private static string? Text(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static decimal? Number(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
}
