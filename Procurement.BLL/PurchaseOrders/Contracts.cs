using Procurement.Models.PurchaseOrders;

namespace Procurement.BLL.PurchaseOrders;

public interface IPurchaseOrderLookup
{
    Task<PurchaseOrderOptions> GetOptionsAsync(CancellationToken cancellationToken);
    Task<string?> GetCreatorNameAsync(int userId, CancellationToken cancellationToken);
    Task<ReceivingSummary> GetReceivingSummaryAsync(long purchaseOrderId, CancellationToken cancellationToken);
}

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

public sealed class PurchaseOrderValidationException(string[] errors)
    : Exception(errors.FirstOrDefault() ?? "Thông tin đơn mua hàng chưa hợp lệ.")
{
    public string[] Errors { get; } = errors;
}

public sealed class ImportConflictException(string message) : Exception(message);
public sealed class PurchaseOrderNotFoundException() : Exception("Không tìm thấy đơn mua hàng.");
public sealed class PurchaseOrderConflictException(string message) : Exception(message);
