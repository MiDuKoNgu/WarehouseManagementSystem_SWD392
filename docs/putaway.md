# Putaway theo Serial

Putaway gán vị trí cho SerialItem đã nhận chính thức, đang `IN_WAREHOUSE`, thuộc kho của Warehouse Staff và chưa có `CurrentLocationID`. Chức năng dùng các project .NET 8 và React hiện hữu; không tạo thêm thiết bị, không đổi schema và không tăng on hand lần nữa.

## Hiện trạng và phần bổ sung

Trước thay đổi, Inbound DONE đã tạo SerialItem chưa có vị trí, SerialMovement PURCHASE_RECEIPT, AuditLog và SupplySource. `GET /api/inventory/pending-putaway` cùng màn `/warehouse/pending-putaway` chỉ phục vụ tra cứu; chưa có API xác nhận, kiểm sức chứa hoặc chống đặt đồng thời. Catalog options chưa trả profile đóng gói; endpoint quản trị Product chỉ cho Purchasing. Warehouse options chưa trả cây vị trí và quy tắc Zone.

Phần bổ sung cung cấp danh sách chờ có thông tin sản phẩm/phân trang, tìm bằng Serial, chi tiết, gợi ý vị trí và POST xác nhận. BLL giữ quy tắc cây/condition/Category + Brand/capacity; DAL kiểm lại dữ liệu authoritative và ghi Inventory trong transaction. API đọc Catalog và Warehouse giữ quyền đọc theo miền, không mở quyền sửa sản phẩm cho nhân viên kho.

Các file chính:

