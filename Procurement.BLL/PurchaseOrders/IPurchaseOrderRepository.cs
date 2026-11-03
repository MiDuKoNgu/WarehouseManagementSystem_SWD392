using Procurement.Models.PurchaseOrders;

namespace Procurement.BLL.PurchaseOrders;

public interface IPurchaseOrderRepository
{
    Task<StoredPurchaseOrder[]> ListAsync(CancellationToken cancellationToken);
    Task<StoredPurchaseOrder?> FindAsync(long id, CancellationToken cancellationToken);
    Task<StoredPurchaseOrder> CreateAsync(CreatePurchaseOrder request, int userId, string userName,
        string action, CancellationToken cancellationToken, string? importKey = null);
    Task<StoredPurchaseOrder> UpdateAsync(StoredPurchaseOrder expected, CreatePurchaseOrder request, int userId, string userName, CancellationToken cancellationToken);
    Task<StoredPurchaseOrder> TransitionAsync(StoredPurchaseOrder expected, string targetStatus, string? reason,
        IReadOnlyDictionary<long, int> closedQuantities, int userId, string userName, CancellationToken cancellationToken);
}
