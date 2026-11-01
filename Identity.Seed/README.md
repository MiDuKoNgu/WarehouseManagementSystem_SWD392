# Tạo tài khoản ADMIN đầu tiên

## Đặt lại mật khẩu đã quên

Tại thư mục solution, chạy `./scripts/start-identity.ps1 -Mode ResetPassword` bằng PowerShell. Nhập SQL login, sau đó username EWMS cần đổi (ví dụ `warehouse01`). Kiểm tra tài khoản được hiển thị, gõ `RESET`, rồi nhập mật khẩu mới hai lần. Mật khẩu nhập ẩn, dài 12–1024 ký tự; không cần gửi cho người khác.

Công cụ chỉ cập nhật PasswordHash, giữ nguyên quyền, trạng thái và kho. Không tạo tài khoản nếu username không tồn tại. Nếu dữ liệu tài khoản bị sửa đồng thời, thao tác sẽ dừng để tránh ghi đè. RowVersion tự thay đổi khiến các phiên đăng nhập cũ bị từ chối khi gọi API tiếp theo.

Nếu đã có biến môi trường kết nối, có thể chạy trực tiếp `dotnet run --project ./Identity.Seed -- --reset-password`.

Công cụ local tạo tài khoản trong schema IdentityDB của EWMS_Demo_5_Databases.sql. Không cần tạo Console App bên ngoài hoặc copy password hash. Chỉ chạy khi cần khởi tạo tài khoản; không chạy tự động lúc API khởi động.

## Chạy

Mở PowerShell tại thư mục chứa solution:

```powershell
dotnet run --project .\Identity.Seed
```

Nhập server giống tên đang dùng trong SSMS (ví dụ `.\SQLEXPRESS` hoặc `(localdb)\MSSQLLocalDB`). Database mặc định là `IdentityDB`. Công cụ mặc định dùng Windows Authentication và tin cậy chứng chỉ SQL cho môi trường local. Với cấu hình kết nối khác, đặt biến môi trường `EWMS_IDENTITY_CONNECTION` cho phiên terminal; không commit chuỗi chứa mật khẩu vào source.

Sau khi kết nối, nhập username, họ tên, mật khẩu ít nhất 12 ký tự và nhập lại mật khẩu. Khi nhập mật khẩu terminal không hiện ký tự. Không truyền mật khẩu qua tham số dòng lệnh.

## Dữ liệu được tạo

- `RoleID`: tra theo `RoleName = 'ADMIN'`, không giả định ID bằng 1.
- `WarehouseID`, `Email`, `Phone`: NULL.
- `Status`: ACTIVE (quy ước cần dùng thống nhất trong API login).
- `CreatedAt`: thời gian UTC của SQL Server.
- `PasswordHash`: tạo bằng PasswordService trong Identity.BLL, sử dụng ASP.NET Core PasswordHasher.
- `UserID` và `RowVersion`: do SQL Server sinh.

Nếu username tồn tại, công cụ bỏ qua, không reset mật khẩu. Cần có bảng Role, User và role ADMIN trước khi chạy. Công cụ không chạy lại script tạo bảng hoặc tự tạo role.

## Kiểm tra

```powershell
dotnet run --project .\Identity.Seed -- --self-test
```

Kiểm tra hash ngẫu nhiên và xác minh mật khẩu đúng/sai, không kết nối database.

Sau khi seed, kiểm tra trong SSMS (không cần đọc PasswordHash):

```sql
USE IdentityDB;
SELECT u.UserID, u.Username, u.FullName, r.RoleName, u.Status, u.WarehouseID
FROM dbo.[User] AS u
JOIN dbo.[Role] AS r ON r.RoleID = u.RoleID;
```

## Tích hợp login tiếp theo

API login dùng PasswordService.Verify: chỉ Failed là mật khẩu sai; SuccessRehashNeeded là mật khẩu hợp lệ và cần lưu hash mới. Kiểm tra Status ACTIVE trước khi cấp phiên. API chưa được triển khai trong bước seed này; tạo tài khoản xong chưa thể đăng nhập qua giao diện hiện tại.
