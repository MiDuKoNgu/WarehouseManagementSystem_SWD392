using Procurement.Models.PurchaseOrders;

namespace Procurement.BLL.PurchaseOrders;

public sealed class PurchaseOrderNotFoundException() : Exception("Không tìm thấy đơn mua hàng.");
