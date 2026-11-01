namespace Inventory.Models.Putaway;

public sealed record PutawayActor(int UserId, string Name, int WarehouseId);
public sealed record ConfirmPutaway(long SerialItemId, int TargetLocationId, string RowVersion);
public sealed record PutawayResult(long SerialItemId, string SerialNumber, int TargetLocationId, string LocationPath,
    DateTime OccurredAt, int ActorUserId, string ActorName, string RowVersion);

public sealed record ProductStorageProfile(int ProductId, string ProductCode, string ProductName,
    int CategoryId, string CategoryName, int BrandId, string BrandName, string Status,
    decimal? LengthCm, decimal? WidthCm, decimal? HeightCm, decimal? GrossWeightKg);

public sealed record PutawaySerial(long SerialItemId, string SerialNumber, int ProductId, string StockState,
    string ItemCondition, bool IsQuarantined, int CurrentWarehouseId, int? CurrentLocationId,
    string? CurrentLocationPath, string RowVersion, ProductStorageProfile Product, string WarehouseName,
    long? OriginalInboundId, string? ReceiptCode, DateTime? ReceivedAt, bool HasActiveClaim = false,
    bool HasBlockingLock = false, string? InboundStatus = null, string? OriginType = null,
    bool ClaimChecksAvailable = false, bool StocktakeChecksAvailable = false);

public sealed record PutawayPendingPage(PutawaySerial[] Items, int TotalCount, int Page, int PageSize);
public sealed record PutawayLocationNode(int LocationId, int WarehouseId, int? ParentLocationId,
    string LocationName, string LocationType, string? StorageType, string? CapacityMode,
    string? ConditionPolicy, string? ZoneType, string Status, decimal? LengthCm, decimal? WidthCm,
    decimal? HeightCm, decimal? MaxWeightKg);
public sealed record PutawayZoneRule(int ZoneLocationId, int CategoryId, int? BrandId);
public sealed record PutawayWarehouseSnapshot(int WarehouseId, string WarehouseName, string Status,
    PutawayLocationNode[] Locations, PutawayZoneRule[] Rules, string WarehouseType = "OWNED",
    DateTime? RentalEndDate = null);
public sealed record PutawayOccupant(long SerialItemId, int LocationId, int ProductId,
    string ItemCondition, ProductStorageProfile Product);

public sealed record PutawayCandidate(int LocationId, string LocationPath, string LocationType,
    string ZoneType, bool IsReserve, string CapacityMode, decimal? LengthCm, decimal? WidthCm,
    decimal? HeightCm, decimal? TotalAreaM2, decimal? UsedAreaM2, decimal? AvailableAreaM2,
    decimal? TotalVolumeM3, decimal? UsedVolumeM3, decimal? AvailableVolumeM3,
    decimal? MaxWeightKg, decimal UsedWeightKg, decimal? RemainingWeightKg,
    int OccupantCount, int SuggestionRank, bool IsValid, string? Reason);
public sealed record PutawaySuggestions(PutawaySerial Serial, PutawayCandidate[] Candidates, string? Reason = null);
