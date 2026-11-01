namespace Warehouse.Models.Lookups;

public sealed record PutawayWarehouse(int WarehouseId, string WarehouseName, string WarehouseType,
    DateTime? RentalStartDate, DateTime? RentalEndDate, string Status, string RowVersion);

public sealed record PutawayLocation(int LocationId, int WarehouseId, int? ParentLocationId,
    string LocationName, string LocationType, string? ZoneType, string? StorageType,
    string? ConditionPolicy, string? CapacityMode, decimal? LengthCm, decimal? WidthCm,
    decimal? HeightCm, decimal? MaxWeightKg, string Status, string RowVersion);

public sealed record PutawayZoneRule(int ZoneLocationId, int CategoryId, int? BrandId);

// The flat parent-linked snapshot retains inactive nodes so callers can reject invalid ancestry.
public sealed record PutawayLayout(PutawayWarehouse Warehouse, PutawayLocation[] Locations,
    PutawayZoneRule[] Rules);