| Nhóm | File | Vai trò |
|---|---|---|
| Inventory Models | `Inventory.Models/Putaway/PutawayContracts.cs` | DTO serial, profile, vị trí, capacity, request và result |
| Inventory BLL | `Inventory.BLL/Putaway/PutawayRules.cs` | Validation cây, condition, rule và sức chứa |
| Inventory BLL | `Inventory.BLL/Putaway/PutawayService.cs` | Điều phối, kiểm request và điểm nối operation guard |
| Inventory DAL | `Inventory.DAL/Putaway/SqlPutawayRepository.cs` | Đọc authoritative, khóa và transaction |
| Inventory API | `Inventory.API/Controllers/PutawayController.cs` | API dành cho Warehouse Staff |
| Inventory API | `Inventory.API/Program.cs` | DI và xử lý lỗi Putaway |
| Inventory DAL | `Inventory.DAL/Receiving/ReceiptQueries.cs` | Giữ route cũ và cho phép nguồn nhận gốc tùy chọn |
| Catalog | `Catalog.Models/Products/ProductStorageProfile.cs` | Contract đọc profile đóng gói |
| Catalog | `Catalog.DAL/Repositories/ProductStorageProfileRepository.cs` | SELECT profile và Category/Brand |
| Catalog | `Catalog.Models/Products/ProductStorageProfile.cs` | DTO đọc profile, status và RowVersion |
| Catalog | `Catalog.API/Controllers/ProductStorageProfilesController.cs` | Endpoint đọc cho nhân viên được gán kho |
| Catalog | `Catalog.API/Program.cs` | Đăng ký repository đọc |
| Warehouse | `Warehouse.Models/Lookups/PutawayLayout.cs` | Contract kho, cây và ZoneStorageRule |
| Warehouse | `Warehouse.DAL/Repositories/PutawayLayoutRepository.cs` | SELECT layout trong kho được giao |
| Warehouse | `Warehouse.API/Controllers/PutawayLayoutController.cs` | Endpoint layout theo warehouse claim |
| Warehouse | `Warehouse.API/Program.cs` | Đăng ký repository đọc |
| React | `frontend/src/features/inbound/pending-putaway-page.tsx` | Danh sách, scan, chọn vị trí và xác nhận |
| React | `frontend/src/features/inbound/putaway-api.ts` | Types, GET/POST và lấy CSRF token |
| React | `frontend/src/features/inbound/putaway.css` | Bố cục chi tiết, capacity và xác nhận |
| React | `frontend/vite.config.ts` | Proxy Putaway đến Inventory |
| React | `frontend/src/features/inbound/putaway-api.ts` | Types, tra cứu query string và POST có CSRF |
| React | `frontend/src/features/inbound/putaway.css` | Layout desktop/mobile, danh sách vị trí và modal |
| React | `frontend/src/features/inbound/putaway-location-filter.ts` | Tìm vị trí không phân biệt dấu/hoa thường/số 0 đầu, khớp đúng Zone/Dãy/Kệ/Tầng |
| React | `frontend/src/features/inbound/putaway-location-filters.tsx` | Bộ lọc nhanh bằng từ khóa và các danh sách chọn phụ thuộc |
| React | `frontend/src/features/inbound/putaway-serial-capture.tsx` | Dùng chung quét camera/ảnh tem với Inbound, chỉ điền Serial tra cứu |
| React | `frontend/src/features/inbound/serial-capture.tsx` | Bộ nhận diện dùng chung; dừng camera khi khóa, đổi ngữ cảnh hoặc đóng màn |
| React | `frontend/src/features/inbound/layout.tsx` | Breadcrumb Putaway |
| Inventory | `Inventory.API/Program.cs` | DI và error contract Putaway |
| Inventory | `Inventory.DAL/Receiving/ReceiptQueries.cs` | Giữ route cũ, LEFT JOIN nguồn tùy chọn, loại PURCHASE thiếu phiếu |
| Test | `tests/Inventory.Inbound.Tests/PutawayRuleTests.cs` | Quy tắc, hình học, capacity, hai kho và sáu mapping |
| Test | `tests/Inventory.Inbound.Tests/PutawayApiTests.cs` | Role, claim kho, CSRF, giả actor, query và contract 409 |
| Test | `tests/Inventory.Inbound.Tests/PutawaySqlTests.cs` | Transaction, cạnh tranh capacity/Serial, rollback và bất biến dữ liệu |
| Test | `tests/Inventory.Inbound.Tests/Program.cs` | Gọi các bộ kiểm thử Putaway trong project sẵn có |
| Test | `tests/Inventory.Inbound.Tests/SqlTests.cs` | Tìm đúng file fixture khi chạy từ artifacts directory |
| Test | `tests/Inventory.Inbound.Tests/README.md` | Lệnh chạy SQL disposable bằng Windows Authentication |
| Test | `tests/Catalog.Products.Tests/ProfileApiTests.cs` | Kiểm quyền và SELECT thật profile/layout theo hai warehouse claim |
| Test | `tests/Catalog.Products.Tests/Catalog.Products.Tests.csproj` | Tham chiếu Warehouse.API cho test và chế độ profile-only |
| Test | `tests/Catalog.Products.Tests/ProductTests.cs` | Kết nối kiểm thử profile vào entrypoint cũ |
| Test | `frontend/tests/putaway.test.tsx` | Chọn Reserve, bộ lọc vị trí, xác nhận không quét lần hai, CSRF, 409 và scan lặp |
| Test | `frontend/tests/putaway-location-filter.test.ts` | Khớp đúng thành phần đường dẫn, số thứ tự và dấu tiếng Việt |
| Test | `frontend/tests/putaway-capture.test.tsx` | Camera điền Serial sau 3 lần đọc trùng, dừng stream, không tự submit |
| Test | `frontend/tests/inbound.test.tsx` | Cập nhật mock pending sang response có profile/phân trang |
| Tài liệu | `docs/putaway.md` | Contracts, chứng cứ kiểm thử, SQL SELECT và walkthrough |
| Tests | `tests/Catalog.Products.Tests/ProfileApiTests.cs` | Role, assignment, profile và layout hai kho |
| Tests | `tests/Catalog.Products.Tests/Catalog.Products.Tests.csproj` | Tái dùng project test, tùy chọn chạy riêng test profile |
| Tests | `tests/Catalog.Products.Tests/ProductTests.cs` | Gọi test đọc profile từ suite Catalog hiện hữu |

Danh sách file thực tế đầy đủ và kết quả lệnh build/test được ghi trong báo cáo bàn giao. Các file OCR đã sửa từ trước trong working tree không thuộc thay đổi nghiệp vụ Putaway.

## API và quyền

Mọi endpoint Putaway yêu cầu `WAREHOUSE_STAFF`; actor và kho lấy từ session claims server-side. Tài khoản Admin/Purchasing không được thay nhân viên kho thực hiện Putaway. POST yêu cầu cookie session và `X-CSRF-TOKEN`, lấy từ `/api/auth/csrf` theo cơ chế sẵn có.

| Method | Route | Kết quả |
|---|---|---|
| GET | `/api/putaway/pending?search=&page=1&pageSize=25` | `{items,totalCount,page,pageSize}`, tối đa 100 dòng/trang |
| GET | `/api/putaway/serials/{serialNumber}` | Một serial thuộc kho claim; URL encode serial |
| GET | `/api/putaway/serials?serialNumber=` | Cách scan React sử dụng; hỗ trợ Serial có dấu `/`, `%`, `+` |
| GET | `/api/putaway/{serialItemId}` | Chi tiết, profile, trạng thái, vị trí hiện có và RowVersion |
| GET | `/api/putaway/{serialItemId}/suggestions` | `{serial,candidates,reason}` và capacity snapshot |
| POST | `/api/putaway` | Xác nhận gán vị trí |
| GET | `/api/inventory/pending-putaway` | Route tra cứu cũ được giữ để tránh phá client |
| GET | `/api/catalog/storage-profiles/{productId}` | Product/Category/Brand/status/kích thước/kg/RowVersion, quyền đọc riêng |
| GET | `/api/warehouses/putaway-layout` | Kho claim, flat nodes có ParentLocationID và ZoneStorageRule |

