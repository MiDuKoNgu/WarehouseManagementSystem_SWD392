namespace Inventory.Models.Inbound;
public sealed record ReceiptItem(long InboundItemId, long PoLineId, string ScannedSerial, int? ManufacturingMonth,
    int? ManufacturingYear, string? ReceivedCondition, string? ConditionNote, string InputMethod, int? ScannedByUserId,
    string? ScannedByName, DateTime ScannedAt, long? SerialItemId);
