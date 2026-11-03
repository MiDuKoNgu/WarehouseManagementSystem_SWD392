namespace Warehouse.Models.Entities;

public sealed class Warehouse
{
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public string WarehouseType { get; set; } = "";
    public string Address { get; set; } = "";
    public DateTime? RentalStartDate { get; set; }
    public DateTime? RentalEndDate { get; set; }
    public string Status { get; set; } = "";
    public byte[] RowVersion { get; set; } = [];
    public ICollection<StorageLocation> Locations { get; set; } = new List<StorageLocation>();
}
