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

public static class PutawayRules
{
    public static byte[] Version(string? version)
    {
        try
        {
            var bytes = Convert.FromBase64String(version ?? "");
            if (bytes.Length == 8) return bytes;
        }
        catch (FormatException) { }
        throw new PutawayException(400, "INVALID_ROW_VERSION", "Phiên bản serial không hợp lệ. Vui lòng tải lại.");
    }

    public static void ValidateSerial(PutawaySerial item, int warehouseId)
    {
        if (warehouseId <= 0 || item.CurrentWarehouseId != warehouseId)
            throw new PutawayException(403, "WRONG_WAREHOUSE", "Serial không thuộc kho được phân công.");
        if (item.CurrentLocationId is not null)
            throw new PutawayException(409, "ALREADY_LOCATED", "Serial đã được đặt tại " +
                (item.CurrentLocationPath ?? $"vị trí #{item.CurrentLocationId}") + ".",
                item.CurrentLocationId, item.CurrentLocationPath);
        if (item.StockState != "IN_WAREHOUSE")
            throw new PutawayException(409, "SERIAL_UNAVAILABLE", "Serial không còn ở trạng thái đã nhập kho.");
        if (item.OriginType == "PURCHASE" && item.OriginalInboundId is null)
            throw new PutawayException(409, "RECEIPT_NOT_DONE", "Serial mua hàng thiếu phiếu nhập gốc đã hoàn tất.");
        if (item.OriginalInboundId is not null && item.InboundStatus != "DONE")
            throw new PutawayException(409, "RECEIPT_NOT_DONE", "Phiếu nhập gốc chưa hoàn tất.");
        if (item.HasActiveClaim || item.HasBlockingLock)
            throw new PutawayException(409, "SERIAL_LOCKED", "Serial đang được đặt chỗ hoặc bị khóa thao tác.");
        if (item.ItemCondition is not ("GOOD" or "DAMAGED"))
            throw new PutawayException(409, "INVALID_CONDITION", "Tình trạng serial không hỗ trợ Putaway.");
        if (item.Product.ProductId != item.ProductId || item.Product.Status != "ACTIVE")
            throw new PutawayException(409, "PRODUCT_UNAVAILABLE", "Hồ sơ sản phẩm không hợp lệ hoặc đã ngừng hoạt động.");
        if (!ValidProfile(item.Product))
            throw new PutawayException(409, "MISSING_STORAGE_PROFILE", "Sản phẩm thiếu kích thước hoặc khối lượng hợp lệ.");
        // Quarantine remains a picking restriction. Putaway never clears it or changes condition.
    }

    public static PutawaySuggestions Suggest(PutawaySerial item, PutawayWarehouseSnapshot warehouse,
        IReadOnlyCollection<PutawayOccupant> occupants)
    {
        ValidateSerial(item, warehouse.WarehouseId);
        var evaluations = warehouse.Locations.Where(x => x.LocationType is "TIER" or "FLOOR_SLOT")
            .Select(x => Evaluate(item, warehouse, x, occupants)).ToArray();
        // Only the confirmed STANDARD-before-RESERVE priority is applied. Path/ID provide stable ordering.
        var candidates = evaluations.Where(x => x.IsValid).OrderBy(x => x.IsReserve)
            .ThenBy(x => x.LocationPath, StringComparer.Ordinal).ThenBy(x => x.LocationId)
            .Select((x, index) => x with { SuggestionRank = index + 1 }).ToArray();
        var reason = candidates.Length > 0 ? null : evaluations.Length == 0
            ? "Kho chưa có vị trí TIER/FLOOR_SLOT được cấu hình."
            : "Không có vị trí phù hợp: " + string.Join("; ", evaluations.Select(x => x.Reason)
                .Where(x => x is not null).Distinct().Take(5));
        return new(item, candidates, reason);
    }