Ví dụ đọc danh sách chờ:

```http
GET /api/putaway/pending?search=Manufacturer-Serial&page=1&pageSize=25
```

```json
{"items":[],"totalCount":0,"page":1,"pageSize":25}
```

Ví dụ POST: lấy ID và RowVersion thật từ GET, không dùng giá trị minh họa để thao tác production.

```http
POST /api/putaway
Content-Type: application/json
X-CSRF-TOKEN: <token-cua-session>
```

```json
{"serialItemId":123,"targetLocationId":456,"rowVersion":"AAAAAAAAB9E="}
```

Kết quả thành công có hình dạng:

```json
{
  "serialItemId":123,
  "serialNumber":"Manufacturer-Serial",
  "targetLocationId":456,
  "locationPath":"Kho A → B → Dãy 01 → Ô sàn 01",
  "occurredAt":"2026-11-01T14:00:00Z",
  "actorUserId":7,
  "actorName":"Warehouse Staff",
  "rowVersion":"AAAAAAAAB9I="
}
```

Trả lỗi nghiệp vụ theo `{code,message,currentLocationId,currentLocationPath}`; 400 cho query/request/RowVersion sai, 401 khi chưa đăng nhập, 403 khi sai role hoặc chưa được gán kho, 404 khi không có serial trong phạm vi truy cập, 409 khi trạng thái/phiên bản/vị trí/sức chứa đã thay đổi, 503 khi dữ liệu không khả dụng. Lỗi CSRF giữ contract hiện hữu. Khóa bận hoặc deadlock/lock timeout trả 409 để tải lại và thử lại. Thao tác retry sau commit không tạo movement thứ hai; tải lại serial để xem vị trí hiện tại.

Ví dụ xung đột khi đã đặt serial:

```json
{"code":"ALREADY_LOCATED","message":"Serial đã được đặt tại Kho A → B → Dãy 01 → Ô sàn 01.","currentLocationId":456,"currentLocationPath":"Kho A → B → Dãy 01 → Ô sàn 01"}
```

Các code chính: `INVALID_QUERY`, `INVALID_SERIAL`, `INVALID_ROW_VERSION`, `WAREHOUSE_REQUIRED`, `SERIAL_NOT_FOUND`, `ALREADY_LOCATED`, `STALE_SERIAL`, `SERIAL_UNAVAILABLE`, `RECEIPT_NOT_DONE`, `LOCATION_UNAVAILABLE`, `LOCATION_BUSY`, `CONCURRENT_PUTAWAY`, `MOVEMENT_GUARD_REQUIRED`, `RENTAL_POLICY_REQUIRED`.

## Quy tắc lưu trữ

`STANDARD` phải khớp cùng cặp CategoryID + BrandID trong ZoneStorageRule; BrandID NULL là wildcard trong schema. `RESERVE` không bị thu hẹp bởi các dòng rule đã seed ở Zone A. Danh sách hợp lệ xếp STANDARD trước RESERVE; nhân viên vẫn được chủ động chọn RESERVE khi STANDARD còn chỗ. Không hardcode ProductID hoặc LocationID.

Chỉ đặt tại FLOOR_SLOT hoặc TIER có đủ cây đúng loại, parent cùng kho và ACTIVE. FLOOR_SLOT dùng policy của ô; TIER dùng policy của RACK. GOOD chỉ vào GOOD; DAMAGED chỉ vào DAMAGED. DAMAGED được nhận chính thức và đang quarantine được đặt vào ô DAMAGED, giữ nguyên quarantine. Không thay Product, condition, StockState, current warehouse/source, nguồn nhận gốc, SupplySource hay PO/Inbound quantity.

Profile dài/rộng/cao tính bằng cm, trọng lượng bằng kg. Mỗi kiện phải lọt theo hướng đóng gói cố định, không tự xoay. FLOOR_SLOT dùng footprint m² và tổng kg, không xếp chồng; TIER dùng volume m³ và tổng kg. Sức chứa được suy ra từ SerialItem IN_WAREHOUSE đang chiếm vị trí, không lưu bộ đếm capacity riêng.

