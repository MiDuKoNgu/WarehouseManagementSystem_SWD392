using Inventory.Models.Putaway;

namespace Inventory.BLL.Putaway;

public sealed class PutawayService(IPutawayRepository repository)
{
    public Task<PutawayPendingPage> PendingAsync(PutawayActor actor, string? search, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || page > 1000000 || pageSize is < 1 or > 100 || search?.Length > 100)
            throw new PutawayException(400, "INVALID_QUERY", "Tìm kiếm tối đa 100 ký tự, trang >= 1 và số dòng 1..100.");
        return repository.PendingAsync(actor, search?.Trim(), page, pageSize, ct);
    }
    public Task<PutawaySerial> FindAsync(PutawayActor actor, long? id, string? serial, CancellationToken ct)
    {
        serial = serial?.Trim();
        if (id is <= 0 || id is null && (string.IsNullOrEmpty(serial) || serial.Length > 100 || serial.Any(char.IsControl)))
            throw new PutawayException(400, "INVALID_SERIAL", "Serial không hợp lệ.");
        return repository.FindAsync(actor, id, serial, ct);
    }
    public Task<PutawaySuggestions> SuggestionsAsync(PutawayActor actor, long id, CancellationToken ct) => repository.SuggestionsAsync(actor, id, ct);
    public Task<PutawayResult> ConfirmAsync(PutawayActor actor, ConfirmPutaway request, CancellationToken ct)
    {
        if (request.SerialItemId <= 0 || request.TargetLocationId <= 0)
            throw new PutawayException(400, "INVALID_TARGET", "Serial và vị trí đích phải hợp lệ.");
        _ = PutawayRules.Version(request.RowVersion);
        return repository.ConfirmAsync(actor, request, ct);
    }
}