    public static PutawayCandidate Evaluate(PutawaySerial item, PutawayWarehouseSnapshot warehouse,
        PutawayLocationNode target, IReadOnlyCollection<PutawayOccupant> occupants)
    {
        var result = new PutawayCandidate(target.LocationId, target.LocationName, target.LocationType,
            "", false, target.CapacityMode ?? "", target.LengthCm, target.WidthCm, target.HeightCm,
            null, null, null, null, null, null, target.MaxWeightKg, 0, target.MaxWeightKg, 0, 0, false, null);
        PutawayCandidate Fail(string reason) => result with { IsValid = false, Reason = reason };
        if (warehouse.WarehouseId != item.CurrentWarehouseId || warehouse.Status != "ACTIVE")
            return Fail("Kho không hoạt động hoặc không thuộc kho của serial.");
        if (target.LocationType is not ("FLOOR_SLOT" or "TIER"))
            return Fail("Chỉ được đặt hàng trực tiếp tại ô sàn hoặc tầng kệ.");
        if (warehouse.Locations.Count(x => x.LocationId == target.LocationId) != 1)
            return Fail("Vị trí không thuộc cây kho hoặc bị trùng định danh.");

        var chain = new List<PutawayLocationNode>();
        var seen = new HashSet<int>();
        PutawayLocationNode? node = target;
        while (node is not null)
        {
            if (!seen.Add(node.LocationId) || chain.Count >= 4)
                return Fail("Cây vị trí có chu trình hoặc sai độ sâu.");
            if (node.WarehouseId != warehouse.WarehouseId || node.Status != "ACTIVE")
                return Fail("Vị trí hoặc vị trí cha không hoạt động/cùng kho.");
            chain.Add(node);
            if (node.ParentLocationId is not int parentId) break;
            var parents = warehouse.Locations.Where(x => x.LocationId == parentId).ToArray();
            if (parents.Length != 1) return Fail("Cây vị trí thiếu hoặc trùng vị trí cha.");
            node = parents[0];
        }

        var isFloor = target.LocationType == "FLOOR_SLOT";
        var validHierarchy = isFloor
            ? chain.Count == 3 && chain[1].LocationType == "ROW" && chain[1].StorageType == "FLOOR"
                && chain[2].LocationType == "ZONE"
            : chain.Count == 4 && chain[1].LocationType == "RACK" && chain[2].LocationType == "ROW"
                && chain[2].StorageType == "RACK" && chain[3].LocationType == "ZONE";
        if (!validHierarchy || chain[^1].ParentLocationId is not null)
            return Fail("Cây vị trí phải là Zone → Dãy → Ô sàn hoặc Zone → Dãy → Kệ → Tầng.");
        if (target.StorageType is not null && target.StorageType != (isFloor ? "FLOOR" : "RACK"))
            return Fail("Loại lưu trữ của vị trí không khớp cây cha.");
        if (!isFloor && chain[1].StorageType is not null && chain[1].StorageType != "RACK")
            return Fail("Kệ cha có loại lưu trữ không hợp lệ.");
        var zone = chain[^1];
        var mode = target.CapacityMode;
        var policy = isFloor ? target.ConditionPolicy : chain[1].ConditionPolicy;
        result = result with
        {
            LocationPath = warehouse.WarehouseName + " → " + string.Join(" → ", chain.AsEnumerable().Reverse().Select(x => x.LocationName)),
            ZoneType = zone.ZoneType ?? "", IsReserve = zone.ZoneType == "RESERVE", CapacityMode = mode ?? ""
        };
        if (mode != (isFloor ? "FLOOR_AREA" : "VOLUME") ||
            !isFloor && chain[1].CapacityMode is not null && chain[1].CapacityMode != mode)
            return Fail("Chế độ sức chứa không khớp loại vị trí.");
        if (policy is not ("GOOD" or "DAMAGED") || policy != item.ItemCondition ||
            !isFloor && target.ConditionPolicy is not null && target.ConditionPolicy != policy)
            return Fail("Vị trí có chính sách tình trạng khác serial.");
        if (zone.ZoneType is not ("STANDARD" or "RESERVE"))
            return Fail("Zone chưa có chính sách STANDARD/RESERVE hợp lệ.");
        if (zone.ZoneType == "STANDARD" && !warehouse.Rules.Any(rule => rule.ZoneLocationId == zone.LocationId &&
            rule.CategoryId == item.Product.CategoryId && (rule.BrandId is null || rule.BrandId == item.Product.BrandId)))
            return Fail("Zone STANDARD không có quy tắc đúng cặp ngành hàng và thương hiệu.");
        if (item.Product.ProductId != item.ProductId || item.Product.Status != "ACTIVE" || !ValidProfile(item.Product))
            return Fail("Hồ sơ lưu trữ sản phẩm thiếu hoặc không hợp lệ.");
        if (target.LengthCm is not > 0 || target.WidthCm is not > 0 ||
            !isFloor && target.HeightCm is not > 0 || target.HeightCm is <= 0 || target.MaxWeightKg is <= 0)
            return Fail("Vị trí thiếu kích thước hoặc tải trọng hợp lệ.");

        var current = occupants.Where(x => x.LocationId == target.LocationId && x.SerialItemId != item.SerialItemId).ToArray();
        result = result with { OccupantCount = current.Length };
        if (current.Any(x => x.ItemCondition != item.ItemCondition))
            return Fail("Vị trí đang chứa serial khác tình trạng; không được trộn GOOD/DAMAGED.");
        if (current.Any(x => x.Product.ProductId != x.ProductId || !ValidProfile(x.Product)))
            return Fail("Không chứng minh được sức chứa vì hàng đang đặt thiếu hồ sơ kích thước/khối lượng.");
        var profiles = current.Select(x => x.Product).Append(item.Product).ToArray();
        if (profiles.Any(p => p.LengthCm > target.LengthCm || p.WidthCm > target.WidthCm ||
            target.HeightCm is not null && p.HeightCm > target.HeightCm))
            return Fail("Kiện hàng vượt dài/rộng/cao hữu dụng theo hướng đóng gói cố định; không tự xoay kiện.");
        var usedWeight = current.Sum(x => x.Product.GrossWeightKg!.Value);
        result = result with { UsedWeightKg = usedWeight,
            RemainingWeightKg = target.MaxWeightKg is decimal maxWeight ? maxWeight - usedWeight : null };
        if (target.MaxWeightKg is decimal limit && usedWeight + item.Product.GrossWeightKg!.Value > limit)
            return Fail("Vị trí không đủ tải trọng còn lại.");

        if (isFloor)
        {
            var total = target.LengthCm!.Value * target.WidthCm!.Value / 10_000m;
            var used = current.Sum(x => Footprint(x.Product));
            result = result with { TotalAreaM2 = total, UsedAreaM2 = used, AvailableAreaM2 = total - used };
            if (used + Footprint(item.Product) > total) return Fail("Ô sàn không đủ diện tích còn lại.");
            // First-fit decreasing shelves prove a repeatable, no-rotation, no-stacking 2D layout.
            // Coordinates are deliberately not persisted: staff must physically arrange to this model.
            if (!CanPackFloor(target.LengthCm.Value, target.WidthCm.Value,
                current.Select(x => (x.SerialItemId, x.Product)).Append((item.SerialItemId, item.Product))))
                return Fail("Chưa chứng minh được bố trí cạnh nhau 2D không xếp chồng, dù tổng diện tích có thể còn đủ.");
        }
        else
        {
            var total = target.LengthCm!.Value * target.WidthCm!.Value * target.HeightCm!.Value / 1_000_000m;
            var used = current.Sum(x => Volume(x.Product));
            result = result with { TotalVolumeM3 = total, UsedVolumeM3 = used, AvailableVolumeM3 = total - used };
            if (used + Volume(item.Product) > total) return Fail("Tầng kệ không đủ thể tích còn lại.");
        }
        return result with { IsValid = true, Reason = null };
    }

