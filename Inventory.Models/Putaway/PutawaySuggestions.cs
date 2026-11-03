namespace Inventory.Models.Putaway;
public sealed record PutawaySuggestions(PutawaySerial Serial, PutawayCandidate[] Candidates, string? Reason = null);
