namespace Inventory.Models.Putaway;

public sealed record PutawaySerial(long SerialItemId, string SerialNumber, int ProductId, string StockState,
    string ItemCondition, bool IsQuarantined, int CurrentWarehouseId, int? CurrentLocationId,
    string? CurrentLocationPath, string RowVersion, ProductStorageProfile Product, string WarehouseName,
    long? OriginalInboundId, string? ReceiptCode, DateTime? ReceivedAt, bool HasActiveClaim = false,
    bool HasBlockingLock = false, string? InboundStatus = null, string? OriginType = null,
    bool ClaimChecksAvailable = false, bool StocktakeChecksAvailable = false);
