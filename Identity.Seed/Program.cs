using System.Data;
using System.Text;
using Identity.BLL.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

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

    // Never pass connection credentials/passwords as command-line arguments or commit them to source.
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
            // Local development only. Production should validate a trusted server certificate.
            TrustServerCertificate = true,
            ConnectTimeout = 15
        }.ConnectionString;
        Console.WriteLine("Dùng Windows Authentication (chứng chỉ SQL được tin cậy cho môi trường local).");
    }

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    Console.WriteLine("Đã kết nối SQL Server.");

    var username = ReadRequired("Username", 100);
    if (resetPassword)
    {
        await using var lookup = new SqlCommand("SELECT UserID, FullName, RowVersion FROM dbo.[User] WHERE Username = @username", connection);
        lookup.Parameters.Add("@username", SqlDbType.NVarChar, 100).Value = username;
        int userId;
        byte[] rowVersion;
        await using (var reader = await lookup.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                Console.WriteLine("Không tìm thấy tài khoản. Không thay đổi dữ liệu.");
                return 1;
            }
            userId = reader.GetInt32(0);
            rowVersion = (byte[])reader[2];
            Console.WriteLine($"Tài khoản: {username} — {reader.GetString(1)} (ID {userId})");
        }
        Console.Write("Đặt lại mật khẩu tài khoản này? Nhập RESET để tiếp tục: ");
        if (Console.ReadLine()?.Trim() != "RESET")
        {
            Console.WriteLine("Đã hủy. Không thay đổi dữ liệu.");
            return 0;
        }
        var newPassword = ReadNewPassword();
        var newHash = new PasswordService().Hash(newPassword);
        newPassword = string.Empty;
        await using var update = new SqlCommand("""
            UPDATE dbo.[User] SET PasswordHash = @hash
            WHERE UserID = @id AND RowVersion = @version
            """, connection);
        update.Parameters.Add("@hash", SqlDbType.NVarChar, 512).Value = newHash;
        update.Parameters.Add("@id", SqlDbType.Int).Value = userId;
        update.Parameters.Add("@version", SqlDbType.Timestamp, 8).Value = rowVersion;
        if (await update.ExecuteNonQueryAsync() != 1)
        {
            Console.WriteLine("Tài khoản đã thay đổi trong lúc thao tác. Chưa cập nhật mật khẩu; vui lòng chạy lại.");
            return 1;
        }
        Console.WriteLine("Đã đặt lại mật khẩu. Hãy đăng nhập bằng mật khẩu mới; các phiên cũ sẽ bị từ chối ở yêu cầu tiếp theo.");
        return 0;
    }
    // Check before asking for a password, then check again inside a transaction to handle concurrent runs.
    if (await UserExists(connection, username))
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
            var text = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(text)) break;
            if (!int.TryParse(text, out var candidate) || candidate <= 0)
            {
                Console.WriteLine("WarehouseID phải là số nguyên dương.");
                continue;
            }
            var warehouseConnection = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "WarehouseDB" };
            await using var warehouseDb = new SqlConnection(warehouseConnection.ConnectionString);
            await warehouseDb.OpenAsync();
            await using var warehouseQuery = new SqlCommand("SELECT COUNT(*) FROM dbo.Warehouse WHERE WarehouseID = @id AND Status = 'ACTIVE'", warehouseDb);
            warehouseQuery.Parameters.Add("@id", SqlDbType.Int).Value = candidate;
            if (Convert.ToInt32(await warehouseQuery.ExecuteScalarAsync()) == 0)
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
    using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
    await using var command = new SqlCommand("""
        IF EXISTS (SELECT 1 FROM dbo.[User] WITH (UPDLOCK, HOLDLOCK) WHERE Username = @username)
            SELECT CAST(0 AS int);
        ELSE
        BEGIN
            DECLARE @roleId int = (SELECT RoleID FROM dbo.[Role] WHERE RoleName = @role);
            IF @roleId IS NULL
                THROW 50001, 'Thieu role. Hay tao role truoc khi seed tai khoan.', 1;
            INSERT INTO dbo.[User]
                (RoleID, WarehouseID, Username, PasswordHash, FullName, Email, Phone, Status, CreatedAt)
            VALUES
                (@roleId, @warehouseId, @username, @hash, @fullName, NULL, NULL, 'ACTIVE', SYSUTCDATETIME());
            SELECT CAST(1 AS int);
        END
        """, connection, transaction);
    command.Parameters.Add("@username", SqlDbType.NVarChar, 100).Value = username;
    command.Parameters.Add("@hash", SqlDbType.NVarChar, 512).Value = passwordHash;
    command.Parameters.Add("@fullName", SqlDbType.NVarChar, 150).Value = fullName;
    command.Parameters.Add("@role", SqlDbType.VarChar, 32).Value = role;
    command.Parameters.Add("@warehouseId", SqlDbType.Int).Value = warehouseId is { } assigned ? assigned : DBNull.Value;
    var created = Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    await transaction.CommitAsync();
    Console.WriteLine(created
        ? $"Đã tạo tài khoản {role}, trạng thái ACTIVE, WarehouseID = {warehouseId?.ToString() ?? "NULL"}."
        : "Username đã được tạo bởi tiến trình khác. Không thay đổi tài khoản.");
    return 0;
}
catch (SqlException exception)
{
    // Do not print connection strings, SQL parameters or full exception details containing secrets.
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

static async Task<bool> UserExists(SqlConnection connection, string username)
{
    await using var command = new SqlCommand(
        "SELECT COUNT(*) FROM dbo.[User] WHERE Username = @username", connection);
    command.Parameters.Add("@username", SqlDbType.NVarChar, 100).Value = username;
    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
}
