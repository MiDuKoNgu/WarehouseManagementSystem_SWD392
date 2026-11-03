using Inventory.Models.Inbound;

namespace Inventory.BLL.Inbound;

public sealed class ReceiptException(int statusCode, string message) : Exception(message)
{ public int StatusCode { get; } = statusCode; }
