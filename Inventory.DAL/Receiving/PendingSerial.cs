using Microsoft.EntityFrameworkCore;

namespace Inventory.DAL.Receiving;
public sealed record PendingSerial(long SerialItemId, string SerialNumber, int ProductId, long? InboundId,
    string? ReceiptCode, int? ManufacturingMonth, int? ManufacturingYear, string ItemCondition, bool IsQuarantined, DateTime RegisteredAt);
