namespace Warehouse.Models.Lookups;

public sealed record PutawayLocation(int LocationId, int WarehouseId, int? ParentLocationId,
    string LocationName, string LocationType, string? ZoneType, string? StorageType,
    string? ConditionPolicy, string? CapacityMode, decimal? LengthCm, decimal? WidthCm,
    decimal? HeightCm, decimal? MaxWeightKg, string Status, string RowVersion);
