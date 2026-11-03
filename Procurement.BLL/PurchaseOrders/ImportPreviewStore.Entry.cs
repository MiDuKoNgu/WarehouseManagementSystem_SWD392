using Procurement.Models.PurchaseOrders;

namespace Procurement.BLL.PurchaseOrders;

public sealed partial class ImportPreviewStore
{
    public sealed class Entry(int userId, CreatePurchaseOrder request)
    {
        public int UserId { get; } = userId;
        public CreatePurchaseOrder Request { get; } = request;
        public DateTimeOffset ExpiresAt { get; } = DateTimeOffset.UtcNow.AddMinutes(15);
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public long? CreatedId { get; set; }
    }
}
