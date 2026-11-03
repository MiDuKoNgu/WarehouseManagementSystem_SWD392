namespace Inventory.Models.Putaway;
public sealed record PutawayLocationNode(int LocationId, int WarehouseId, int? ParentLocationId,
    string LocationName, string LocationType, string? StorageType, string? CapacityMode,
    string? ConditionPolicy, string? ZoneType, string Status, decimal? LengthCm, decimal? WidthCm,
    decimal? HeightCm, decimal? MaxWeightKg);
