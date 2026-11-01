namespace Procurement.Models.PurchaseOrders;

public sealed record SupplierOption(int SupplierId, string SupplierName);
public sealed record WarehouseOption(int WarehouseId, string WarehouseName, string Address);
public sealed record ProductOption(int ProductId, string ProductCode, string ProductName, string Unit, int[] SupplierIds);
public sealed record PurchaseOrderOptions(SupplierOption[] Suppliers, WarehouseOption[] Warehouses, ProductOption[] Products);
public sealed record CatalogOptions(SupplierOption[] Suppliers, ProductOption[] Products);
public sealed record CreateLine(int ProductId, int Quantity);
public sealed record CreatePurchaseOrder(int SupplierId, int WarehouseId, DateOnly? ExpectedArrivalDate, string? Note, CreateLine[] Lines);
public sealed record PurchaseOrderLine(long PoLineId, int ProductId, string ProductCode, string ProductName, string Unit, int Quantity,
    int ClosedQuantity = 0, int ReceivedQuantity = 0, int RemainingQuantity = 0);
public sealed record PurchaseOrderSummary(long PurchaseOrderId, string PoCode, int SupplierId, string SupplierName,
    int WarehouseId, string WarehouseName, DateOnly OrderDate, DateOnly? ExpectedArrivalDate, string Status,
    DateTime CreatedAt, int LineCount, long TotalQuantity);
public sealed record PurchaseOrderDetail(long PurchaseOrderId, string PoCode, int SupplierId, string SupplierName,
    int WarehouseId, string WarehouseName, DateOnly OrderDate, DateOnly? ExpectedArrivalDate, string Status,
    DateTime CreatedAt, int LineCount, long TotalQuantity, int CreatedByUserId, string CreatedByName,
    string? Note, PurchaseOrderLine[] Lines, string RowVersion = "", DateTime? ConfirmedAt = null,
    DateTime? ClosedAt = null, string? CloseReason = null, bool HasAnyInbound = false, int OpenInboundCount = 0);

// Database-only records are enriched with current service lookup labels in BLL.
public sealed record StoredPurchaseOrder(long PurchaseOrderId, string PoCode, int SupplierId, int WarehouseId,
    DateOnly OrderDate, DateOnly? ExpectedArrivalDate, string Status, DateTime CreatedAt, int CreatedByUserId,
    string? Note, StoredLine[] Lines, string RowVersion = "", DateTime? ConfirmedAt = null,
    DateTime? ClosedAt = null, string? CloseReason = null);
public sealed record StoredLine(long PoLineId, int ProductId, int Quantity, int ClosedQuantity = 0);
public sealed record UpdatePurchaseOrder(int SupplierId, int WarehouseId, DateOnly? ExpectedArrivalDate, string? Note, CreateLine[] Lines, string RowVersion)
{
    public CreatePurchaseOrder ToCreate() => new(SupplierId, WarehouseId, ExpectedArrivalDate, Note, Lines);
}
public sealed record ConfirmPurchaseOrder(string RowVersion);
public sealed record FinishPurchaseOrder(string RowVersion, string Reason);
public sealed record ReceivedLine(long PoLineId, int ReceivedQuantity);
public sealed record ReceivingSummary(long PurchaseOrderId, bool HasAnyInbound, int OpenInboundCount, ReceivedLine[] Lines, bool HasInvalidReceipts = false);

public sealed class ImportRow
{
    public int RowNumber { get; init; }
    public int? SupplierId { get; init; }
    public int? WarehouseId { get; init; }
    public DateOnly? ExpectedArrivalDate { get; init; }
    public string? Note { get; init; }
    public string ProductCode { get; init; } = "";
    public string? ProductName { get; set; }
    public int? Quantity { get; init; }
    public List<string> Errors { get; init; } = [];
}
public sealed record ParsedWorkbook(ImportRow[] Rows, string[] Errors);
public sealed record ImportPreview(string? Token, bool Valid, ImportRow[] Rows, string[] Errors);
public sealed record ConfirmImport(string Token);

public interface IPurchaseOrderWorkbook
{
    byte[] CreateTemplate(PurchaseOrderOptions options);
    ParsedWorkbook Parse(Stream stream);
}
