using Inventory.BLL.Putaway;
using Inventory.Models.Putaway;

namespace Inventory.Inbound.Tests;

internal static class PutawayRuleTests
{
    private static int checks;
    private static readonly ProductStorageProfile Product = new(101, "MODEL-101", "Thiết bị", 3, "Ngành", 7,
        "Hãng", "ACTIVE", 100, 60, 100, 25);
    private static readonly PutawaySerial Serial = new(501, "SERIAL-501", 101, "IN_WAREHOUSE", "GOOD", false,
        1, null, null, Convert.ToBase64String(new byte[8]), Product, "Kho A", 80, "PN-80", DateTime.UtcNow,
        InboundStatus: "DONE");

    public static void Run()
    {
        SerialValidation();
        ClassificationAndHierarchy();
        CapacityAndGeometry();
        BothWarehousesAndSixModels();
        Console.WriteLine($"PASS Putaway rules: {checks} assertions (hierarchy, classifications, quarantine, capacity, 2D packing, two warehouses).");
    }

    private static void Check(bool condition, string name)
    {
        checks++;
        InboundTests.Check(condition, "Putaway " + name);
    }

    private static void Reject(Action action, string code)
    {
        try { action(); }
        catch (PutawayException exception) when (exception.Code == code) { checks++; return; }
        throw new Exception("FAIL Putaway expected " + code);
    }

    private static PutawayLocationNode Node(int id, int? parent, string name, string type,
        string? storage = null, string? mode = null, string? condition = null, string? zone = null,
        decimal? length = null, decimal? width = null, decimal? height = null, decimal? weight = null) =>
        new(id, 1, parent, name, type, storage, mode, condition, zone, "ACTIVE", length, width, height, weight);

    private static PutawayWarehouseSnapshot Warehouse() => new(1, "Kho A", "ACTIVE",
    [
        Node(1, null, "A", "ZONE", zone: "RESERVE"), Node(2, 1, "Dãy 01", "ROW", "FLOOR"),
        Node(3, 2, "Ô 01", "FLOOR_SLOT", mode: "FLOOR_AREA", condition: "GOOD", length: 200, width: 120, height: 220, weight: 500),
        Node(4, 2, "Ô cách ly", "FLOOR_SLOT", mode: "FLOOR_AREA", condition: "DAMAGED", length: 200, width: 120, height: 220, weight: 500),
        Node(10, null, "B", "ZONE", zone: "STANDARD"), Node(11, 10, "Dãy 01", "ROW", "FLOOR"),
        Node(12, 11, "Ô 01", "FLOOR_SLOT", mode: "FLOOR_AREA", condition: "GOOD", length: 200, width: 120, height: 220, weight: 500),
        Node(20, null, "C", "ZONE", zone: "STANDARD"), Node(21, 20, "Dãy 01", "ROW", "RACK"),
        Node(22, 21, "Kệ 01", "RACK", condition: "GOOD"),
        Node(23, 22, "Tầng 01", "TIER", mode: "VOLUME", length: 270, width: 110, height: 150, weight: 1000)
    ], [new(10, 3, 7), new(20, 3, 7)]);

    private static PutawayCandidate Evaluate(PutawayWarehouseSnapshot? warehouse = null, int id = 3,
        PutawaySerial? serial = null, PutawayOccupant[]? occupants = null)
    {
        warehouse ??= Warehouse();
        return PutawayRules.Evaluate(serial ?? Serial, warehouse,
            warehouse.Locations.First(x => x.LocationId == id), occupants ?? []);
    }

    private static PutawayWarehouseSnapshot Change(int id, Func<PutawayLocationNode, PutawayLocationNode> change)
    {
        var warehouse = Warehouse();
        return warehouse with { Locations = warehouse.Locations.Select(x => x.LocationId == id ? change(x) : x).ToArray() };
    }

    private static PutawaySerial Package(decimal length, decimal width, decimal height = 100, decimal weight = 25) =>
        Serial with { Product = Product with { LengthCm = length, WidthCm = width, HeightCm = height, GrossWeightKg = weight } };
    private static PutawayOccupant Occupant(int index, PutawaySerial package, int locationId = 3) =>
        new(600 + index, locationId, package.ProductId, package.ItemCondition, package.Product);

