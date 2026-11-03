using System.Data;
using System.Text;
using Identity.BLL.Security;
using Identity.DAL;
using Identity.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.DAL;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--self-test"))
{
    var passwords = new PasswordService();
    const string sample = "Seed-test-only-482!";
    var first = passwords.Hash(sample);
    var second = passwords.Hash(sample);
    if (first == second || passwords.Verify(first, sample) == PasswordVerificationResult.Failed
        || passwords.Verify(first, "wrong-password") != PasswordVerificationResult.Failed)
        throw new InvalidOperationException("Password self-test failed.");
    Console.WriteLine("PASS: hash ngẫu nhiên, mật khẩu đúng được chấp nhận, mật khẩu sai bị từ chối. Không truy cập database.");
    return 0;
}

try
{
    if (Console.IsInputRedirected)
        throw new InvalidOperationException("Hãy chạy trong terminal tương tác để nhập mật khẩu ẩn.");
    var resetPassword = args.Contains("--reset-password");
    Console.WriteLine(resetPassword ? "EWMS — Đặt lại mật khẩu tài khoản" : "EWMS — Tạo tài khoản cho IdentityDB");
    Console.WriteLine(resetPassword
        ? "Chỉ cập nhật mật khẩu của tài khoản được chọn; giữ nguyên vai trò, trạng thái và kho. Ctrl+C để hủy.\n"
        : "Không tạo database/bảng, không sửa tài khoản đã tồn tại. Ctrl+C để hủy.\n");

    // Never pass credentials/passwords as command-line arguments or commit them.
    var connectionString = Environment.GetEnvironmentVariable("EWMS_IDENTITY_CONNECTION");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        Console.Write("SQL Server [localhost]: ");
        var server = Console.ReadLine()?.Trim();
        Console.Write("Database [IdentityDB]: ");
        var database = Console.ReadLine()?.Trim();
        connectionString = new SqlConnectionStringBuilder
        {
            DataSource = string.IsNullOrEmpty(server) ? "localhost" : server,
            InitialCatalog = string.IsNullOrEmpty(database) ? "IdentityDB" : database,
            IntegratedSecurity = true,
            // Local development only; production should validate a trusted certificate.
            TrustServerCertificate = true,
            ConnectTimeout = 15
        }.ConnectionString;
        Console.WriteLine("Dùng Windows Authentication (chứng chỉ SQL được tin cậy cho môi trường local).");
    }

    await using var db = IdentityDbContext.Create(connectionString);
    await db.Database.OpenConnectionAsync();
    Console.WriteLine("Đã kết nối SQL Server.");
    var username = ReadRequired("Username", 100);
    if (resetPassword)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Username == username);
        if (user is null)
        {
            Console.WriteLine("Không tìm thấy tài khoản. Không thay đổi dữ liệu.");
            return 1;
        }
        Console.WriteLine($"Tài khoản: {username} — {user.FullName} (ID {user.UserId})");
        Console.Write("Đặt lại mật khẩu tài khoản này? Nhập RESET để tiếp tục: ");
        if (Console.ReadLine()?.Trim() != "RESET")
        {
            Console.WriteLine("Đã hủy. Không thay đổi dữ liệu.");
            return 0;
        }
        var newPassword = ReadNewPassword();
        user.PasswordHash = new PasswordService().Hash(newPassword);
        newPassword = string.Empty;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            Console.WriteLine("Tài khoản đã thay đổi trong lúc thao tác. Chưa cập nhật mật khẩu; vui lòng chạy lại.");
            return 1;
        }
        Console.WriteLine("Đã đặt lại mật khẩu. Hãy đăng nhập bằng mật khẩu mới; các phiên cũ sẽ bị từ chối ở yêu cầu tiếp theo.");
        return 0;
    }
    if (await db.Users.AnyAsync(x => x.Username == username))
    {
        Console.WriteLine("Username đã tồn tại. Không thay đổi mật khẩu hoặc tài khoản.");
        return 0;
    }
    var fullName = ReadRequired("Họ tên", 150);
    string role;
    while (true)
    {
        Console.Write("Vai trò [1=ADMIN, 2=PURCHASING_STAFF, 3=WAREHOUSE_STAFF; mặc định 1]: ");
        var choice = Console.ReadLine()?.Trim();
        role = choice switch { "" or null or "1" => "ADMIN", "2" => "PURCHASING_STAFF", "3" => "WAREHOUSE_STAFF", _ => "" };
        if (role.Length > 0) break;
        Console.WriteLine("Vui lòng chọn 1, 2 hoặc 3.");
    }
    int? warehouseId = null;
    if (role == "WAREHOUSE_STAFF")
    {
        while (true)
        {
            Console.Write("WarehouseID (Enter để chưa gán kho — tài khoản chưa thể đăng nhập): ");
            var value = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(value)) break;
            if (!int.TryParse(value, out var candidate) || candidate <= 0)
            {
                Console.WriteLine("WarehouseID phải là số nguyên dương.");
                continue;
            }
            var warehouseConnection = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "WarehouseDB" };
            await using var warehouseDb = new WarehouseDbContext(warehouseConnection.ConnectionString);
            if (!await warehouseDb.Warehouses.AnyAsync(x => x.WarehouseId == candidate && x.Status == "ACTIVE"))
            {
                Console.WriteLine("Không tìm thấy kho ACTIVE trong WarehouseDB. Vui lòng kiểm tra lại.");
                continue;
            }
            warehouseId = candidate;
            break;
        }
    }
    var password = ReadNewPassword();
    var passwordHash = new PasswordService().Hash(password);
    password = string.Empty;
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    var existing = await db.Users.FromSqlInterpolated($"SELECT * FROM dbo.[User] WITH (UPDLOCK,HOLDLOCK) WHERE Username={username}").AnyAsync();
    var created = !existing;
    if (created)
    {
        var roleId = await db.Roles.Where(x => x.RoleName == role).Select(x => (int?)x.RoleId).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("Thiếu role. Hãy tạo role trước khi seed tài khoản.");
        db.Users.Add(new User { RoleId = roleId, WarehouseId = warehouseId, Username = username,
            PasswordHash = passwordHash, FullName = fullName, Status = "ACTIVE" });
        await db.SaveChangesAsync();
    }
    await transaction.CommitAsync();
    Console.WriteLine(created
        ? $"Đã tạo tài khoản {role}, trạng thái ACTIVE, WarehouseID = {warehouseId?.ToString() ?? "NULL"}."
        : "Username đã được tạo bởi tiến trình khác. Không thay đổi tài khoản.");
    return 0;
}
catch (DbUpdateException exception) when (exception.InnerException is SqlException)
{
    var sql = (SqlException)exception.InnerException!;
    Console.Error.WriteLine($"Không thể seed (SQL error {sql.Number}). Kiểm tra dữ liệu và kết nối database.");
    return 1;
}
catch (SqlException exception)
{
    Console.Error.WriteLine($"Không thể seed (SQL error {exception.Number}). Kiểm tra server, quyền kết nối, bảng Role/User, role và WarehouseDB nếu gán kho.");
    return 1;
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
catch (ArgumentException)
{
    Console.Error.WriteLine("Chuỗi kết nối không hợp lệ. Kiểm tra EWMS_IDENTITY_CONNECTION.");
    return 1;
}

static string ReadRequired(string label, int maxLength)
{
    while (true)
    {
        Console.Write($"{label}: ");
        var value = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(value) && value.Length <= maxLength) return value;
        Console.WriteLine($"Vui lòng nhập từ 1 đến {maxLength} ký tự.");
    }
}
static string ReadSecret(string label)
{
    Console.Write(label);
    var value = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
        if (key.Key == ConsoleKey.Backspace)
        {
            if (value.Length > 0) value.Length--;
        }
        else if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
    }
}
static string ReadNewPassword()
{
    while (true)
    {
        var password = ReadSecret("Mật khẩu mới (12–1024 ký tự): ");
        if (password.Length < 12 || password.Length > 1024)
        {
            Console.WriteLine("Vui lòng nhập từ 12 đến 1024 ký tự.");
            continue;
        }
        if (password == ReadSecret("Nhập lại mật khẩu: ")) return password;
        Console.WriteLine("Hai mật khẩu không khớp. Hãy nhập lại.");
    }
}
