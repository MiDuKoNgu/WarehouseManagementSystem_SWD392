namespace Catalog.Models.Entities;

public sealed class Category
{
    public int CategoryId { get; set; }
    public string? Description { get; set; }
    public string CategoryName { get; set; } = "";
}