Nhiều serial được phép cùng FLOOR_SLOT khi có thể chứng minh bố trí 2D bảo thủ, cùng condition và đủ tải. Thuật toán có thể từ chối một cách xếp thực tế vẫn khả thi khi không tìm được phương án đã chứng minh. Không có tọa độ x/y lưu trong schema; nhân viên phải đối chiếu bố trí thực tế với không gian mô phỏng, không coi tổng diện tích đủ là bảo đảm xếp vừa. Capacity trên giao diện là snapshot và luôn được kiểm lại khi POST.

## Transaction và đồng thời

Inventory cập nhật duy nhất CurrentLocationID của SerialItem, để SQL tự tăng RowVersion; INSERT SerialMovement PUTAWAY và AuditLog `Action=PUTAWAY_COMPLETED`, `EntityName=Putaway` trong cùng transaction. Trước ghi, lấy exclusive `sp_getapplock` có resource `EWMS:Inventory:Location:{WarehouseID}:{LocationID}`, owner Transaction, timeout 10 giây; sau đó mới lấy serial `UPDLOCK,HOLDLOCK`. Thứ tự target trước serial tránh người đợi target giữ sẵn một serial khiến bên thắng không đọc được occupancy. So RowVersion, kiểm trạng thái/kho/vị trí trống và tính lại occupancy sau khóa. Hai request cùng serial chỉ một request được commit. Hai request tranh cùng chỗ phải tính occupancy nối tiếp, không cùng dùng snapshot còn trống.

Ba database triển khai hiện tại cùng SQL Server instance. Transaction ReadCommitted dùng một connection Inventory, SELECT dữ liệu CatalogDB/WarehouseDB authoritative với HOLDLOCK và giữ read locks đến hết commit; occupancy cũng có HOLDLOCK để bảo vệ predicate. Chỉ ghi bảng Inventory. Không dùng DTC, không ghi chéo domain, không thay schema. Các trường hợp deadlock/lock timeout rollback và trả `CONCURRENT_PUTAWAY`; không tự trả thành công. Endpoint đọc Catalog/Warehouse cung cấp contract cho client; kết quả HTTP đọc không thay thế việc revalidate trong transaction ghi. Database name cho SELECT được lấy từ `Putaway:CatalogDatabase`, `Putaway:WarehouseDatabase`, mặc định tên DB hiện hữu, kiểm whitelist ký tự identifier; không lấy tên database từ request.

Module Relocate/Outbound/Transfer/Adjustment tương lai cần tuân theo cùng quy ước khóa khi thay occupancy; phải cập nhật operation guard khi bổ sung PickingClaim hoặc Stocktake. Không suy ra các module chưa tồn tại đã được bảo vệ chỉ từ khóa Putaway.

## Kiểm tra môi trường đã thực hiện

Ngày 01/11/2026, SELECT chỉ đọc trên SQL Server `localhost` bằng Windows Authentication xác nhận cả WarehouseID 1 và 2 là OWNED. Zone A ở mỗi kho có 18 FLOOR_SLOT GOOD và 6 DAMAGED. Agent không chạy script UPDATE dữ liệu DAMAGED.

Schema Inventory hiện có AuditLog, Inbound, InboundItem, SerialItem, SerialMovement và SupplySource; chưa có bảng Stocktake/PickingClaim/Reservation. Điều kiện khóa này có điểm nối mở rộng, chưa thể kiểm một module không tồn tại. Hai kho hiện tại không bị ảnh hưởng bởi chính sách RENTED. Kho thuê đến/hết ngày kết thúc hoặc thiếu ngày thuê bị từ chối và cần chốt chính sách về ngày cuối và hàng đã chờ trước khi hết hạn.

DTO serial có `claimChecksAvailable=false`, `stocktakeChecksAvailable=false`; UI thể hiện thông tin chưa khả dụng thay vì khẳng định đã kiểm tra không có claim/khóa. Repository từ chối thao tác nếu phát hiện các bảng Stocktake/SerialClaim/PickingSession/PickingClaim ở branch mới mà chưa tích hợp validator. Các cờ này không phải quyền bỏ qua khóa.

Catalog hiện có:

| ProductCode | Sản phẩm | CategoryID / BrandID | Profile cm / kg | Zone ưu tiên |
|---|---|---|---|---|
| DEMO-AC-001 | Điều hòa Daikin 1 HP | 9 / 8 | 85 × 35 × 30 / 35 | C |
| DEMO-AC-002 | Điều hòa Panasonic 1.5 HP | 9 / 9 | 95 × 38 × 35 / 42 | C |
| DEMO-TV-001 | Ti vi Samsung 55 inch | 10 / 2 | 138 × 85 × 18 / 20 | B |
| DEMO-FRIDGE-001 | Tủ lạnh Panasonic 300 lít | 11 / 9 | 70 × 72 × 180 / 65 | B |
| DEMO-IND-001 | Bếp từ đôi Electrolux | 12 / 10 | 82 × 52 × 18 / 12 | D |
| DEMO-RICE-001 | Nồi cơm điện Sharp 1.8 lít | 13 / 11 | 38 × 35 × 35 / 4.5 | D |

Các ID trên là dữ liệu kiểm chứng, không phải hằng số trong logic.

## Kiểm chứng SSMS chỉ đọc

Chạy query cấu hình trước test. Nếu môi trường khác chưa có 18 GOOD + 6 DAMAGED mỗi kho, backup và người vận hành tự chạy file `EWMS_ZoneA_DamagedSlots_KhoA_KhoB_SQL2022.sql` được cung cấp trong Downloads; coding agent không tự UPDATE.

```sql
SELECT w.WarehouseID,w.WarehouseName,slot.ConditionPolicy,COUNT(*) AS SlotCount
FROM WarehouseDB.dbo.Warehouse w
JOIN WarehouseDB.dbo.StorageLocation z ON z.WarehouseID=w.WarehouseID
  AND z.LocationType='ZONE' AND z.ParentLocationID IS NULL
  AND z.LocationName=N'A' AND z.ZoneType='RESERVE'
JOIN WarehouseDB.dbo.StorageLocation r ON r.WarehouseID=w.WarehouseID
  AND r.ParentLocationID=z.LocationID AND r.LocationType='ROW' AND r.StorageType='FLOOR'
JOIN WarehouseDB.dbo.StorageLocation slot ON slot.WarehouseID=w.WarehouseID
  AND slot.ParentLocationID=r.LocationID AND slot.LocationType='FLOOR_SLOT'
WHERE w.WarehouseID IN (1,2)
GROUP BY w.WarehouseID,w.WarehouseName,slot.ConditionPolicy
ORDER BY w.WarehouseID,slot.ConditionPolicy;
```

Thay biến Serial bằng thiết bị vừa nhận DONE trong môi trường test. Chạy trước và sau POST, lưu riêng hai result grids để so sánh. Query không sửa dữ liệu và không hardcode LocationID.

```sql
DECLARE @serial nvarchar(100)=N'<Serial-thuc-te>';
DECLARE @serialId bigint,@locationId int,@warehouseId int,@sourceId bigint,@inboundId bigint;
SELECT @serialId=SerialItemID,@locationId=CurrentLocationID,@warehouseId=CurrentWarehouseID,
  @sourceId=CurrentSupplySourceID,@inboundId=OriginalInboundID
FROM InventoryDB.dbo.SerialItem WHERE SerialNumber=@serial;

SELECT SerialItemID,ProductID,SerialNumber,StockState,ItemCondition,IsQuarantined,
  CurrentWarehouseID,CurrentLocationID,CurrentSupplySourceID,OriginalInboundID,
  OriginAdjustmentID,RowVersion
FROM InventoryDB.dbo.SerialItem WHERE SerialItemID=@serialId;

SELECT MovementID,SerialItemID,EventType,PerformedByUserID,OccurredAt,
  FromState,ToState,FromCondition,ToCondition,FromWarehouseID,ToWarehouseID,
  FromLocationID,ToLocationID,FromQuarantined,ToQuarantined,InboundID,Note
FROM InventoryDB.dbo.SerialMovement WHERE SerialItemID=@serialId
ORDER BY OccurredAt,MovementID;

SELECT AuditLogID,UserID,Action,EntityName,EntityID,OldValues,NewValues,Status,CreatedAt
FROM InventoryDB.dbo.AuditLog
WHERE EntityName='Putaway' AND EntityID=CONVERT(nvarchar(100),@serialId)
ORDER BY AuditLogID;

SELECT COUNT_BIG(*) AS OnHandCount
FROM InventoryDB.dbo.SerialItem
WHERE CurrentWarehouseID=@warehouseId AND StockState='IN_WAREHOUSE';

SELECT COUNT_BIG(*) AS OccupantCount,
  SUM(CONVERT(decimal(28,8),p.LengthCm)*p.WidthCm/10000) AS UsedAreaM2,
  SUM(CONVERT(decimal(28,8),p.LengthCm)*p.WidthCm*p.HeightCm/1000000) AS UsedVolumeM3,
  SUM(p.GrossWeightKg) AS UsedWeightKg
FROM InventoryDB.dbo.SerialItem s
JOIN CatalogDB.dbo.Product p ON p.ProductID=s.ProductID
WHERE s.CurrentWarehouseID=@warehouseId AND s.CurrentLocationID=@locationId
  AND s.StockState='IN_WAREHOUSE';

SELECT SupplySourceID,ProductID,WarehouseID,ItemCondition,EligibleQuantity,RemainingQuantity,RowVersion
FROM InventoryDB.dbo.SupplySource WHERE SupplySourceID=@sourceId;

SELECT i.InboundID,i.ReceiptCode,i.Status,i.CompletedAt,i.RowVersion,
  COUNT_BIG(ii.InboundItemID) AS ActiveReceivedItems
FROM InventoryDB.dbo.Inbound i
LEFT JOIN InventoryDB.dbo.InboundItem ii ON ii.InboundID=i.InboundID AND ii.IsActive=1
WHERE i.InboundID=@inboundId
GROUP BY i.InboundID,i.ReceiptCode,i.Status,i.CompletedAt,i.RowVersion;

SELECT po.PurchaseOrderID,po.Status,po.RowVersion,l.POLineID,l.ProductID,l.OrderedQuantity,l.ClosedQuantity,
  (SELECT COUNT_BIG(*) FROM InventoryDB.dbo.InboundItem ii
    JOIN InventoryDB.dbo.Inbound i ON i.InboundID=ii.InboundID
    WHERE ii.POLineID=l.POLineID AND ii.IsActive=1 AND i.Status IN ('DONE','COMPLETED')) AS ReceivedQuantity
FROM InventoryDB.dbo.Inbound i
JOIN ProcurementDB.dbo.PurchaseOrder po ON po.PurchaseOrderID=i.PurchaseOrderID
JOIN ProcurementDB.dbo.PurchaseOrderLine l ON l.PurchaseOrderID=po.PurchaseOrderID
WHERE i.InboundID=@inboundId
ORDER BY l.POLineID;
```

