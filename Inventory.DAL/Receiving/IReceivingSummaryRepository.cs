using Inventory.Models.Receiving;
using Microsoft.EntityFrameworkCore;

namespace Inventory.DAL.Receiving;

public interface IReceivingSummaryRepository
{ Task<PurchaseOrderReceivingSummary> GetAsync(long purchaseOrderId, CancellationToken ct); }
