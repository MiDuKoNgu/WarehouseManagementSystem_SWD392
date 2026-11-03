using Inventory.Models.Putaway;

namespace Inventory.BLL.Putaway;

/// <summary>Extension point for future stocktake/claim controls. Invoke again inside the write transaction.</summary>
public interface IPutawayOperationGuard
{
    Task ValidateAsync(PutawaySerial serial, PutawayWarehouseSnapshot warehouse, CancellationToken ct);
}
