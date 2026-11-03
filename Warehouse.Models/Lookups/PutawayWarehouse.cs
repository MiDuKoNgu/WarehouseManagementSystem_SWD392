namespace Warehouse.Models.Lookups;

public sealed record PutawayWarehouse(int WarehouseId, string WarehouseName, string WarehouseType,
    DateTime? RentalStartDate, DateTime? RentalEndDate, string Status, string RowVersion);
