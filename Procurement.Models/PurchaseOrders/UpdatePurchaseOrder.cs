namespace Procurement.Models.PurchaseOrders;
public sealed record UpdatePurchaseOrder(int SupplierId, int WarehouseId, DateOnly? ExpectedArrivalDate, string? Note, CreateLine[] Lines, string RowVersion)
{
    public CreatePurchaseOrder ToCreate() => new(SupplierId, WarehouseId, ExpectedArrivalDate, Note, Lines);
}