Đúng sau Putaway: CurrentLocationID đổi NULL → target, SerialItem RowVersion đổi; thêm đúng một PUTAWAY movement và audit. StockState/kho/condition/quarantine/nguồn gốc/nguồn hiện tại không đổi. OnHandCount, EligibleQuantity, RemainingQuantity, Inbound quantity, PO quantity và các RowVersion nguồn/chứng từ giữ nguyên. Occupancy đích tăng một. Query tổng area và volume trả hai đơn vị riêng; chỉ đối chiếu mode của vị trí, không cộng m² với m³. Tổng diện tích không thay thế kiểm 2D.

## Walkthrough React và Swagger

1. Đóng backend cũ bằng Ctrl+C trong terminal đang chạy nếu cần nạp bản build mới. Giữ server và cách cấu hình hiện có. Với Windows Authentication local: `powershell -File scripts/start-ewms.ps1 -Server localhost -WindowsAuthentication`. Script không tự dừng tiến trình của người dùng.
2. Trong terminal khác: `cd frontend` rồi `npm run dev`. Mở địa chỉ Vite hiển thị, đăng nhập Warehouse Staff có warehouse_id 1. Không dùng Admin để làm nghiệp vụ kho.
3. Qua luồng Inbound sẵn có, tạo/hoàn tất phiếu từ PO đã xác nhận cho kho 1. Nhận ít nhất GOOD và DAMAGED ở dữ liệu test; DAMAGED giữ cờ quarantine. Không dùng DRAFT để thử gán vị trí.
4. Mở `/warehouse/pending-putaway`, tìm bằng Serial hoặc chọn dòng. Có thể bấm **Quét bằng camera** để dùng camera thực, tải ảnh tem hoặc dùng máy quét bàn phím. Cấp quyền camera trên localhost/HTTPS; mã đọc trùng 3 lần sẽ tự điền ô Serial và đóng camera, sau đó bấm **Tra cứu Serial**. Kiểm tên/mã/Category/Brand, ngày nhận, thông số kiện, kho, condition và quarantine.
5. Nhập **Tìm vị trí**, ví dụ `zone C, kệ 3`, hoặc chọn Zone/Dãy/Kệ/Tầng để lọc ngay các vị trí hợp lệ. Từ khóa không phân biệt dấu, hoa thường hoặc `03`/`3`, nhưng không nhầm Kệ 3 với Kệ 13 hay Tầng 3. Các bộ lọc không thay đổi quy tắc hợp lệ hoặc tự đổi vị trí đang chọn. Nếu vị trí cũ bị ẩn, chọn rõ một kết quả hoặc xóa bộ lọc trước khi lưu. STANDARD vẫn được ưu tiên và vẫn chọn được RESERVE khi STANDARD còn chỗ. Kiểm thông tin trên modal rồi bấm **Xác nhận Putaway**, không phải quét/nhập lại Serial.
6. Kiểm danh sách chờ giảm một, feedback có path, reload giữ kết quả. Scan lại cùng serial: thấy vị trí đã đặt; không có movement PUTAWAY thứ hai và không tự relocate.
7. Với DAMAGED đang quarantine, chỉ chọn các ô DAMAGED trong A/Dãy 04. Sau thành công kiểm IsQuarantined vẫn 1; không có thao tác mở khóa để bán/xuất.
8. Lặp cùng các trường hợp với Warehouse Staff warehouse_id 2; serial kho 1 không xuất hiện và không được đặt vào kho 2. Admin/Purchasing POST phải 403; staff chưa gán kho phải 403.
9. Mở Swagger Inventory tại `http://localhost:5159/swagger` sau đăng nhập cùng host localhost để cookie dùng chung theo cơ chế hiện có. GET RowVersion/gợi ý, lấy CSRF token từ `/api/auth/csrf`, POST bằng header. Nếu Swagger không cho nhập custom header, dùng Network/console của phiên React hoặc công cụ HTTP giữ cookie thay vì bỏ kiểm CSRF.
10. Hai tab tải cùng Serial/RowVersion rồi POST: một commit, request còn lại 409. Hai serial tranh chỗ gần đầy: chỉ số serial thật sự vừa chỗ được commit. Giữ chọn lựa khi lỗi, tải lại gợi ý; không retry bằng cách đổi SerialItemID tùy ý.
11. Dùng SSMS SELECT trước/sau để kiểm source/PO/Inbound/on hand không đổi, movement/audit đủ actor/trạng thái và capacity đúng. Lỗi rollback movement/audit được kiểm tự động trong database test tách biệt, không gây lỗi có chủ ý trên production.

