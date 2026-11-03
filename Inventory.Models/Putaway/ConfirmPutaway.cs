namespace Inventory.Models.Putaway;
public sealed record ConfirmPutaway(long SerialItemId, int TargetLocationId, string RowVersion);