    private static void SerialValidation()
    {
        PutawayRules.ValidateSerial(Serial, 1);
        Check(PutawayRules.Version(Serial.RowVersion).Length == 8, "accept schema rowversion");
        foreach (var version in new[] { "", "bad", Convert.ToBase64String(new byte[7]), Convert.ToBase64String(new byte[9]) })
            Reject(() => PutawayRules.Version(version), "INVALID_ROW_VERSION");
        Reject(() => PutawayRules.ValidateSerial(Serial, 0), "WRONG_WAREHOUSE");
        Reject(() => PutawayRules.ValidateSerial(Serial, 2), "WRONG_WAREHOUSE");
        foreach (var state in new[] { "SOLD", "IN_TRANSIT", "LOST" })
            Reject(() => PutawayRules.ValidateSerial(Serial with { StockState = state }, 1), "SERIAL_UNAVAILABLE");
        Reject(() => PutawayRules.ValidateSerial(Serial with { CurrentLocationId = 3 }, 1), "ALREADY_LOCATED");
        Reject(() => PutawayRules.ValidateSerial(Serial with { HasActiveClaim = true }, 1), "SERIAL_LOCKED");
        Reject(() => PutawayRules.ValidateSerial(Serial with { HasBlockingLock = true }, 1), "SERIAL_LOCKED");
        foreach (var status in new[] { "DRAFT", "CANCELLED", "" })
            Reject(() => PutawayRules.ValidateSerial(Serial with { InboundStatus = status }, 1), "RECEIPT_NOT_DONE");
        PutawayRules.ValidateSerial(Serial with { OriginalInboundId = null, InboundStatus = null }, 1);
        Check(true, "future officially received source does not require original inbound");
        Reject(() => PutawayRules.ValidateSerial(Serial with { OriginType = "PURCHASE", OriginalInboundId = null }, 1), "RECEIPT_NOT_DONE");
        Reject(() => PutawayRules.ValidateSerial(Serial with { ItemCondition = "UNKNOWN" }, 1), "INVALID_CONDITION");
        Reject(() => PutawayRules.ValidateSerial(Serial with { Product = Product with { ProductId = 102 } }, 1), "PRODUCT_UNAVAILABLE");
        Reject(() => PutawayRules.ValidateSerial(Serial with { Product = Product with { Status = "INACTIVE" } }, 1), "PRODUCT_UNAVAILABLE");
        foreach (var profile in new[] { Product with { LengthCm = null }, Product with { WidthCm = 0 },
            Product with { HeightCm = -1 }, Product with { GrossWeightKg = null }, Product with { GrossWeightKg = 0 } })
            Reject(() => PutawayRules.ValidateSerial(Serial with { Product = profile }, 1), "MISSING_STORAGE_PROFILE");
        PutawayRules.ValidateSerial(Serial with { ItemCondition = "DAMAGED", IsQuarantined = true }, 1);
        Check(true, "damaged quarantined serial remains eligible for matching isolation slot");
    }

