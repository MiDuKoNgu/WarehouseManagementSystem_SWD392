# Inbound — Phiếu nhập kho

## Phạm vi

Module tiếp nhận theo Serial từ PO `CONFIRMED`, dành cho `WAREHOUSE_STAFF` có kho được phân công. Giữ .NET 8, React/TypeScript, cookie session, CSRF và các database theo miền nghiệp vụ hiện tại. Không tạo Inbound không PO, không cấp Serial nội bộ, không bổ sung quy tắc hạn sử dụng và không triển khai thao tác chọn vị trí Putaway trong đợt này.

Nguồn yêu cầu: `PROMPT_AI_CODING.md.docx`, `EWMS_Business_Requirements_AI_Coding.md` và schema `db.sql` trong Downloads. Nội dung tài liệu được đối chiếu với source trước khi triển khai; chính sách tiếp nhận đã được người dùng xác nhận trực tiếp và thể hiện trong cấu hình.

## Sử dụng

1. Đăng nhập Warehouse Staff và mở **Phiếu nhập kho** ở `/warehouse/inbounds`.
2. Tạo phiếu từ PO đã xác nhận thuộc kho của tài khoản và còn số lượng nhận.
3. Chọn dòng PO, tháng/năm sản xuất; nhập Serial bằng tay, camera hoặc ảnh tem, sau đó chọn tình trạng hàng và ghi chú tình trạng.
4. Camera tự điền Serial sau khi đọc ổn định. Ảnh chụp/tải lên tự điền khi chỉ có một ứng viên; nếu nhiều mã hoặc barcode/OCR khác nhau thì chọn mã phù hợp. Kiểm tra ô Serial, chọn tình trạng hàng và nhập ghi chú rồi bấm thêm để lưu vào database.
5. Trong DRAFT có thể sửa Serial hoặc hủy hiệu lực dòng nhập sai. Tùy chọn giữ tháng/năm giúp nhập tiếp cùng lô; đổi sản phẩm cần kiểm tra lại thông tin.
6. Hoàn tất sau khi kiểm tra danh sách. Màn hình hàng chờ xếp `/warehouse/pending-putaway` hiển thị Serial đã nhận, chưa có vị trí lưu kho.

Nếu phiếu đã DONE/CANCELLED nhưng yêu cầu đồng bộ PO trước đó báo lỗi dịch vụ, mở lại chi tiết và bấm **Đồng bộ trạng thái PO**. Thao tác retry giải phóng reservation theo bằng chứng terminal, không tiếp nhận tồn kho lần nữa.

Danh sách hỗ trợ tìm mã phiếu/PO, lọc trạng thái và ngày. Chi tiết phân biệt lượng đã nhận chính thức, lượng giữ trong DRAFT khác, lượng trong phiếu hiện tại và lượng còn được nhận.

Camera cần HTTPS hoặc localhost, quyền camera trình duyệt và camera thiết bị. `@zxing/browser` ưu tiên camera sau, chỉ đọc vùng khung giữa và cần ba lần đọc trùng trong thời gian ngắn trước khi giữ kết quả. Bấm Quét lại để xóa kết quả camera và ô Serial bên dưới; reset sau khi lưu Serial. Giải phóng camera khi đóng hoặc rời trang. Camera tự nhận diện mọi định dạng thư viện hỗ trợ, gồm Code128/39/93, QR/Data Matrix, EAN/UPC, ITF và Codabar; không cần chọn loại mã.

Ảnh JPEG/PNG/WebP tối đa 10 MB, tối đa 32 megapixel; kiểm tra MIME và chữ ký file. Thử barcode toàn ảnh, vùng cắt chồng lấn và các hướng xoay; OCR tăng kích thước ảnh nhỏ và thử bố cục sparse/block. Parser chỉ lấy chuỗi có nhãn Serial/SN, hỗ trợ giá trị ở dòng kế tiếp, loại ký tự rác như £, không thay O/0 hoặc I/1. Mã barcode và chữ không khớp sẽ cảnh báo; người dùng phải đối chiếu tem, tránh nhầm SKU/EAN/model thành Serial. Không lưu ảnh tem phía server.

