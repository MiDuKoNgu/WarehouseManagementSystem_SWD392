namespace Procurement.Models.PurchaseOrders;

public interface IPurchaseOrderWorkbook
{
    byte[] CreateTemplate(PurchaseOrderOptions options);
    ParsedWorkbook Parse(Stream stream);
}
