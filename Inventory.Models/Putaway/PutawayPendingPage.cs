namespace Inventory.Models.Putaway;

public sealed record PutawayPendingPage(PutawaySerial[] Items, int TotalCount, int Page, int PageSize);