Worker, WASM và model tiếng Anh được phục vụ từ `/ocr/` cùng origin. `npm run dev`/`npm run build` tự chạy `scripts/prepare-ocr-assets.mjs` để lấy tài nguyên từ dependency đã cài; điện thoại không cần tải OCR từ CDN ngoài. Cần `npm install` khi lấy source mới, không commit thư mục public/ocr sinh tự động.

## Chính sách đã xác nhận

Chủ dự án đã xác nhận trong phiên làm việc ngày 01/11/2026. Inventory API đọc cấu hình khi khởi động; `appsettings.json` đặt các giá trị:

| Khóa cấu hình | Giá trị và tác dụng |
|---|---|
| `Inbound:AllowMultipleDrafts` | `false`: một phiếu DRAFT đang mở cho mỗi PO |
| `Inbound:AcceptDamaged` | `true`: nhận hàng hỏng vào cách ly |
| `Inbound:AllowCancellation` | `true`: nhân viên được hủy toàn bộ DRAFT, bắt buộc lý do |

Biến môi trường tương ứng là `Inbound__AllowMultipleDrafts`, `Inbound__AcceptDamaged`, `Inbound__AllowCancellation`. Giá trị tổng thể là `false,true,true`. Luồng nhập hàng chỉ có hai tình trạng `GOOD` và `DAMAGED`, không phân mức độ hỏng. Hàng hỏng được nhận với mã `DAMAGED`, bắt buộc mô tả tình trạng; Serial bị cách ly và nguồn hàng có `EligibleQuantity=0`. Backend chỉ chấp nhận `GOOD` hoặc `DAMAGED`, từ chối enum khác. Hủy hiệu lực một Serial nhập sai trong DRAFT luôn được phép; quyền này khác hủy toàn phiếu.

## API

Tất cả endpoint sau yêu cầu Warehouse Staff; backend kiểm tra kho được phân công. Mọi mutation yêu cầu `X-CSRF-TOKEN`, lấy từ `/api/auth/csrf`.

| Phương thức | Đường dẫn | Chức năng |
|---|---|---|
| GET | `/api/inbounds` | Phiếu thuộc kho hiện tại |
| GET | `/api/inbounds/orders` | PO đủ điều kiện tạo phiếu |
| GET | `/api/inbounds/orders/{poId}` | Thông tin PO, tiến độ và chính sách |
| GET | `/api/inbounds/{id}` | `{receipt,order,lines,items,policy}` |
| POST | `/api/inbounds` | `{purchaseOrderId,note,token}`; token GUID giữ nguyên khi retry |
| POST | `/api/inbounds/{id}/items` | Thêm Serial |
| PUT | `/api/inbounds/{id}/items/{itemId}` | Sửa dòng DRAFT |
| DELETE | `/api/inbounds/{id}/items/{itemId}` | Body `{rowVersion}`; đặt `IsActive=0` |
| POST | `/api/inbounds/{id}/complete` | Body `{rowVersion}` |
| POST | `/api/inbounds/{id}/cancel` | Body `{rowVersion,reason}`; theo chính sách |
| GET | `/api/inventory/pending-putaway` | Serial đã nhận, chưa được xếp vị trí |

Payload thêm/sửa Serial:

```json
{
  "poLineId": 1,
  "serial": "Manufacturer-Serial",
  "manufacturingMonth": 10,
  "manufacturingYear": 2026,
  "receivedCondition": "GOOD",
  "conditionNote": null,
  "inputMethod": "MANUAL",
  "rowVersion": "base64-header-rowversion"
}
```

`inputMethod` của yêu cầu mới nhận `MANUAL`, `CAMERA`, `IMAGE_UPLOAD`. Dòng lịch sử không biết phương thức được migration đánh dấu `UNKNOWN`; giao diện hiển thị chưa ghi nhận, không giả định đã nhập tay. Serial được trim, không đổi hoa/thường, tối đa 100 ký tự; kiểm tra trùng theo collation database. Tháng từ 1–12, năm từ 1900 và không ở tương lai theo múi giờ Việt Nam. Mọi phương thức nhập dùng chung backend validation. Lỗi validation 400, sai quyền 403, ngoài phạm vi/không tồn tại 404, xung đột dữ liệu/trạng thái 409. Phiếu DONE trả lại kết quả hoàn tất khi retry, không tạo tồn/movement lần nữa.

