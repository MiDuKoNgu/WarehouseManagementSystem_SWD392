using Procurement.Models.PurchaseOrders;

namespace Procurement.BLL.PurchaseOrders;

public sealed class PurchaseOrderValidationException(string[] errors)
    : Exception(errors.FirstOrDefault() ?? "Thông tin đơn mua hàng chưa hợp lệ.")
{
    public string[] Errors { get; } = errors;
}
