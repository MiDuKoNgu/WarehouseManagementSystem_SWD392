# Class Specifications — Model gốc dùng chung

## Phạm vi và cách đọc

Tài liệu đi kèm [class diagram model gốc](wms-domain-class-diagrams.drawio) và [12 sequence diagrams](wms-sequence-study-12.drawio). Mỗi model được đặc tả một lần và được các chức năng tham chiếu lại.

Đây là **đặc tả model nghiệp vụ từ dữ liệu/schema và SQL đang dùng**, không khẳng định dự án có class C# mang đúng từng tên như `User`, `PurchaseOrder`, `Inbound`. Code hiện dùng nhiều record/DTO như `AuthUser`, `StoredPurchaseOrder`, `ReceiptHeader`. Chỉ liệt kê thuộc tính liên quan phạm vi chức năng; không khẳng định đây là toàn bộ DDL production.

- `?`: cho phép null. `RowVersion: byte[]` là dữ liệu concurrency gốc; DTO HTTP thường mã hóa Base64 thành `string`.
- Không thêm enum nghiệp vụ: code đang dùng string/hằng chuỗi.
- Không gán phương thức nghiệp vụ tưởng tượng cho model. Dòng **Xử lý trong code** chỉ ra class/service/repository thật chứa thao tác.
- Các phương thức tự sinh của C# record như `Equals`, `ToString` không phải nghiệp vụ và không cần đưa vào bản model đơn giản.

## Danh mục dùng lại

| Nhóm chức năng | Class diagram dùng một lần | Sequence tham chiếu |
|---|---|---|
| Authentication | Trang 01 - Authentication | SD03; SD01–SD02 là tương tác bảo mật dùng chung |
| Purchase Order | Trang 02 - Purchase Order | SD04, SD05, SD06 |
| Inbound | Trang 03 - Inbound | SD07, SD08, SD09 |
| Putaway | Trang 04 - Putaway | SD10, SD11 |
| Xem audit | Trang 05 - View Audit Log | SD12 |

## CS01 — Warehouse

- **Trách nhiệm:** Kho được gán cho nhân viên, nhận hàng và chứa vị trí.
- **Nơi sở hữu dữ liệu:** WarehouseDB.
- **Định danh:** WarehouseID.
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `WarehouseID` | `int - WarehouseName: string - WarehouseType: string - Address: string - RentalStartDate: DateTime? - RentalEndDate: DateTime? - Status: string - RowVersion: byte[]` | Không theo phần model đang mô tả |

**Quan hệ và ràng buộc:** Một kho có nhiều vị trí/PO/serial. Quy tắc putaway kiểm trạng thái và hạn thuê kho RENTED. WarehouseID nullable trên User và SerialItem, nhưng nhân viên kho/serial đang xử lý phải thuộc kho hợp lệ.

**Xử lý trong code:** `CurrentSchemaPutawayGuard.ValidateAsync`.

**Nguồn đối chiếu:** [Warehouse.Models/Lookups/PutawayLayout.cs](../Warehouse.Models/Lookups/PutawayLayout.cs), [Warehouse.DAL/Repositories/PutawayLayoutRepository.cs](../Warehouse.DAL/Repositories/PutawayLayoutRepository.cs).

## CS02 — Role

- **Trách nhiệm:** Nhóm quyền của tài khoản.
- **Nơi sở hữu dữ liệu:** IdentityDB.
- **Định danh:** RoleID.
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `RoleID` | `int - RoleName: string` | Không theo phần model đang mô tả |

**Quan hệ và ràng buộc:** Một Role có 0..* User. Login hiện hỗ trợ ADMIN, PURCHASING_STAFF, WAREHOUSE_STAFF. UserRoles là static class chứa const string, không phải enum.

**Xử lý trong code:** `UserRoles.IsSupported`.

**Nguồn đối chiếu:** [Identity.Models/Authentication/AuthUser.cs](../Identity.Models/Authentication/AuthUser.cs), [Identity.DAL/Repositories/UserRepository.cs](../Identity.DAL/Repositories/UserRepository.cs).

## CS03 — User

- **Trách nhiệm:** Tài khoản và danh tính người thao tác.
- **Nơi sở hữu dữ liệu:** IdentityDB.
- **Định danh:** UserID.
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `UserID` | `int - RoleID: int - WarehouseID: int? - Username: string - PasswordHash: string - FullName: string - Status: string - CreatedAt: DateTime - RowVersion: byte[]` | Không theo phần model đang mô tả |

