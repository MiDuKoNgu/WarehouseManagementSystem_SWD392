using System.Collections.Concurrent;
using System.Security.Cryptography;
using Procurement.Models.PurchaseOrders;

namespace Procurement.BLL.PurchaseOrders;

public sealed class PurchaseOrderService(IPurchaseOrderRepository repository, IPurchaseOrderLookup lookup,
    IPurchaseOrderWorkbook workbook, ImportPreviewStore previews)
{
    public Task<PurchaseOrderOptions> OptionsAsync(CancellationToken ct) => lookup.GetOptionsAsync(ct);

    public async Task<PurchaseOrderSummary[]> ListAsync(CancellationToken ct)
    {
        var options = await lookup.GetOptionsAsync(ct);
        return (await repository.ListAsync(ct)).Select(order => Summary(order, options)).ToArray();
    }

    public async Task<PurchaseOrderDetail?> FindAsync(long id, CancellationToken ct)
    {
        var order = await repository.FindAsync(id, ct);
        if (order is null) return null;
        var receiving = await lookup.GetReceivingSummaryAsync(id, ct);
        ValidateReceiving(order, receiving);
        return Detail(order, await lookup.GetOptionsAsync(ct), await lookup.GetCreatorNameAsync(order.CreatedByUserId, ct), receiving);
    }

    public async Task<PurchaseOrderDetail> CreateAsync(CreatePurchaseOrder request, int userId, string userName, CancellationToken ct)
    {
        var options = await lookup.GetOptionsAsync(ct);
        Validate(request, options);
        var stored = await repository.CreateAsync(Normalize(request), userId, userName, "CREATE_PO", ct);
        return Detail(stored, options, userName);
    }

    public async Task<byte[]> TemplateAsync(CancellationToken ct) => workbook.CreateTemplate(await lookup.GetOptionsAsync(ct));

    public async Task<PurchaseOrderDetail> UpdateAsync(long id, UpdatePurchaseOrder request, int userId, string userName, CancellationToken ct)
    {
        var order = await Existing(id, request.RowVersion, ct);
        RequireState(order, "DRAFT");
        var receiving = await lookup.GetReceivingSummaryAsync(id, ct);
        ValidateReceiving(order, receiving);
        if (receiving.HasAnyInbound) throw new PurchaseOrderConflictException("Không thể sửa PO đã có phiếu nhập.");
        var options = await lookup.GetOptionsAsync(ct);
        Validate(request.ToCreate(), options);
        var creator = await lookup.GetCreatorNameAsync(order.CreatedByUserId, ct);
        var saved = await repository.UpdateAsync(order, Normalize(request.ToCreate()), userId, userName, ct);
        return Detail(saved, options, creator, new(id, false, 0, []));
    }

    public async Task<PurchaseOrderDetail> ConfirmOrderAsync(long id, string rowVersion, int userId, string userName, CancellationToken ct)
    {
        var order = await Existing(id, rowVersion, ct);
        RequireState(order, "DRAFT");
        var receiving = await lookup.GetReceivingSummaryAsync(id, ct);
        ValidateReceiving(order, receiving);
        if (receiving.HasAnyInbound) throw new PurchaseOrderConflictException("PO nháp đã có dữ liệu nhập hàng không hợp lệ. Vui lòng kiểm tra trước khi xác nhận.");
        var options = await lookup.GetOptionsAsync(ct);
        Validate(new(order.SupplierId, order.WarehouseId, order.ExpectedArrivalDate, order.Note,
            order.Lines.Select(line => new CreateLine(line.ProductId, line.Quantity)).ToArray()), options);
        var creator = await lookup.GetCreatorNameAsync(order.CreatedByUserId, ct);
        var saved = await repository.TransitionAsync(order, "CONFIRMED", null, new Dictionary<long, int>(), userId, userName, ct);
        return Detail(saved, options, creator, receiving);
    }

    public async Task<PurchaseOrderDetail> CancelAsync(long id, FinishPurchaseOrder request, int userId, string userName, CancellationToken ct)
    {
        var reason = Reason(request.Reason);
        var order = await Existing(id, request.RowVersion, ct);
        RequireState(order, "DRAFT", "CONFIRMED");
        var receiving = await lookup.GetReceivingSummaryAsync(id, ct);
        ValidateReceiving(order, receiving);
        if (receiving.HasAnyInbound) throw new PurchaseOrderConflictException("Chỉ được hủy PO chưa có bất kỳ phiếu nhập nào.");
        var options = await lookup.GetOptionsAsync(ct);
        var creator = await lookup.GetCreatorNameAsync(order.CreatedByUserId, ct);
        var saved = await repository.TransitionAsync(order, "CANCELLED", reason, new Dictionary<long, int>(), userId, userName, ct);
        return Detail(saved, options, creator, receiving);
    }

    public async Task<PurchaseOrderDetail> CloseAsync(long id, FinishPurchaseOrder request, int userId, string userName, CancellationToken ct)
    {
        var reason = Reason(request.Reason);
        var order = await Existing(id, request.RowVersion, ct);
        RequireState(order, "CONFIRMED");
        var receiving = await lookup.GetReceivingSummaryAsync(id, ct);
        ValidateReceiving(order, receiving);
        if (receiving.OpenInboundCount > 0) throw new PurchaseOrderConflictException("Chưa thể đóng PO khi còn phiếu nhập đang mở.");
        var received = receiving.Lines.ToDictionary(line => line.PoLineId, line => line.ReceivedQuantity);
        var closed = order.Lines.ToDictionary(line => line.PoLineId, line => line.Quantity - received.GetValueOrDefault(line.PoLineId));
        var options = await lookup.GetOptionsAsync(ct);
        var creator = await lookup.GetCreatorNameAsync(order.CreatedByUserId, ct);
        var saved = await repository.TransitionAsync(order, "CLOSED", reason, closed, userId, userName, ct);
        return Detail(saved, options, creator, receiving);
    }

    private async Task<StoredPurchaseOrder> Existing(long id, string rowVersion, CancellationToken ct)
    {
        var order = await repository.FindAsync(id, ct) ?? throw new PurchaseOrderNotFoundException();
        byte[] supplied;
        try { supplied = Convert.FromBase64String(rowVersion ?? ""); }
        catch (FormatException) { throw new PurchaseOrderValidationException(["Phiên bản PO không hợp lệ. Vui lòng tải lại chi tiết."]); }
        if (supplied.Length != 8) throw new PurchaseOrderValidationException(["Phiên bản PO không hợp lệ. Vui lòng tải lại chi tiết."]);
        if (rowVersion != order.RowVersion) throw new PurchaseOrderConflictException("PO đã được thay đổi. Vui lòng tải lại trước khi thực hiện.");
        return order;
    }

    private static void RequireState(StoredPurchaseOrder order, params string[] allowed)
    {
        if (!allowed.Contains(order.Status.ToUpperInvariant()))
            throw new PurchaseOrderConflictException("Trạng thái PO không cho phép thao tác này. Vui lòng tải lại chi tiết.");
    }

    private static string Reason(string? reason)
    {
        var value = reason?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > 1000)
            throw new PurchaseOrderValidationException(["Vui lòng nhập lý do từ 1 đến 1000 ký tự."]);
        return value;
    }

    private static void ValidateReceiving(StoredPurchaseOrder order, ReceivingSummary receiving)
    {
        if (receiving.HasInvalidReceipts || receiving.PurchaseOrderId != order.PurchaseOrderId || receiving.OpenInboundCount < 0
            || (!receiving.HasAnyInbound && (receiving.OpenInboundCount > 0 || receiving.Lines.Any(line => line.ReceivedQuantity > 0)))
            || receiving.Lines.GroupBy(line => line.PoLineId).Any(group => group.Count() > 1))
            throw new PurchaseOrderValidationException(["Dữ liệu nhận hàng của PO không nhất quán. Vui lòng kiểm tra phiếu nhập."]);
        foreach (var line in receiving.Lines)
        {
            var ordered = order.Lines.SingleOrDefault(item => item.PoLineId == line.PoLineId);
            if (ordered is null || line.ReceivedQuantity < 0 || line.ReceivedQuantity > ordered.Quantity
                || line.ReceivedQuantity + (long)ordered.ClosedQuantity > ordered.Quantity)
                throw new PurchaseOrderValidationException(["Số lượng nhận/đóng hoặc dòng hàng không khớp PO. Không thể thực hiện thao tác."]);
        }
    }

    public async Task<ImportPreview> PreviewAsync(Stream stream, int userId, CancellationToken ct)
    {
        var parsed = workbook.Parse(stream);
        var options = await lookup.GetOptionsAsync(ct);
        var errors = parsed.Errors.ToList();
        if (parsed.Rows.Length == 0) errors.Add("File không có dòng sản phẩm.");
        if (parsed.Rows.Length > 500) errors.Add("Mỗi PO tối đa 500 dòng sản phẩm.");
        var duplicates = parsed.Rows.GroupBy(row => row.ProductCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Key.Length > 0 && group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var first = parsed.Rows.FirstOrDefault();
        foreach (var row in parsed.Rows)
        {
            if (row.SupplierId is not { } supplier || !options.Suppliers.Any(value => value.SupplierId == supplier))
                row.Errors.Add("Nhà cung cấp không tồn tại hoặc không hoạt động.");
            if (row.WarehouseId is not { } warehouse || !options.Warehouses.Any(value => value.WarehouseId == warehouse))
                row.Errors.Add("Kho nhận không tồn tại hoặc không hoạt động.");
            var product = options.Products.FirstOrDefault(value => value.ProductCode.Equals(row.ProductCode.Trim(), StringComparison.OrdinalIgnoreCase));
            row.ProductName = product?.ProductName;
            if (product is null) row.Errors.Add("Mã sản phẩm không tồn tại hoặc không hoạt động.");
            else if (row.SupplierId is { } supplierId && !product.SupplierIds.Contains(supplierId))
                row.Errors.Add("Sản phẩm không thuộc nhà cung cấp đã chọn.");
            if (row.Quantity is not > 0) row.Errors.Add("Số lượng phải là số nguyên lớn hơn 0.");
            if (duplicates.Contains(row.ProductCode.Trim())) row.Errors.Add("Sản phẩm bị trùng trong đơn mua hàng.");
            if (row.Note?.Length > 1000) row.Errors.Add("Ghi chú tối đa 1000 ký tự.");
            if (row.ExpectedArrivalDate is { } date && date < Today()) row.Errors.Add("Ngày dự kiến giao không được trước ngày tạo đơn.");
            if (first is not null && (row.SupplierId != first.SupplierId || row.WarehouseId != first.WarehouseId
                || row.ExpectedArrivalDate != first.ExpectedArrivalDate || NormalizeNote(row.Note) != NormalizeNote(first.Note)))
                row.Errors.Add("Mọi dòng phải cùng nhà cung cấp, kho, ngày dự kiến giao và ghi chú vì một file tạo một PO.");
        }
        var valid = errors.Count == 0 && parsed.Rows.All(row => row.Errors.Count == 0);
        string? token = null;
        if (valid && first is not null)
        {
            var request = new CreatePurchaseOrder(first.SupplierId!.Value, first.WarehouseId!.Value, first.ExpectedArrivalDate,
                NormalizeNote(first.Note), parsed.Rows.Select(row => new CreateLine(
                    options.Products.Single(product => product.ProductCode.Equals(row.ProductCode.Trim(), StringComparison.OrdinalIgnoreCase)).ProductId,
                    row.Quantity!.Value)).ToArray());
            token = previews.Add(userId, request);
        }
        return new(token, valid, parsed.Rows, errors.ToArray());
    }

    public async Task<PurchaseOrderDetail> ConfirmAsync(string token, int userId, string userName, CancellationToken ct)
    {
        var entry = previews.Get(token, userId);
        await entry.Gate.WaitAsync(ct);
        try
        {
            if (entry.CreatedId is { } created)
                return await FindAsync(created, ct) ?? throw new ImportConflictException("PO đã tạo không còn tồn tại.");
            if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
                throw new ImportConflictException("Bản xem trước đã hết hạn. Vui lòng tải lại file.");
            var options = await lookup.GetOptionsAsync(ct);
            Validate(entry.Request, options);
            var importKey = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
            var order = await repository.CreateAsync(entry.Request, userId, userName, "IMPORT_PO_EXCEL", ct, importKey);
            entry.CreatedId = order.PurchaseOrderId;
            return Detail(order, options, userName);
        }
        finally { entry.Gate.Release(); }
    }

    public static void Validate(CreatePurchaseOrder request, PurchaseOrderOptions options)
    {
        var errors = new List<string>();
        if (!options.Suppliers.Any(value => value.SupplierId == request.SupplierId)) errors.Add("Nhà cung cấp không tồn tại hoặc không hoạt động.");
        if (!options.Warehouses.Any(value => value.WarehouseId == request.WarehouseId)) errors.Add("Kho nhận không tồn tại hoặc không hoạt động.");
        if (request.ExpectedArrivalDate is { } date && date < Today()) errors.Add("Ngày dự kiến giao không được trước ngày tạo đơn.");
        if (request.Note?.Length > 1000) errors.Add("Ghi chú tối đa 1000 ký tự.");
        if (request.Lines is null || request.Lines.Length is < 1 or > 500) errors.Add("Đơn mua hàng cần từ 1 đến 500 dòng sản phẩm.");
        var seen = new HashSet<int>();
        foreach (var line in request.Lines ?? [])
        {
            if (line is null) { errors.Add("Dòng sản phẩm không được rỗng."); continue; }
            var product = options.Products.FirstOrDefault(value => value.ProductId == line.ProductId);
            if (product is null) errors.Add($"Sản phẩm #{line.ProductId} không tồn tại hoặc không hoạt động.");
            else if (!product.SupplierIds.Contains(request.SupplierId)) errors.Add($"{product.ProductCode} không thuộc nhà cung cấp đã chọn.");
            if (!seen.Add(line.ProductId)) errors.Add($"Sản phẩm #{line.ProductId} bị trùng.");
            if (line.Quantity <= 0) errors.Add("Số lượng đặt mua phải lớn hơn 0.");
        }
        if (errors.Count > 0) throw new PurchaseOrderValidationException(errors.ToArray());
    }

    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    private static CreatePurchaseOrder Normalize(CreatePurchaseOrder request) => request with { Note = NormalizeNote(request.Note) };
    private static PurchaseOrderSummary Summary(StoredPurchaseOrder order, PurchaseOrderOptions options) => new(
        order.PurchaseOrderId, order.PoCode, order.SupplierId,
        options.Suppliers.FirstOrDefault(value => value.SupplierId == order.SupplierId)?.SupplierName ?? $"Nhà cung cấp #{order.SupplierId}",
        order.WarehouseId, options.Warehouses.FirstOrDefault(value => value.WarehouseId == order.WarehouseId)?.WarehouseName ?? $"Kho #{order.WarehouseId}",
        order.OrderDate, order.ExpectedArrivalDate, order.Status.ToUpperInvariant(), order.CreatedAt, order.Lines.Length, order.Lines.Sum(line => (long)line.Quantity));
    private static PurchaseOrderDetail Detail(StoredPurchaseOrder order, PurchaseOrderOptions options, string? creatorName = null, ReceivingSummary? receiving = null)
    {
        var summary = Summary(order, options);
        return new(summary.PurchaseOrderId, summary.PoCode, summary.SupplierId, summary.SupplierName, summary.WarehouseId,
            summary.WarehouseName, summary.OrderDate, summary.ExpectedArrivalDate, summary.Status, summary.CreatedAt,
            summary.LineCount, summary.TotalQuantity, order.CreatedByUserId, creatorName ?? $"Người dùng #{order.CreatedByUserId}", order.Note,
            order.Lines.Select(line => {
                var product = options.Products.FirstOrDefault(value => value.ProductId == line.ProductId);
                var received = receiving?.Lines.SingleOrDefault(item => item.PoLineId == line.PoLineId)?.ReceivedQuantity ?? 0;
                return new PurchaseOrderLine(line.PoLineId, line.ProductId, product?.ProductCode ?? $"#{line.ProductId}",
                    product?.ProductName ?? $"Sản phẩm #{line.ProductId}", product?.Unit ?? "", line.Quantity,
                    line.ClosedQuantity, received, order.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) ? 0 : line.Quantity - line.ClosedQuantity - received);
            }).ToArray(), order.RowVersion, order.ConfirmedAt, order.ClosedAt, order.CloseReason, receiving?.HasAnyInbound ?? false, receiving?.OpenInboundCount ?? 0);
    }
}