    private static bool ValidProfile(ProductStorageProfile profile) => profile.LengthCm is > 0 &&
        profile.WidthCm is > 0 && profile.HeightCm is > 0 && profile.GrossWeightKg is > 0;
    private static decimal Footprint(ProductStorageProfile profile) => profile.LengthCm!.Value * profile.WidthCm!.Value / 10_000m;
    private static decimal Volume(ProductStorageProfile profile) => profile.LengthCm!.Value * profile.WidthCm!.Value * profile.HeightCm!.Value / 1_000_000m;

    private static bool CanPackFloor(decimal length, decimal width,
        IEnumerable<(long SerialItemId, ProductStorageProfile Product)> packages)
    {
        var shelves = new List<(decimal Height, decimal UsedLength)>();
        var usedWidth = 0m;
        foreach (var package in packages.OrderByDescending(x => x.Product.WidthCm)
            .ThenByDescending(x => x.Product.LengthCm).ThenBy(x => x.SerialItemId))
        {
            var packageLength = package.Product.LengthCm!.Value;
            var packageWidth = package.Product.WidthCm!.Value;
            var existingShelf = shelves.FindIndex(x => packageWidth <= x.Height && x.UsedLength + packageLength <= length);
            if (existingShelf >= 0)
            {
                var shelf = shelves[existingShelf];
                shelves[existingShelf] = (shelf.Height, shelf.UsedLength + packageLength);
            }
            else
            {
                if (packageLength > length || usedWidth + packageWidth > width) return false;
                shelves.Add((packageWidth, packageLength));
                usedWidth += packageWidth;
            }
        }
        return true;
    }
}