    private static void ClassificationAndHierarchy()
    {
        var warehouse = Warehouse();
        var suggestions = PutawayRules.Suggest(Serial, warehouse, []);
        Check(suggestions.Candidates.Length == 3, "GOOD excludes DAMAGED slot");
        Check(suggestions.Candidates[0].ZoneType == "STANDARD" && suggestions.Candidates[^1].IsReserve,
            "STANDARD before RESERVE");
        Check(suggestions.Candidates.Select(x => x.SuggestionRank).SequenceEqual(new[] { 1, 2, 3 }), "stable ranks");
        Check(suggestions.Candidates[^1].LocationId == 3, "manual reserve remains available while standard fits");
        Check(Evaluate(warehouse with { Rules = [] }).IsValid, "RESERVE accepts category and brand without rules");
        Check(Evaluate(warehouse with { Rules = [new(1, 999, 999)] }).IsValid, "RESERVE ignores restrictive existing seed rules");
        Check(!Evaluate(warehouse with { Rules = [] }, 12).IsValid, "STANDARD requires rule");
        Check(!Evaluate(warehouse with { Rules = [new(10, 3, 8), new(10, 4, 7)] }, 12).IsValid,
            "category-brand pairs do not form a cross product");
        Check(Evaluate(warehouse with { Rules = [new(10, 3, null)] }, 12).IsValid, "explicit wildcard brand rule");
        Check(!Evaluate(serial: Serial with { ItemCondition = "DAMAGED", IsQuarantined = true }).IsValid, "DAMAGED cannot enter GOOD");
        var damaged = PutawayRules.Suggest(Serial with { ItemCondition = "DAMAGED", IsQuarantined = true }, warehouse, []);
        Check(damaged.Candidates.Length == 1 && damaged.Candidates[0].LocationId == 4, "DAMAGED uses dedicated reserve slot");
        Check(damaged.Serial.IsQuarantined && damaged.Serial.ItemCondition == "DAMAGED", "quarantine and condition preserved");
        Check(!Evaluate(id: 4).IsValid, "GOOD cannot occupy DAMAGED");
        Check(Evaluate(id: 23).IsValid, "tier uses its own volume mode and inherits rack condition");
        Check(!Evaluate(Change(22, x => x with { ConditionPolicy = "DAMAGED" }), 23).IsValid, "rack condition enforced");
        Check(!Evaluate(Change(23, x => x with { ConditionPolicy = "DAMAGED" }), 23).IsValid, "conflicting tier override rejected");
        Check(!Evaluate(Change(22, x => x with { CapacityMode = "FLOOR_AREA" }), 23).IsValid, "rack capacity mode enforced");
        Check(!Evaluate(Change(23, x => x with { CapacityMode = "FLOOR_AREA" }), 23).IsValid, "conflicting tier mode rejected");
        Check(!Evaluate(warehouse with { Status = "INACTIVE" }).IsValid, "inactive warehouse");
        Check(!Evaluate(warehouse with { WarehouseId = 2 }).IsValid, "wrong warehouse snapshot");
        foreach (var id in new[] { 1, 2, 3 })
        {
            Check(!Evaluate(Change(id, x => x with { Status = "INACTIVE" })).IsValid, "inactive ancestor " + id);
            Check(!Evaluate(Change(id, x => x with { WarehouseId = 2 })).IsValid, "foreign ancestor " + id);
        }
        Check(!Evaluate(Change(3, x => x with { ParentLocationId = 999 })).IsValid, "orphan");
        Check(!Evaluate(Change(2, x => x with { ParentLocationId = 3 })).IsValid, "cycle");
        Check(!Evaluate(Change(1, x => x with { ParentLocationId = 20 })).IsValid, "root zone must be root");
        Check(!Evaluate(Change(2, x => x with { StorageType = "RACK" })).IsValid, "floor row type");
        Check(!Evaluate(Change(21, x => x with { StorageType = "FLOOR" }), 23).IsValid, "rack row type");
        Check(!Evaluate(Change(22, x => x with { StorageType = "FLOOR" }), 23).IsValid, "rack parent storage type");
        Check(!Evaluate(Change(1, x => x with { ZoneType = "OTHER" })).IsValid, "unknown zone type");
        Check(!Evaluate(id: 1).IsValid && !Evaluate(id: 2).IsValid && !Evaluate(id: 22).IsValid, "non-leaf targets");
        Check(!Evaluate(warehouse with { Locations = warehouse.Locations.Append(warehouse.Locations[2]).ToArray() }).IsValid, "duplicate node rejected");
        Check(Evaluate().LocationPath == "Kho A → A → Dãy 01 → Ô 01", "full human-readable path");
    }

