namespace Inventory.Models.Putaway;
public sealed record PutawayWarehouseSnapshot(int WarehouseId, string WarehouseName, string Status,
    PutawayLocationNode[] Locations, PutawayZoneRule[] Rules, string WarehouseType = "OWNED",
    DateTime? RentalEndDate = null);
