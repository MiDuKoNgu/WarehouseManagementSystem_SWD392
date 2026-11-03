using Procurement.Models.PurchaseOrders;

namespace Procurement.BLL.PurchaseOrders;

public sealed class PurchaseOrderConflictException(string message) : Exception(message);
