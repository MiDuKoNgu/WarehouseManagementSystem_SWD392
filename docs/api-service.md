# API Service — cửa vào chung cho frontend

## Kiến trúc đang triển khai

Frontend gọi đường dẫn tương đối `/api/...`. Khi chạy development, Vite chuyển toàn bộ
nhóm đường dẫn này đến `WMS.API` tại `http://localhost:5062`; WMS chuyển tiếp đến service
nghiệp vụ bằng YARP. Frontend không còn định tuyến trực tiếp đến từng backend.

```text
Browser → Vite /api → WMS.API (API Service)
                       ├─ Identity.API → IdentityDB
                       ├─ Catalog.API → CatalogDB
                       ├─ Warehouse.API → WarehouseDB
                       ├─ Procurement.API → ProcurementDB
                       └─ Inventory.API → InventoryDB
```

WMS.API không có database riêng. Các lời gọi nội bộ giữa service nghiệp vụ vẫn đi
trực tiếp đến service sở hữu dữ liệu, theo cấu hình hiện có. Thay đổi này bổ sung
cửa vào chung cho frontend; không thay đổi schema hoặc chuyển nghiệp vụ sang WMS.

## Bảng định tuyến

Các prefix khớp cả URL gốc và URL con, theo từng đoạn đường dẫn. Ví dụ
`/api/products` và `/api/products/1` cùng được chuyển đến Catalog, nhưng
`/api/products-other` không khớp.

| Prefix | Đích mặc định | Cấu hình tại WMS.API |
|---|---|---|
| `/api/auth`, `/api/workspaces` | Identity.API — `http://localhost:5140` | `Services:Identity` |
| `/api/products`, `/api/catalog` | Catalog.API — `http://localhost:5254` | `Services:Catalog` |
| `/api/warehouses` | Warehouse.API — `http://localhost:5104` | `Services:Warehouse` |
| `/api/purchase-orders`, `/api/receiving-orders` | Procurement.API — `http://localhost:5136` | `Services:Procurement` |
| `/api/inbounds`, `/api/inventory`, `/api/putaway` | Inventory.API — `http://localhost:5159` | `Services:Inventory` |
| `/api/admin/audit-logs` | Controller tổng hợp audit ngay tại WMS.API | Dùng các địa chỉ service trên để đọc nguồn |

Không có fallback `/api` đến Identity. Các API không thuộc bảng định tuyến hoặc
controller của WMS trả 404. `/api/audit-logs` của các service nguồn không được mở
thành một route chung; Admin dùng `/api/admin/audit-logs`.

Địa chỉ đích đặt trong `WMS.API/appsettings.json`, có thể ghi đè bằng biến môi trường
như `Services__Identity`. Giá trị phải là HTTP(S) origin tuyệt đối, không có thông
tin đăng nhập, đường dẫn con, query hoặc fragment. Khởi động lại WMS sau khi đổi
cấu hình. Không đặt địa chỉ service nghiệp vụ trỏ ngược về WMS vì sẽ tạo vòng lặp.

## Đăng nhập, phân quyền và truyền dữ liệu

Luồng login hiện là:

```text
Frontend → WMS.API → Identity.API: GET /api/auth/csrf
Frontend → WMS.API → Identity.API: POST /api/auth/login
Identity.API → WMS.API → Browser: Set-Cookie và UserProfile
```

Cookie `EWMS.Session`, cookie CSRF, header `X-CSRF-TOKEN`, phương thức HTTP, path,
query và request body được chuyển tiếp. Response giữ status, body, các header
`Set-Cookie`, `Content-Type` và `Content-Disposition`. Upload multipart và tải
file Excel đi qua cùng cửa vào. WMS không lưu cookie trong một cookie jar dùng
chung và không tự đi theo redirect của service đích.

Service nghiệp vụ tiếp tục kiểm tra phiên, role, kho và CSRF như trước. Proxy chạy
trước middleware xác thực audit của WMS để tránh kiểm tra phiên hai lần, đồng thời
giữ khả năng lấy CSRF và logout với cookie cũ khi Identity không đọc được tài khoản.
Các controller audit cục bộ vẫn yêu cầu role `ADMIN`.

Lỗi nghiệp vụ như 400/401/403/409/429 được chuyển về nguyên trạng. Lỗi kết nối đến
service đích trả lỗi gateway 502/504 kèm thông báo, nếu response chưa bắt đầu.

## Khởi chạy và kiểm tra

Tại thư mục solution, chạy:

```powershell
.\scripts\start-ewms.ps1
```

Script khởi động cả sáu backend, bao gồm WMS.API. Trong terminal khác:

```powershell
cd frontend
npm.cmd run dev
```

Nếu đang chạy bản cũ, dừng bằng Ctrl+C trong terminal sở hữu tiến trình, rồi chạy
lại backend và Vite để nhận code/cấu hình mới. Không chỉ chạy `start-identity.ps1`
khi sử dụng frontend qua gateway.

Mở Network trong trình duyệt: frontend tiếp tục gọi `/api/...` cùng origin.
Vite chỉ có một đích API là WMS 5062. Các cổng service riêng có thể dùng cho
debug/Swagger nhưng không còn là các đích proxy của frontend.

```powershell
dotnet run --project tests/WMS.ApiService.Tests
dotnet run --project tests/WMS.Audit.Gateway.Tests
dotnet run --project tests/Identity.Authentication.Tests
```

## Khi triển khai ngoài development

Vite dev proxy không phải cấu hình triển khai production. Web server phục vụ bản
build frontend cần chuyển `/api` cùng origin đến WMS.API. Các địa chỉ upstream
phải phù hợp với môi trường triển khai và chứng chỉ hợp lệ.

Identity hiện bật chuyển HTTP sang HTTPS ngoài Development. Dùng địa chỉ HTTPS
upstream phù hợp, hoặc triển khai xử lý forwarded headers từ proxy tin cậy một
cách tường minh nếu kết thúc TLS ở cửa vào. Không giả định cấu hình localhost HTTP
development đủ cho production. Khi cần giữ IP client cho rate limiting, cũng cần
cấu hình trusted proxies/forwarded headers; không tin header do client tự gửi.

Proxy hiện không viết lại origin trong header `Location`. Response tạo mới dùng
`CreatedAtAction` có thể chứa địa chỉ upstream; frontend hiện đọc JSON response
để điều hướng nên không phụ thuộc header này. Nếu client production cần đi theo
`Location`, cần cấu hình host forwarding tin cậy hoặc phép biến đổi response phù hợp.

Việc frontend sử dụng WMS không tự chặn truy cập trực tiếp các cổng backend.
Muốn chỉ công khai API Service cần cấu hình binding, mạng hoặc firewall ở môi
trường triển khai. Các service vẫn giữ kiểm tra quyền của riêng mình.

## Mã liên quan

- `WMS.API/Gateway/ApiServiceProxy.cs`: bảng định tuyến và chuyển tiếp.
- `WMS.API/Program.cs`: thứ tự middleware proxy và audit.
- `WMS.API/appsettings.json`: địa chỉ service nghiệp vụ.
- `frontend/vite.config.ts`: một proxy `/api` đến WMS.API.
- [YARP direct forwarding](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/direct-forwarding): cơ chế chuyển tiếp HTTP được sử dụng.
