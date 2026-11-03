namespace Inventory.Models.Putaway;

public sealed record PutawayCandidate(int LocationId, string LocationPath, string LocationType,
    string ZoneType, bool IsReserve, string CapacityMode, decimal? LengthCm, decimal? WidthCm,
    decimal? HeightCm, decimal? TotalAreaM2, decimal? UsedAreaM2, decimal? AvailableAreaM2,
    decimal? TotalVolumeM3, decimal? UsedVolumeM3, decimal? AvailableVolumeM3,
    decimal? MaxWeightKg, decimal UsedWeightKg, decimal? RemainingWeightKg,
    int OccupantCount, int SuggestionRank, bool IsValid, string? Reason);
