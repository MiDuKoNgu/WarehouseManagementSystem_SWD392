using Microsoft.EntityFrameworkCore;

namespace Inventory.DAL.Receiving;

public sealed record ReservationProof(bool Exists, bool Terminal, long PurchaseOrderId, int WarehouseId);
