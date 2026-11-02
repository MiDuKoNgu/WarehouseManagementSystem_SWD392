# Audit Logs dành cho Admin

## Phạm vi

Admin xem người thực hiện, thời điểm, hành động, chứng từ, trạng thái và dữ liệu trước/sau. Không có nút sửa/xóa hoặc API mutation lịch sử. SerialMovement được quản lý riêng, không được trộn vào Audit Log.

PO được đọc từ ProcurementDB.AuditLog; Inbound/Putaway từ InventoryDB.AuditLog qua API của service sở hữu. WMS.API tổng hợp, không đọc chéo database. Hiện CREATE_PO/IMPORT_PO_EXCEL đã được ghi cùng transaction lưu PO. Các nghiệp vụ chưa triển khai chưa có lịch sử; không chèn log giả vào database thật.

## Khởi chạy

Dừng backend cũ bằng Ctrl+C, chạy tại thư mục solution:

```powershell
.\scripts\start-ewms.ps1
```

Nhập SQL username/password. Lệnh chạy sáu service: Identity 5140, Catalog 5254, Warehouse 5104, Procurement 5136, Inventory 5159, WMS gateway 5062. WMS.API là cửa vào API chung cho frontend và không có database riêng. Giữ terminal mở.

Dừng/chạy lại frontend để nhận proxy mới:

```powershell
cd frontend
npm.cmd run dev
```

Đăng nhập Admin, chọn **Administration → Audit Logs** hoặc mở `/admin/audit-logs`.

Vite chuyển toàn bộ `/api` đến WMS.API. Các route `/api/admin/audit-logs` vẫn do controller cục bộ của WMS xử lý và kiểm tra role `ADMIN`; chúng không bị chuyển tiếp như các route nghiệp vụ khác. Gateway tiếp tục gọi trực tiếp API nguồn của Inventory/Procurement và Identity để tổng hợp dữ liệu. Xem [API Service](api-service.md).

## Lọc và phân trang

Bộ lọc: nhân viên (UserID), nhóm PO/Inbound/Putaway, hành động, mã chứng từ và khoảng ngày. Nhân viên lấy thông tin hiển thị từ Identity, bao gồm tài khoản không hoạt động. Mã chứng từ tìm theo chuỗi con; %, _ được hiểu là ký tự thường.

Ngày tính theo UTC+7: bắt đầu bao gồm 00:00 ngày chọn, kết thúc bao gồm hết ngày chọn (API dùng mốc đầu ngày tiếp theo và loại trừ). Áp dụng bộ lọc đưa về trang đầu. Trang trước/sau dùng cursor, giữ bộ lọc khi xem chi tiết và quay lại.

Mỗi trang 20 bản ghi; API giới hạn 1–50. Thứ tự CreatedAt giảm dần, Source ordinal tăng dần, AuditLogID giảm dần. Mỗi nguồn lấy tối đa pageSize+1 bằng keyset; gateway hợp nhất một trang, tổng số là tổng count theo bộ lọc không tính cursor. Không tải toàn bộ log vào trình duyệt. Khi có log mới, về trang đầu để xem.

## Chi tiết và thông tin nhạy cảm

Chi tiết có metadata, before/after và bảng trường thay đổi. Tạo mới có thể không có OldValues. Snapshot performedByName được ưu tiên; createdByName chỉ dùng cho tạo/import, tránh nhầm người tạo với người sửa. Nếu không có tên snapshot, lấy tên theo UserID; tài khoản không còn tồn tại vẫn hiện ID dự phòng.

Source API và gateway đều ẩn password/hash/token/secret/connection string/importKey trước khi trả JSON. Nội dung JSON hỏng hoặc dạng scalar được thay bằng thông báo an toàn. Frontend hiển thị text, không thực thi HTML.

Chứng từ ưu tiên mã trong bảng PO/Inbound cùng database, sau đó mã snapshot hoặc EntityID. Putaway chỉ liên kết Inbound qua inboundId rõ ràng, không lấy mã phiếu nhập do ID vô tình trùng.

## API chỉ đọc

| Endpoint | Quyền |
|---|---|
| GET /api/admin/audit-logs | ADMIN — tổng hợp/lọc/phân trang |
| GET /api/admin/audit-logs/options | ADMIN — nhân viên/hành động |
| GET /api/admin/audit-logs/{source}/{id} | ADMIN — chi tiết |
| GET /api/audit-logs, /actions, /{id} tại Procurement/Inventory | ADMIN — nguồn |
| GET /api/auth/audit-users | ADMIN — thông tin hiển thị nhân viên |

Query: userId, businessGroup, action, documentCode, from, to, pageSize, cursor. Dates là DateTimeOffset, chuyển sang UTC trước khi lọc CreatedAt. Sai bộ lọc/cursor trả400; chưa đăng nhập401, sai quyền403, không có chi tiết404, lỗi nguồn503. Gateway không bỏ qua nguồn lỗi và trả danh sách thiếu như thể đầy đủ.

## Kiểm thử

```powershell
dotnet run --project tests/WMS.Audit.Gateway.Tests
dotnet run --project tests/WMS.Audit.Source.Tests
dotnet run --project tests/Identity.Authentication.Tests
cd frontend
npm.cmd test
npm.cmd run build
```

Bài SQL tùy chọn chỉ dùng database tạm tự sinh:

```powershell
$env:EWMS_AUDIT_TEST_SERVER = 'MIDUKONGU'
dotnet run --project tests/WMS.Audit.Source.Tests
Remove-Item Env:EWMS_AUDIT_TEST_SERVER
```

Đã kiểm tra filter, UTC+7 boundaries, cursor trùng thời điểm, wildcard, tên người sửa, Putaway ID collision, redaction, API chỉ đọc và quyền trực tiếp của cả hai nguồn.

## Thử dữ liệu thật

Nếu danh sách trống, đăng nhập Purchasing Staff và tạo PO hoặc xác nhận import Excel. Sau đó đăng nhập Admin, vào Audit Logs, lọc theo nhân viên/mã PO và mở chi tiết. Thay đổi SQL trực tiếp trước đây không tự sinh log ứng dụng. Inbound/Putaway sẽ có dữ liệu khi nghiệp vụ tương ứng được triển khai và ghi AuditLog trong transaction.
