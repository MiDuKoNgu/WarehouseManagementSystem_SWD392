namespace Warehouse.Models.Entities;

public sealed class ZoneStorageRule
{
    public int ZoneRuleId { get; set; }
    public int ZoneLocationId { get; set; }
    // Category and Brand belong to Catalog; retain IDs without cross-service navigation.
    public int CategoryId { get; set; }
    public int? BrandId { get; set; }
    public StorageLocation ZoneLocation { get; set; } = null!;
}