## Kiểm thử endpoint đọc Catalog và Warehouse

Các lệnh thực tế đã chạy thành công:

```powershell
dotnet build Catalog.API/Catalog.API.csproj -c Release --no-restore
dotnet build Warehouse.API/Warehouse.API.csproj -c Release --no-restore
$env:EWMS_PUTAWAY_PROFILE_TEST_SERVER = 'localhost'
dotnet run --project tests/Catalog.Products.Tests/Catalog.Products.Tests.csproj -c Release --no-restore -p:PutawayProfilesOnly=true
Remove-Item Env:EWMS_PUTAWAY_PROFILE_TEST_SERVER
```

Hai build trả 0 warning/error. Test profile thực thi 401 khi anonymous, 403 cho Admin/Purchasing và staff chưa được phân kho, đọc profile thực từ CatalogDB và layout thực cho warehouse claim 1/2. Cố gửi `?warehouseId=` của kho đối diện không đổi phạm vi trả về. Toàn bộ truy vấn SQL trong test profile là SELECT; không ghi dữ liệu ứng dụng. Không đặt biến môi trường thì chỉ chạy kiểm tra quyền, bỏ phần đọc SQL thực và in SKIP rõ ràng.

Tùy chọn `PutawayProfilesOnly=true` chỉ chọn entrypoint test mới trong project test sẵn có. Suite Catalog đầy đủ trước thay đổi đã tham chiếu lớp `SqlProductTests` không có source; build mặc định suite đó vẫn bị lỗi CS0103 này. Phần Putaway không sửa hoặc giả lập kiểm thử SQL Product đang thiếu. Debug build API cũng có thể bị DLL lock nếu backend cũ đang chạy; build Release kiểm chứng riêng mà không dừng tiến trình người dùng. Các kết quả unit/integration Inventory, build solution và frontend được ghi trong báo cáo bàn giao cuối cùng.

## Giới hạn phạm vi

Relocate UC-028, picking/xuất, Return/TransferReceipt/Adjustment và Stocktake không được tạo mới trong lần triển khai này. Pending được xây từ SerialItem state/kho/location và nguồn Inbound là metadata tùy chọn để có thể hỗ trợ nguồn chính thức khác về sau. Các nguồn chưa có không được báo là đã kiểm thử.

Không tự commit/push. Không chạy schema migration hoặc file UPDATE DAMAGED trên database ứng dụng. Kiểm tra thủ công camera, thiết bị scanner vật lý và bố trí hàng ngoài kho cần người vận hành thực hiện; test code không chứng minh các thao tác vật lý này.

## Kết quả kiểm chứng cuối cùng

Các lệnh dưới đây chạy tại repository trên Windows, ngày 01/11/2026:

