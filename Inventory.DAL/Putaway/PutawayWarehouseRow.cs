namespace Inventory.DAL.Putaway;

internal sealed class PutawayWarehouseRow
{
    public string WarehouseName { get; set; } = "";
    public string Status { get; set; } = "";
    public string WarehouseType { get; set; } = "";
    public DateTime? RentalEndDate { get; set; }
}