Procurement bổ sung `/api/receiving-orders`, `/{id}`, `/{id}/reserve`, `/{id}/release` cho luồng nhận, chỉ đọc PO và giữ/giải phóng quyền tiếp nhận; không cấp quyền sửa PO cho Warehouse Staff. `/api/inventory/receiving-reservations/{token}` cung cấp bằng chứng trạng thái cuối của phiếu cho Procurement.

## Transaction và đồng thời

Inventory mutation dùng `sp_getapplock` độc quyền `EWMS:InboundWrite` trong transaction, kiểm tra RowVersion ở đầu phiếu và tăng phiên bản khi thêm/sửa/xóa dòng. Các kiểm tra trạng thái, trùng Serial, dòng PO và tổng lượng nhận/giữ chỗ được thực hiện lại trong transaction. Unique index active `ScannedSerial` và `SerialItemID` là lớp bảo vệ cuối.

Số lượng còn được nhận = đặt − đóng thiếu − Serial hợp lệ từ DONE/COMPLETED − Serial hoạt động trong DRAFT. Không đếm dòng đã hủy; Serial hoàn tất được đếm distinct. Khi xem phiếu hiện tại, `ReservedQuantity` chỉ tính DRAFT khác, `CurrentQuantity` tính phiếu đang xem; không trừ lại Serial DONE hai lần.

Trước khi tạo Inventory receipt, Procurement ghi `ReceivingReservation` bền vững dưới cùng khóa PO mà thao tác đóng/hủy sử dụng. Tạo và giải phóng reservation tăng PO RowVersion, tránh sử dụng snapshot nhận hàng cũ để đóng thiếu. Chỉ giải phóng sau khi Inventory xác nhận phiếu terminal. Khi kết quả tạo chưa rõ do dịch vụ mất kết nối, reservation được giữ; retry cùng token phục hồi mà không tạo trùng.

Nếu tạo phiếu bị từ chối sau khi đã reserve, `AbortCreationAsync` dùng cùng khóa Inventory để kiểm tra token. DRAFT đang tồn tại được giữ nguyên. Token chưa có phiếu được ghi một phiếu CANCELLED rỗng cùng audit lý do tạo thất bại; retry muộn token đó bị từ chối. Đây là bù trừ kỹ thuật, không mở quyền hủy DRAFT cho nhân viên. Không giải phóng reservation khi chưa có bằng chứng terminal.

Hoàn tất phiếu tạo SupplySource theo Product/Condition, SerialItem, liên kết InboundItem, movement `PURCHASE_RECEIPT`, cập nhật DONE/người hoàn tất/thời gian và AuditLog trong **một InventoryDB transaction**. Lỗi bất kỳ rollback toàn bộ. Không dùng transaction xuyên database. Audit gồm `INBOUND_CREATED`, `INBOUND_SERIAL_ADDED`, `INBOUND_SERIAL_UPDATED`, `INBOUND_SERIAL_REMOVED`, `INBOUND_COMPLETED`, `INBOUND_CANCELLED`; không ghi mỗi khung hình camera/OCR.

## Chờ Putaway và tồn kho

Schema hiện có không có enum `PENDING_PUTAWAY`. Serial mới dùng `StockState=IN_WAREHOUSE`, `CurrentWarehouseID` đúng kho, `CurrentLocationID=NULL`, `CurrentSupplySourceID` trỏ nguồn `INBOUND/PHYSICAL`. NULL location biểu diễn chờ xếp. Tháng/năm sản xuất được giữ trên SerialItem; không tạo ngày sản xuất giả. GOOD có EligibleQuantity bằng số Serial, DAMAGED bằng 0 và cách ly. Luồng chọn vị trí Putaway về sau phải cập nhật SerialItem đã tồn tại, ghi movement riêng; không tạo lại Serial hoặc cộng lại SupplySource.

## Migration và chạy ứng dụng

Chạy trước khi khởi động phiên bản API mới:

```powershell
.\scripts\migrate-inbound.ps1 -Server MIDUKONGU
```

Script dùng Windows Authentication, chạy `scripts/sql/migrate-procurement-receiving.sql` trên ProcurementDB và `scripts/sql/migrate-inbound.sql` trên InventoryDB. Migration thêm ReceivingReservation và metadata nhận hàng, CreationToken unique, InputMethod, người scan; không tạo bảng Inbound/SerialItem trùng. Script chạy lại được; không xóa dữ liệu nghiệp vụ.

