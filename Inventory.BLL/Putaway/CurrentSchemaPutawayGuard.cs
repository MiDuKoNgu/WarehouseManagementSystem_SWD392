using Inventory.Models.Putaway;

namespace Inventory.BLL.Putaway;

public sealed class CurrentSchemaPutawayGuard : IPutawayOperationGuard
{
    public Task ValidateAsync(PutawaySerial serial, PutawayWarehouseSnapshot warehouse, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // The final rental day and pre-existing awaiting goods have no agreed policy yet.
        // Never silently authorize putaway into an expired or incompletely configured rented warehouse.
        var today = DateTime.UtcNow.AddHours(7).Date;
        if (warehouse.WarehouseType == "RENTED" && (warehouse.RentalEndDate is null || warehouse.RentalEndDate.Value.Date <= today))
            throw new PutawayException(409, "RENTAL_POLICY_REQUIRED", "Kho thuê đã đến/hết hạn hoặc thiếu hạn thuê. Cần xác nhận chính sách trước khi Putaway.");
        return Task.CompletedTask;
    }
}