    private static void CapacityAndGeometry()
    {
        var fit = Package(100, 60);
        var three = Enumerable.Range(1, 3).Select(x => Occupant(x, fit)).ToArray();
        var capacity = Evaluate(serial: fit, occupants: three);
        Check(capacity.IsValid && capacity.OccupantCount == 3, "four serials share floor without stacking");
        Check(capacity.UsedAreaM2 == 1.8m && capacity.AvailableAreaM2 == .6m && capacity.TotalAreaM2 == 2.4m,
            "floor area uses m2");
        Check(capacity.UsedVolumeM3 is null && capacity.AvailableVolumeM3 is null, "floor does not mix volume units");
        Check(capacity.UsedWeightKg == 75 && capacity.RemainingWeightKg == 425, "weight snapshot");
        Check(!Evaluate(serial: fit, occupants: Enumerable.Range(1, 4).Select(x => Occupant(x, fit)).ToArray()).IsValid,
            "no floor area overbooking");
        var geometry = Package(121, 61);
        var impossible = Evaluate(serial: geometry, occupants: [Occupant(1, geometry)]);
        Check(!impossible.IsValid && impossible.Reason!.Contains("2D"), "area fits but rectangles cannot be adjacent");
        Check(!Evaluate(serial: Package(90, 150)).IsValid, "no automatic rotation");
        Check(!Evaluate(serial: Package(201, 10)).IsValid, "length exceeded");
        Check(!Evaluate(serial: Package(10, 121)).IsValid, "width exceeded");
        Check(!Evaluate(serial: Package(10, 10, 221)).IsValid, "floor height exceeded");
        Check(!Evaluate(serial: Package(10, 10, weight: 501)).IsValid, "single overweight");
        Check(!Evaluate(serial: Package(100, 60, weight: 150), occupants: Enumerable.Range(1, 3)
            .Select(x => Occupant(x, Package(100, 60, weight: 150))).ToArray()).IsValid, "cumulative overweight");
        Check(Evaluate(Change(3, x => x with { MaxWeightKg = null }), serial: Package(100, 60, weight: 10000)).IsValid,
            "unconfigured optional weight limit");
        Check(Evaluate(Change(3, x => x with { HeightCm = null }), serial: Package(100, 60, height: 500)).IsValid,
            "optional floor height limit");
        Check(!Evaluate(Change(3, x => x with { LengthCm = null })).IsValid, "missing location dimensions");
        Check(!Evaluate(Change(3, x => x with { MaxWeightKg = -1 })).IsValid, "invalid configured weight");
        Check(!Evaluate(occupants: [Occupant(1, Serial with { ItemCondition = "DAMAGED" })]).IsValid, "mixed occupancy condition");
        Check(!Evaluate(occupants: [Occupant(1, Serial with { Product = Product with { LengthCm = null } })]).IsValid,
            "unknown occupant profile cannot produce fake free space");
        var tierBox = Package(200, 100, 100, 100);
        var tier = Evaluate(id: 23, serial: tierBox, occupants: [Occupant(1, tierBox, 23)]);
        Check(tier.IsValid && tier.TotalVolumeM3 == 4.455m && tier.UsedVolumeM3 == 2m && tier.AvailableVolumeM3 == 2.455m,
            "tier m3 volume snapshot");
        Check(tier.UsedAreaM2 is null && tier.AvailableAreaM2 is null, "tier does not mix area units");
        Check(!Evaluate(id: 23, serial: tierBox, occupants: [Occupant(1, tierBox, 23), Occupant(2, tierBox, 23)]).IsValid,
            "tier cumulative volume");
        Check(!Evaluate(id: 23, serial: Package(100, 50, 151)).IsValid, "tier single dimension");
        var sameSerial = new PutawayOccupant(Serial.SerialItemId, 3, Product.ProductId, "GOOD", Product);
        Check(Evaluate(occupants: [sameSerial]).OccupantCount == 0, "request serial not double-counted in occupancy");
        var shuffled = three.Reverse().ToArray();
        Check(Evaluate(serial: fit, occupants: shuffled) == capacity, "packing independent of occupancy query order");
        var full = Warehouse().Locations.Where(x => x.LocationType is "TIER" or "FLOOR_SLOT")
            .Select((x, i) => new PutawayOccupant(900 + i, x.LocationId, 999, "GOOD",
                Product with { ProductId = 999, LengthCm = null })).ToArray();
        var empty = PutawayRules.Suggest(Serial, Warehouse(), full);
        Check(empty.Candidates.Length == 0 && !string.IsNullOrWhiteSpace(empty.Reason), "no-candidates explanation");
        Check(PutawayRules.Suggest(Serial, Warehouse() with { Locations = [] }, []).Reason!.Contains("chưa có"), "unconfigured warehouse explanation");
    }