**Quan hệ và ràng buộc:** Mỗi User có một Role; có thể chưa được gán Warehouse. Đăng nhập phải ACTIVE, role được hỗ trợ; WAREHOUSE_STAFF cần WarehouseID > 0. PasswordHash là hash, không phải mật khẩu rõ.

**Xử lý trong code:** `LoginService.LoginAsync / AccessError; PasswordService.Verify`.

**Nguồn đối chiếu:** [Identity.Models/Authentication/AuthUser.cs](../Identity.Models/Authentication/AuthUser.cs), [Identity.DAL/Repositories/UserRepository.cs](../Identity.DAL/Repositories/UserRepository.cs), [Identity.Seed/Program.cs](../Identity.Seed/Program.cs).

## CS04 — Supplier

- **Trách nhiệm:** Nhà cung cấp của PO.
- **Nơi sở hữu dữ liệu:** CatalogDB.
- **Định danh:** SupplierID.
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `SupplierID` | `int - SupplierName: string - Address: string - Email: string - Status: string` | Không theo phần model đang mô tả |

**Quan hệ và ràng buộc:** Một Supplier có 0..* PO; một PO chọn đúng một Supplier. Supplier phải hoạt động; từng Product đặt mua phải thuộc supplier đã chọn.

**Xử lý trong code:** `PurchaseOrderService.Validate`.

**Nguồn đối chiếu:** [Catalog.DAL/Repositories/CatalogRepository.cs](../Catalog.DAL/Repositories/CatalogRepository.cs), [scripts/sql/seed-po-catalog.sql](../scripts/sql/seed-po-catalog.sql).

## CS05 — PurchaseOrder

- **Trách nhiệm:** Thông tin đầu đơn mua hàng.
- **Nơi sở hữu dữ liệu:** ProcurementDB.
- **Định danh:** PurchaseOrderID.
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `PurchaseOrderID` | `long - SupplierID: int - WarehouseID: int - CreatedByUserID: int - POCode: string - OrderDate: DateOnly - ExpectedArrivalDate: DateOnly? - Status: string - Note: string? - CreatedAt: DateTime - ConfirmedAt: DateTime? - ClosedAt: DateTime? - CloseReason: string? - RowVersion: byte[]` | Không theo phần model đang mô tả |

**Quan hệ và ràng buộc:** PO hợp lệ có 1..500 dòng, không trùng product. CREATE/IMPORT tạo DRAFT. CONFIRM: DRAFT → CONFIRMED. CANCEL: DRAFT/CONFIRMED, chưa có inbound và không có reservation. CLOSE: CONFIRMED, không có inbound/reservation mở. RowVersion chống ghi đè.

**Xử lý trong code:** `PurchaseOrderService.CreateAsync / UpdateAsync / ConfirmOrderAsync / CancelAsync / CloseAsync`.

**Nguồn đối chiếu:** [Procurement.Models/PurchaseOrders/PurchaseOrderContracts.cs](../Procurement.Models/PurchaseOrders/PurchaseOrderContracts.cs), [Procurement.DAL/PurchaseOrders/PurchaseOrderRepository.cs](../Procurement.DAL/PurchaseOrders/PurchaseOrderRepository.cs).

## CS06 — PurchaseOrderLine

- **Trách nhiệm:** Một sản phẩm và số lượng đặt trong PO.
- **Nơi sở hữu dữ liệu:** ProcurementDB.
- **Định danh:** POLineID.
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `POLineID` | `long - PurchaseOrderID: long - ProductID: int - OrderedQuantity: int - ClosedQuantity: int` | Không theo phần model đang mô tả |

**Quan hệ và ràng buộc:** Thuộc đúng một PO và một Product. OrderedQuantity > 0. ClosedQuantity là lượng đóng không nhận thêm. Khi CLOSE: closed = ordered − received; received được đọc từ Inventory, không phải cột của POLine.

**Xử lý trong code:** `PurchaseOrderService.Validate / CloseAsync; repository.TransitionAsync`.

**Nguồn đối chiếu:** [Procurement.DAL/PurchaseOrders/PurchaseOrderRepository.cs](../Procurement.DAL/PurchaseOrders/PurchaseOrderRepository.cs).

## CS07 — Product

