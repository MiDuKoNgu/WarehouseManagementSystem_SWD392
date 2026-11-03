using System.Net.Http.Json;
using Procurement.BLL.PurchaseOrders;
using Procurement.Models.PurchaseOrders;

namespace Procurement.API.Clients;

public sealed partial class PurchaseOrderLookupClient
{
    private sealed record DisplayName(int UserId, string FullName);
}
