# Tạo PO nháp và import Excel

## Phạm vi

Chức năng dành cho `PURCHASING_STAFF`. Admin và nhân viên kho không truy cập API tạo PO. Giao diện dùng màu nền, font Inter, viền mảnh và điểm nhấn xanh theo DESIGN-notion.md.

Đã có hai nhà cung cấp demo và sáu sản phẩm trong CatalogDB. Giữ nguyên Kho A, Kho B và tài khoản hiện có. Chưa tạo PO thật trong quá trình seed/kiểm thử. Kích thước, trọng lượng và model demo chỉ phục vụ thử quy trình.

## Khởi chạy trên máy hiện tại

1. Dừng backend cũ trong terminal đang chạy `start-identity.ps1` bằng Ctrl+C để giải phóng port 5140.
2. Mở PowerShell tại thư mục chứa solution và chạy:

```powershell
.\scripts\start-ewms.ps1
```

Lệnh build và khởi chạy sáu service, gồm Inventory và WMS.API làm cửa vào API chung cho frontend; chỉ cần nhập SQL username/password một lần. Server mặc định MIDUKONGU, SQL Authentication. Windows Authentication có thể dùng `-WindowsAuthentication`. Không cần gửi hoặc lưu mật khẩu vào source.

Giữ terminal mở. Ctrl+C dừng các tiến trình backend do lệnh này tạo. Nếu port đã được dùng, lệnh sẽ báo lỗi thay vì tự dừng tiến trình khác. Log nằm trong `.local/logs/`, không được đưa vào Git.

3. Dừng và chạy lại Vite để nhận cấu hình proxy mới:

```powershell
cd frontend
npm.cmd run dev
```

4. Mở địa chỉ Local trong terminal, đăng nhập bằng tài khoản nhân viên mua hàng. Chọn **Đơn mua hàng** ở thanh bên.

## Tạo thủ công

Chọn Tạo PO, nhà cung cấp, Kho A hoặc Kho B, ngày dự kiến giao (có thể để trống) và ghi chú. Chọn một hoặc nhiều sản phẩm của nhà cung cấp, nhập số lượng nguyên lớn hơn 0. Không thêm một sản phẩm hai lần trong cùng đơn. Ngày dự kiến không được trước ngày tạo đơn theo giờ Việt Nam, ghi chú tối đa 1000 ký tự, mỗi đơn tối đa 500 dòng.

Lưu bản nháp chuyển đến chi tiết PO với mã tự sinh và trạng thái Draft. Backend lấy người tạo từ phiên đăng nhập. Không có trường giá vì schema hiện tại chưa có giá/đơn giá.

Nếu mạng lỗi khi lưu thủ công, kiểm tra danh sách PO trước khi bấm tạo lại; có thể SQL đã commit trước khi trình duyệt nhận được phản hồi.

## Import Excel

Chọn Import Excel và tải mẫu. Các sheet Suppliers, Warehouses, Products chứa ID/mã đang hợp lệ ở thời điểm tải. Nhập trên sheet PurchaseOrder, mỗi file là một PO:

| SupplierID | WarehouseID | ExpectedArrivalDate | Note | ProductCode | Quantity |
|---|---|---|---|---|---|
| 1 | 2 | để trống | Đơn thử nhập Excel | DEMO-AC-001 | 10 |
| 1 | 2 | để trống | Đơn thử nhập Excel | DEMO-RICE-001 | 20 |

Tra ID trong file mẫu thay vì giả định ID luôn là 1 hoặc 2 ở môi trường khác. Nếu điền ngày, dùng yyyy-MM-dd hoặc ô ngày Excel. Lặp lại cùng SupplierID, WarehouseID, ngày và ghi chú ở tất cả các dòng.

Upload, xem trước từng dòng và ghi chú. Nếu có lỗi, sửa Excel rồi upload lại. Chưa có dữ liệu nào được ghi lúc xem trước. Xác nhận import mới tạo PO Draft và Audit Log. Backend kiểm tra lại dữ liệu tham chiếu trước khi lưu. Bấm lại cùng mã xác nhận không tạo thêm PO.