- **Trách nhiệm:** Sản phẩm được đặt mua và được quản lý theo serial.
- **Nơi sở hữu dữ liệu:** CatalogDB.
- **Định danh:** ProductID.
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `ProductID` | `int - CategoryID: int - BrandID: int - ProductCode: string - ProductName: string - Unit: string - LengthCm: decimal - WidthCm: decimal - HeightCm: decimal - GrossWeightKg: decimal - Status: string` | Không theo phần model đang mô tả |

**Quan hệ và ràng buộc:** Một Product có nhiều POLine/SerialItem. Product có Category/Brand và profile kích thước, trọng lượng để đánh giá putaway.

**Xử lý trong code:** `PurchaseOrderService.Validate; PutawayRules.Suggest / Evaluate`.

**Nguồn đối chiếu:** [Catalog.Models/Products/ProductContracts.cs](../Catalog.Models/Products/ProductContracts.cs), [Inventory.DAL/Putaway/PutawayRepository.cs](../Inventory.DAL/Putaway/PutawayRepository.cs).

## CS08 — Inbound

- **Trách nhiệm:** Phiếu tiếp nhận từ một PO đã xác nhận.
- **Nơi sở hữu dữ liệu:** InventoryDB.
- **Định danh:** InboundID.
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `InboundID` | `long - PurchaseOrderID: long - WarehouseID: int? - CreatedByUserID: int - PerformedByUserID: int? - ReceiptCode: string - ReceiptDate: DateOnly - Status: string - CreatedAt: DateTime - CompletedAt: DateTime? - Note: string? - CreationToken: Guid? - RowVersion: byte[]` | Không theo phần model đang mô tả |

**Quan hệ và ràng buộc:** DRAFT có thể chưa có item; COMPLETE yêu cầu ít nhất một item hợp lệ. DONE tạo hàng chính thức; CANCELLED hủy hiệu lực item. CreationToken chống tạo trùng. WarehouseID nullable trong migration phục vụ dữ liệu cũ; phiếu mới phải đúng kho Actor.

**Xử lý trong code:** `InboundRepository.CreateAsync / CompleteAsync / CancelAsync`.

**Nguồn đối chiếu:** [Inventory.Models/Inbound/InboundContracts.cs](../Inventory.Models/Inbound/InboundContracts.cs), [Inventory.DAL/Inbound/InboundRepository.cs](../Inventory.DAL/Inbound/InboundRepository.cs), [scripts/sql/migrate-inbound.sql](../scripts/sql/migrate-inbound.sql).

## CS09 — InboundItem

- **Trách nhiệm:** Serial được quét/nhập trong phiếu nhập.
- **Nơi sở hữu dữ liệu:** InventoryDB.
- **Định danh:** InboundItemID.
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `InboundItemID` | `long - InboundID: long - POLineID: long - ScannedSerial: string - SerialItemID: long? - ManufacturingMonth: int? - ManufacturingYear: int? - ReceivedCondition: string? - ConditionNote: string? - ScannedAt: DateTime - IsActive: bool - InputMethod: string - ScannedByUserID: int?` | Có |

**Quan hệ và ràng buộc:** Thuộc một Inbound và POLine. Serial không trùng serial thật hoặc item hoạt động khác. Trước complete, SerialItemID thường null; sau complete liên kết một SerialItem. Remove đặt IsActive=false, không xóa vật lý.

**Xử lý trong code:** `InboundRules.Validate; InboundRepository.SaveItemAsync / RemoveItemAsync`.

**Nguồn đối chiếu:** [Inventory.Models/Inbound/InboundContracts.cs](../Inventory.Models/Inbound/InboundContracts.cs), [Inventory.DAL/Inbound/InboundRepository.cs](../Inventory.DAL/Inbound/InboundRepository.cs).

## CS10 — SerialItem

- **Trách nhiệm:** Một đơn vị hàng có serial duy nhất.
- **Nơi sở hữu dữ liệu:** InventoryDB.
- **Định danh:** SerialItemID.
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `SerialItemID` | `long - ProductID: int - SerialNumber: string - OriginalInboundID: long? - CurrentWarehouseID: int? - CurrentLocationID: int? - CurrentSupplySourceID: long? - OriginType: string - StockState: string - ItemCondition: string - IsQuarantined: bool - RegisteredAt: DateTime - RowVersion: byte[]` | Không theo phần model đang mô tả |

