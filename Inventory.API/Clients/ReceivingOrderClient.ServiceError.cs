using System.Net.Http.Json;
using Inventory.BLL.Inbound;
using Inventory.Models.Inbound;

namespace Inventory.API.Clients;

public sealed partial class ReceivingOrderClient
{
    private sealed record ServiceError(string Message);
}
