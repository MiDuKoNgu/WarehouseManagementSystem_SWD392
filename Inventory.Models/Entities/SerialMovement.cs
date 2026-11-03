namespace Inventory.Models.Entities;

public sealed class SerialMovement
{
    public long MovementId { get; set; }
    public long SerialItemId { get; set; }
    public int PerformedByUserId { get; set; }
    public string EventType { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string? FromState { get; set; }
    public string ToState { get; set; } = "";
    public string? FromCondition { get; set; }
    public string ToCondition { get; set; } = "";
    public int? FromWarehouseId { get; set; }
    public int? ToWarehouseId { get; set; }
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    public bool? FromQuarantined { get; set; }
    public bool ToQuarantined { get; set; }
    public long? InboundId { get; set; }
    public long? OutboundId { get; set; }
    public long? ReturnReceiptId { get; set; }
    public long? DispatchId { get; set; }
    public long? TransferReceiptId { get; set; }
    public long? AdjustmentId { get; set; }
    public long? LossRequestId { get; set; }
    public string? Note { get; set; }
}
