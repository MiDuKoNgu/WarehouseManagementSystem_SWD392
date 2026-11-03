namespace Inventory.Models.Inbound;
public sealed record SaveReceiptItem(long PoLineId, string Serial, int ManufacturingMonth, int ManufacturingYear,
    string ReceivedCondition, string? ConditionNote, string InputMethod, string RowVersion);
