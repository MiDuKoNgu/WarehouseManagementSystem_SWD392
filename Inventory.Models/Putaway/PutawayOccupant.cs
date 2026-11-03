namespace Inventory.Models.Putaway;
public sealed record PutawayOccupant(long SerialItemId, int LocationId, int ProductId,
    string ItemCondition, ProductStorageProfile Product);