Sau migration, script đối chiếu `Inbound.PurchaseOrderID` với PO trong database sở hữu để điền WarehouseID cho phiếu cũ; không giả định tất cả phiếu cùng kho. PO thiếu được cảnh báo và để nguyên. Có thể dùng `-SkipLegacyReconciliation` để chỉ chạy DDL. Phiếu cũ chưa có WarehouseID không hiển thị ở danh sách theo kho; metadata người scan/tên lịch sử chưa biết giữ NULL.

Ngày 01/11/2026 đã chạy script thành công trên ProcurementDB và InventoryDB local `MIDUKONGU`. Output xác nhận cả hai migration đã áp dụng; không có phiếu lịch sử cần reconciliation được báo ra. Lỗi SSPI ở lần kết nối ban đầu đã được khắc phục trong môi trường chạy có quyền phù hợp.

API và frontend tiếp tục dùng `scripts/start-ewms.ps1` và `npm.cmd run dev` trong `frontend`. Vite chuyển toàn bộ `/api` đến WMS.API tại cổng 5062. WMS chuyển `/api/inbounds`, `/api/inventory`, `/api/putaway` tới Inventory API và `/api/receiving-orders` tới Procurement API. Khởi động lại backend và Vite khi chuyển từ cấu hình proxy cũ; xem [API Service](api-service.md).

## Kiểm chứng

```powershell
dotnet build WarehouseManagementSystem_SWD392.slnx --no-restore --nologo -m:1 -p:UseSharedCompilation=false
dotnet run --project tests/Inventory.Inbound.Tests --no-restore
$env:EWMS_INBOUND_TEST_SERVER = 'MIDUKONGU'
dotnet run --project tests/Inventory.Inbound.Tests --no-restore
Remove-Item Env:EWMS_INBOUND_TEST_SERVER
cd frontend
npm.cmd test
npm.cmd run build
```

SQL tests tùy chọn dùng database tạm tự sinh, không ghi dữ liệu kho thực. Cần quyền tạo/xóa database test. Khi không đặt `EWMS_INBOUND_TEST_SERVER`, suite thông báo SKIP SQL.

Kết quả đã xác minh ngày 01/11/2026:

- Solution build thành công, 0 lỗi/cảnh báo.
- Inbound rules/API authorization, CSRF, warehouse scope và service forwarding thành công.
- Inbound SQL integration trên database tạm thực tại MIDUKONGU thành công: migration chạy lại, nhiều đợt nhận, duplicate/race, giữ chỗ số lượng, DONE bất biến, rollback, quarantine, pending stock, audit và abort fence.
- Procurement API/SQL, Identity, PO Excel và audit regression suites thành công.
- Frontend 41/41 tests, TypeScript và production build thành công sau khi cập nhật chính sách đã xác nhận. Đã kiểm tra trình duyệt Chrome thật với 8 ảnh desktop/mobile dùng API mock: không tràn ngang và không có lỗi JavaScript ở lượt kiểm tra đó.

Suite có lỗi nguồn tồn tại từ trước: `tests/Catalog.Products.Tests/ProductTests.cs:65` tham chiếu `SqlProductTests` nhưng file/class đó không có trong bản source ban đầu. Không coi suite này đã pass; lỗi không thuộc thay đổi Inbound. Chưa trực tiếp kiểm thử iPhone thật; OCR/ảnh và camera stream đã thử bằng Chrome với nhãn tổng hợp và bộ giải mã thật. Xem `plans/261101-inbound/reports/verification.md` để đối chiếu kết quả và giới hạn.

## Tham chiếu mã nguồn

- `Inventory.Models/Inbound/InboundContracts.cs`, `Inventory.BLL/Inbound/InboundRules.cs`.
- `Inventory.DAL/Inbound/SqlInboundRepository.cs`, `Inventory.DAL/Receiving/ReceiptQueries.cs`.
- `Inventory.API/Controllers/InboundsController.cs`, `Inventory.API/Clients/ReceivingOrderClient.cs`.
- `Procurement.API/Controllers/ReceivingOrdersController.cs`, `Procurement.DAL/Receiving/SqlReceivingReservationRepository.cs`.
- `frontend/src/features/inbound/`, `tests/Inventory.Inbound.Tests/`.