```powershell
dotnet build WarehouseManagementSystem_SWD392.slnx -c Release --no-restore --nologo -m:1 -p:UseSharedCompilation=false
$env:EWMS_INBOUND_TEST_SERVER='localhost'
$env:EWMS_PUTAWAY_TEST_SERVER='localhost'
dotnet tests/Inventory.Inbound.Tests/bin/Release/net8.0/Inventory.Inbound.Tests.dll
$env:EWMS_PUTAWAY_PROFILE_TEST_SERVER='localhost'
dotnet run --project tests/Catalog.Products.Tests -c Release --no-restore -p:PutawayProfilesOnly=true -p:UseSharedCompilation=false
```

| Phần | Kết quả thực tế |
|---|---|
| Solution Release build | PASS, 0 warning, 0 error |
| Putaway rules | PASS, 125 assertions |
| Putaway API | PASS, role/kho/CSRF/query/Serial ký tự đặc biệt/giả actor/409 |
| SQL Inbound hiện hữu | PASS, migration fixture hai lần, nhận nhiều đợt, concurrency, rollback, quarantine và audit |
| SQL Putaway | PASS, 102 assertions, hai kho/sáu profiles, STANDARD→RESERVE, sàn nhiều Serial, thể tích tầng/tải trọng, đồng thời và rollback |
| Catalog/Warehouse profile-only | PASS, quyền và SELECT thật theo warehouse claim 1/2 |
| Identity.Authentication.Tests | PASS, fake repository |
| Procurement.Excel.Tests | PASS, 15 scenarios, workbook trong bộ nhớ |
| Procurement.Tests | PASS, SQL riêng của Procurement SKIP vì chưa đặt biến môi trường; không coi là SQL đã chạy |
| WMS.Audit.Gateway.Tests | PASS, fake HTTP upstream |
| WMS.Audit.Source.Tests | PASS |
| Frontend build | PASS, TypeScript + Vite |
| Frontend suite | PASS, 74/74 tests trong 10 files sau cập nhật bộ lọc/camera và bỏ quét Serial lần hai |
| Chrome desktop/mobile | PASS, fixture API, kiểm bố cục, modal, scan sai, 409; không tràn ngang/lỗi runtime |

Kiểm tra trình duyệt sau cập nhật: desktop 1440px và mobile 390px nhập `zone C, kệ 3` lọc đúng 3/32 vị trí. Chrome thực thi `getUserMedia` và bộ giải mã ZXing thật trên luồng camera giả lập bằng video QR cục bộ: mã `SN-AbC-99` được điền sau khi đọc ổn định, track kết thúc và không tự POST. Đóng camera thủ công cũng giải phóng track. Modal xác nhận không có ô nhập/quét Serial; bấm xác nhận gửi đúng một POST. Không có lỗi runtime hoặc tràn ngang. Đây là kiểm tra luồng camera mô phỏng, chưa kiểm chứng phần cứng camera của người vận hành. Ảnh và báo cáo lưu ở `C:/Users/OS/.codex/visualizations/2026/11/01/01a19788-2691-7481-88e9-b1498c072e00/putaway-filters-camera`.

Các suite .NET hồi quy khác được chạy bằng `dotnet tests/<ten-project>/bin/Release/net8.0/<ten-project>.dll` sau solution build. SQL Inbound và Putaway chỉ ghi các database fixture có tên GUID được tạo/xóa riêng; không ghi InventoryDB/WarehouseDB/CatalogDB/ProcurementDB ứng dụng. Profile test chỉ SELECT dữ liệu thật. Query SSMS trong tài liệu đã được chạy kiểm cú pháp bằng SELECT/DECLARE.

Frontend chạy trong thư mục `frontend`:

```powershell
npm --ignore-scripts run build
node node_modules/vitest/vitest.mjs run
```

Build bỏ lifecycle `prepare:ocr` để giữ nguyên sáu file OCR đã có thay đổi của người dùng; vẫn thực thi đầy đủ `tsc --noEmit && vite build`. Không thêm NuGet/npm dependency, project/service hoặc schema ứng dụng. Bộ kiểm tra Chrome dùng mock API nên không thay thế kiểm tra scanner vật lý hoặc duyệt hàng tại kho thật.

Ảnh QA và script có tại `C:/Users/OS/.codex/visualizations/2026/11/01/01a19788-2691-7481-88e9-b1498c072e00/putaway-ui-check`. Các backend Debug đã mở sẵn không bị dừng hoặc thay thế; cần khởi động lại theo cách hiện có để nạp DLL mới trước khi thử bằng React/Swagger.
