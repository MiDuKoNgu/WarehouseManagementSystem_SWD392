using Inventory.Models.Putaway;

namespace Inventory.BLL.Putaway;

public interface IPutawayRepository
{
    Task<PutawayPendingPage> PendingAsync(PutawayActor actor, string? search, int page, int pageSize, CancellationToken ct);
    Task<PutawaySerial> FindAsync(PutawayActor actor, long? id, string? serial, CancellationToken ct);
    Task<PutawaySuggestions> SuggestionsAsync(PutawayActor actor, long id, CancellationToken ct);
    Task<PutawayResult> ConfirmAsync(PutawayActor actor, ConfirmPutaway request, CancellationToken ct);
}
