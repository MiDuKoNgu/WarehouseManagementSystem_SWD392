using Inventory.Models.Putaway;

namespace Inventory.BLL.Putaway;

public sealed class PutawayException(int statusCode, string code, string message,
    int? currentLocationId = null, string? currentLocationPath = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public int? CurrentLocationId { get; } = currentLocationId;
    public string? CurrentLocationPath { get; } = currentLocationPath;
}