Giới hạn: `.xlsx`, 2 MB, 500 dòng dữ liệu; bỏ qua dòng hoàn toàn trống, báo lỗi dòng điền dở, sản phẩm trùng, sản phẩm không thuộc nhà cung cấp, ID/mã sai, số lượng không hợp lệ hoặc header khác nhau. Không hỗ trợ công thức, macro và external links. Mẫu có hiệu lực dữ liệu tại lúc tải; bản xem trước có thời hạn 15 phút và thuộc tài khoản upload. Khởi động lại Procurement.API sẽ làm hết hiệu lực các bản xem trước chưa xác nhận; upload lại file để tiếp tục.

## Dữ liệu mẫu

Để bổ sung lại ở database khác, chạy (idempotent, không xóa/ghi đè dữ liệu cũ):

```powershell
.\scripts\seed-po-catalog.ps1
```

Đã có script SQL tương ứng tại `scripts/sql/seed-po-catalog.sql`. Không chạy lại file tạo schema gốc nếu bảng đã tồn tại.

## Cấu trúc service

| Service | Port | Trách nhiệm |
|---|---|---|
| WMS.API | 5062 | API Service nhận yêu cầu frontend, chuyển tiếp đến service nghiệp vụ và tổng hợp audit |
| Identity.API | 5140 | Phiên, quyền, CSRF, tên người tạo |
| Catalog.API | 5254 | Nhà cung cấp, sản phẩm, quan hệ SupplierProduct từ CatalogDB |
| Warehouse.API | 5104 | Kho ACTIVE từ WarehouseDB |
| Procurement.API | 5136 | PO, dòng PO, template/preview/confirm, AuditLog trong ProcurementDB |

Frontend gọi cùng origin qua Vite proxy `/api` đến WMS.API. WMS chuyển `/api/purchase-orders` tới Procurement.API, bao gồm tạo/sửa PO và upload/download Excel. Business APIs kiểm tra cookie với Identity, không tin user ID/role từ frontend. POST xác thực CSRF tại Identity. Không đọc chéo database từ Procurement.API; các tham chiếu được kiểm tra qua API Catalog/Warehouse. Xem [API Service](api-service.md) về định tuyến, cookie và cấu hình triển khai.

Các biến cấu hình khi chạy từng service riêng: `ConnectionStrings__Identity`, `ConnectionStrings__Catalog`, `ConnectionStrings__Warehouse`, `ConnectionStrings__Procurement`. Có thể đổi địa chỉ upstream bằng `Services__Identity`, `Services__Catalog`, `Services__Warehouse`. Production cần HTTPS, reverse proxy cùng origin và cấu hình kết nối/chứng chỉ phù hợp; Vite proxy chỉ dùng để phát triển.

Header, toàn bộ dòng PO và AuditLog CREATE_PO/IMPORT_PO_EXCEL được commit trong cùng SQL transaction. Audit có người tạo, thời gian, ID/mã PO và dữ liệu mới. Lỗi log sẽ rollback cả PO. Mã import còn được lưu trong AuditLog để chống tạo trùng khi retry sau commit.

Khi bản ghi danh mục đã ngừng hoạt động, chi tiết PO lịch sử hiện ID dự phòng thay vì tên vì lookup chỉ trả dữ liệu ACTIVE. Các chức năng chỉnh sửa/xác nhận PO, nhận hàng và trang quản lý danh mục nằm ngoài bước này.

## Kiểm thử

```powershell
dotnet run --project tests/Identity.Authentication.Tests
dotnet run --project tests/Procurement.Excel.Tests
dotnet run --project tests/Procurement.Tests
cd frontend
npm.cmd run build
```

Kiểm thử SQL transaction tùy chọn dùng database tạm riêng, tạo rồi xóa đúng database kiểm thử tự sinh, không sửa dữ liệu nghiệp vụ:

```powershell
$env:EWMS_PROCUREMENT_TEST_SERVER = 'MIDUKONGU'
dotnet run --project tests/Procurement.Tests
Remove-Item Env:EWMS_PROCUREMENT_TEST_SERVER
```

Cần Windows Authentication có quyền tạo database tạm để chạy bài SQL này. Đã kiểm tra rollback khi AuditLog lỗi, PO Draft + dòng + audit được lưu đủ, retry import không sinh bản ghi trùng.