**Quan hệ và ràng buộc:** Thuộc một Product; serial nguồn PURCHASE cần Inbound DONE trước putaway. IN_WAREHOUSE và CurrentLocationID=null biểu diễn chờ putaway. Putaway chỉ đổi vị trí, không tạo thêm serial hoặc tăng tồn lần nữa.

**Xử lý trong code:** `PutawayRules.ValidateSerial; PutawayRepository.ConfirmAsync`.

**Nguồn đối chiếu:** [tests/Inventory.Inbound.Tests/Schema.sql](../tests/Inventory.Inbound.Tests/Schema.sql), [Inventory.DAL/Inbound/InboundRepository.cs](../Inventory.DAL/Inbound/InboundRepository.cs), [Inventory.DAL/Putaway/PutawayRepository.cs](../Inventory.DAL/Putaway/PutawayRepository.cs).

## CS11 — SerialMovement

- **Trách nhiệm:** Lịch sử thay đổi trạng thái/vị trí của serial.
- **Nơi sở hữu dữ liệu:** InventoryDB.
- **Định danh:** MovementID.
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `MovementID` | `long - SerialItemID: long - PerformedByUserID: int - EventType: string - OccurredAt: DateTime - FromLocationID: int? - ToLocationID: int? - FromState: string? - ToState: string - FromWarehouseID: int? - ToWarehouseID: int? - Note: string?` | Có |

**Quan hệ và ràng buộc:** Thuộc một SerialItem và một User thực hiện. Inbound complete tạo PURCHASE_RECEIPT; putaway tạo PUTAWAY. Movement được ghi cùng transaction với thay đổi serial và audit.

**Xử lý trong code:** `InboundRepository.CompleteAsync; PutawayRepository.ConfirmAsync`.

**Nguồn đối chiếu:** [tests/Inventory.Inbound.Tests/Schema.sql](../tests/Inventory.Inbound.Tests/Schema.sql), [Inventory.DAL/Putaway/PutawayRepository.cs](../Inventory.DAL/Putaway/PutawayRepository.cs).

## CS12 — StorageLocation

- **Trách nhiệm:** Vị trí trong cây cấu trúc kho.
- **Nơi sở hữu dữ liệu:** WarehouseDB.
- **Định danh:** LocationID.
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `LocationID` | `int - WarehouseID: int - ParentLocationID: int? - LocationName: string - LocationType: string - StorageType: string? - CapacityMode: string? - ConditionPolicy: string? - ZoneType: string? - LengthCm: decimal? - WidthCm: decimal? - HeightCm: decimal? - MaxWeightKg: decimal? - Status: string - RowVersion: byte[]` | Không theo phần model đang mô tả |

**Quan hệ và ràng buộc:** Thuộc một Warehouse; ParentLocationID tạo quan hệ cha 0..1 – con 0..*. Một vị trí có thể chứa nhiều serial tùy sức chứa/rule. Đánh giá type, condition, category/brand, kích thước và tải trọng.

**Xử lý trong code:** `PutawayRules.Evaluate / Suggest`.

**Nguồn đối chiếu:** [Warehouse.Models/Lookups/PutawayLayout.cs](../Warehouse.Models/Lookups/PutawayLayout.cs), [Inventory.DAL/Putaway/PutawayRepository.cs](../Inventory.DAL/Putaway/PutawayRepository.cs).

## CS13 — ZoneStorageRule

- **Trách nhiệm:** Quy tắc loại hàng/hãng cho một zone.
- **Nơi sở hữu dữ liệu:** WarehouseDB.
- **Định danh:** Các cột định danh liên quan: ZoneLocationID, CategoryID, BrandID; sơ đồ không khẳng định khóa SQL khi chưa có DDL đầy đủ..
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `ZoneLocationID` | `int - CategoryID: int - BrandID: int?` | Có |

**Quan hệ và ràng buộc:** Một rule tham chiếu một zone và một Category; BrandID nullable cho rule không giới hạn một hãng. Nhiều rule có thể áp dụng trong một zone.

**Xử lý trong code:** `PutawayRules.Evaluate`.

**Nguồn đối chiếu:** [Warehouse.Models/Lookups/PutawayLayout.cs](../Warehouse.Models/Lookups/PutawayLayout.cs), [Inventory.DAL/Putaway/PutawayRepository.cs](../Inventory.DAL/Putaway/PutawayRepository.cs).

