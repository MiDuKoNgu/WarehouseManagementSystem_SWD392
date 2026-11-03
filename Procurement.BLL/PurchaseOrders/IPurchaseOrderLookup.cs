using Procurement.Models.PurchaseOrders;

namespace Procurement.BLL.PurchaseOrders;

public interface IPurchaseOrderLookup
{
    Task<PurchaseOrderOptions> GetOptionsAsync(CancellationToken cancellationToken);
    Task<string?> GetCreatorNameAsync(int userId, CancellationToken cancellationToken);
    Task<ReceivingSummary> GetReceivingSummaryAsync(long purchaseOrderId, CancellationToken cancellationToken);
}
