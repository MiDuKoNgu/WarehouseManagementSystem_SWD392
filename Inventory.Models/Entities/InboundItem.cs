namespace Inventory.Models.Entities;

public sealed class InboundItem
{
    public string InputMethod { get; set; } = "UNKNOWN";
    public int? ScannedByUserId { get; set; }
    public string? ScannedByName { get; set; }
    public Inbound Inbound { get; set; } = null!;
    public SerialItem? SerialItem { get; set; }
    public long InboundItemId { get; set; }
    public long InboundId { get; set; }
    public long POLineId { get; set; }
    public string ScannedSerial { get; set; } = "";
    public long? SerialItemId { get; set; }
    public byte? ManufacturingMonth { get; set; }
    public short? ManufacturingYear { get; set; }
    public string? ReceivedCondition { get; set; }
    public string? ConditionNote { get; set; }
    public DateTime ScannedAt { get; set; }
    public bool IsActive { get; set; }
}