## CS14 — Category

- **Trách nhiệm:** Phân loại sản phẩm dùng cho quy tắc lưu trữ.
- **Nơi sở hữu dữ liệu:** CatalogDB.
- **Định danh:** CategoryID.
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `CategoryID` | `int - CategoryName: string` | Không theo phần model đang mô tả |

**Quan hệ và ràng buộc:** Một Category có 0..* Product và có thể xuất hiện trong nhiều ZoneStorageRule.

**Xử lý trong code:** `PutawayRules.Evaluate`.

**Nguồn đối chiếu:** [Catalog.Models/Products/ProductContracts.cs](../Catalog.Models/Products/ProductContracts.cs), [Inventory.DAL/Putaway/PutawayRepository.cs](../Inventory.DAL/Putaway/PutawayRepository.cs).

## CS15 — Brand

- **Trách nhiệm:** Hãng sản phẩm dùng cho danh mục và quy tắc zone.
- **Nơi sở hữu dữ liệu:** CatalogDB.
- **Định danh:** BrandID.
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `BrandID` | `int - BrandName: string - Status: string` | Không theo phần model đang mô tả |

**Quan hệ và ràng buộc:** Một Brand có 0..* Product; ZoneStorageRule có thể không chỉ định Brand.

**Xử lý trong code:** `PurchaseOrderService.Validate; PutawayRules.Evaluate`.

**Nguồn đối chiếu:** [Catalog.Models/Products/ProductContracts.cs](../Catalog.Models/Products/ProductContracts.cs), [scripts/sql/seed-po-catalog.sql](../scripts/sql/seed-po-catalog.sql).

## CS16 — AuditLog

- **Trách nhiệm:** Bản ghi lịch sử thao tác.
- **Nơi sở hữu dữ liệu:** ProcurementDB và InventoryDB (hai nguồn riêng).
- **Định danh:** AuditLogID trong từng nguồn; khi tổng hợp định danh bằng (source, AuditLogID)..
- **Phương thức trong model gốc:** không bổ sung phương thức nghiệp vụ; tham khảo lớp xử lý bên dưới.

| Thuộc tính | Kiểu | Cho phép null |
|---|---|---|
| `AuditLogID` | `long - UserID: int - Action: string - EntityName: string - EntityID: string - OldValues: string? - NewValues: string? - Status: string - CreatedAt: DateTime` | Không theo phần model đang mô tả |

**Quan hệ và ràng buộc:** UserID là người thực hiện, không nhất thiết người đang xem. Người xem phải ADMIN. EntityName/EntityID là tham chiếu logic đa loại, không suy diễn FK trực tiếp. OldValues/NewValues là chuỗi snapshot JSON; sanitize khi trả chi tiết. Source/ActorName/DocumentCode/BusinessGroup không phải thuộc tính gốc.

**Xử lý trong code:** `SqlAuditReader.PageAsync / DetailAsync; AuditGateway.ListAsync / DetailAsync`.

**Nguồn đối chiếu:** [WMS.Audit/SqlAuditReader.cs](../WMS.Audit/SqlAuditReader.cs), [WMS.API/Audit/AuditGateway.cs](../WMS.API/Audit/AuditGateway.cs), [các repository ghi AuditLog](../các repository ghi AuditLog).

## Đối chiếu với các phương thức model/DTO thực sự có trong code

| Kiểu C# | Phương thức tự viết | Vì sao không đặt vào model gốc khác |
|---|---|---|
| `UserProfile` | `From(AuthUser)` | Chuyển DTO trả frontend; không phải `User.login()` |
| `UpdatePurchaseOrder` | `ToCreate()` | Chuyển request; không phải `PurchaseOrder.update()` |
| `AuditQuery` | `Validate()` | Kiểm bộ lọc; không phải `AuditLog.validate()` |
| `AuditCursor` | `Encode()`, `Decode()` | Phân trang; không phải hành vi của một log |

## Tham chiếu và sử dụng trong báo cáo

Trong mỗi mục Detailed Design, đặt đường dẫn/mã trang class diagram, chỉ ra các mục CS liên quan, rồi đặt sequence theo mã SD. Với chức năng biến thể, dẫn lại cấu trúc đã có và ghi rõ phần điều kiện/tham số thay đổi; không sao chép toàn bộ hình. Tham khảo [hướng dẫn phân công và đọc ref](sequence-study-guide.md).