    private static void BothWarehousesAndSixModels()
    {
        var models = new[]
        {
            Product with { ProductId = 701, ProductName = "Ti vi Samsung 55 inch", CategoryId = 10, BrandId = 2, LengthCm = 120, WidthCm = 25, HeightCm = 75 },
            Product with { ProductId = 702, ProductName = "Tủ lạnh Panasonic 300 lít", CategoryId = 11, BrandId = 9, LengthCm = 60, WidthCm = 65, HeightCm = 175 },
            Product with { ProductId = 703, ProductName = "Điều hòa Daikin 1 HP", CategoryId = 9, BrandId = 8, LengthCm = 100, WidthCm = 30, HeightCm = 40 },
            Product with { ProductId = 704, ProductName = "Điều hòa Panasonic 1.5 HP", CategoryId = 9, BrandId = 9, LengthCm = 100, WidthCm = 30, HeightCm = 40 },
            Product with { ProductId = 705, ProductName = "Bếp từ đôi Electrolux", CategoryId = 12, BrandId = 10, LengthCm = 80, WidthCm = 50, HeightCm = 20 },
            Product with { ProductId = 706, ProductName = "Nồi cơm điện Sharp 1.8 lít", CategoryId = 13, BrandId = 11, LengthCm = 40, WidthCm = 40, HeightCm = 45 }
        };
        var baseWarehouse = Warehouse();
        var nodes = baseWarehouse.Locations.Concat(new[] {
            Node(30, null, "D", "ZONE", zone: "STANDARD"), Node(31, 30, "Dãy 01", "ROW", "RACK"),
            Node(32, 31, "Kệ 01", "RACK", condition: "GOOD"),
            Node(33, 32, "Tầng 01", "TIER", mode: "VOLUME", length: 270, width: 110, height: 150, weight: 1000)
        }).ToArray();
        var rules = models.Select((x, i) => new PutawayZoneRule(i < 2 ? 10 : i < 4 ? 20 : 30, x.CategoryId, x.BrandId)).ToArray();
        foreach (var warehouseId in new[] { 1, 2 })
        {
            var warehouse = baseWarehouse with { WarehouseId = warehouseId, WarehouseName = "Kho " + warehouseId,
                Locations = nodes.Select(x => x with { WarehouseId = warehouseId }).ToArray(), Rules = rules };
            for (var index = 0; index < models.Length; index++)
            {
                var serial = Serial with { ProductId = models[index].ProductId, Product = models[index], CurrentWarehouseId = warehouseId };
                var suggestions = PutawayRules.Suggest(serial, warehouse, []).Candidates;
                Check(suggestions.Length == 2 && suggestions[0].LocationId == (index < 2 ? 12 : index < 4 ? 23 : 33)
                    && suggestions[1].LocationId == 3, $"warehouse {warehouseId}, model {models[index].ProductName} uses category/brand mapping and reserve");
                var occupied = new PutawayOccupant(900, suggestions[0].LocationId, 999, "GOOD", models[index] with { ProductId = 999, LengthCm = null });
                Check(PutawayRules.Suggest(serial, warehouse, [occupied]).Candidates.Single().IsReserve,
                    $"warehouse {warehouseId}, model {index} reserve fallback");
                var newModel = serial with { ProductId = 9999, Product = serial.Product with { ProductId = 9999, ProductName = "Model mới" } };
                Check(PutawayRules.Suggest(newModel, warehouse, []).Candidates.Length == 2, "new product inherits pair without product-ID hardcode");
            }
        }
    }
}
