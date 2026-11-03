using Procurement.Models.PurchaseOrders;

namespace Procurement.BLL.PurchaseOrders;

public sealed class ImportConflictException(string message) : Exception(message);
