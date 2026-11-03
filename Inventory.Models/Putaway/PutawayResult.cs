namespace Inventory.Models.Putaway;
public sealed record PutawayResult(long SerialItemId, string SerialNumber, int TargetLocationId, string LocationPath,
    DateTime OccurredAt, int ActorUserId, string ActorName, string RowVersion);
