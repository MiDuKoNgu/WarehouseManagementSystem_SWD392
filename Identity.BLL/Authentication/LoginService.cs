using Identity.BLL.Security;
using Identity.Models.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Identity.BLL.Authentication;

public sealed record LoginResult(AuthUser? User, string? Error, bool Forbidden = false);

public sealed class LoginService(IUserRepository users, PasswordService passwords)
{
    private static readonly string DummyHash = new PasswordService().Hash(Guid.NewGuid().ToString());

    public static string? AccessError(AuthUser user)
    {
        if (user.Status != "ACTIVE") return "Tài khoản đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.";
        if (!UserRoles.IsSupported(user.Role)) return "Tài khoản chưa được cấp quyền truy cập hệ thống.";
        if (user.Role == UserRoles.Warehouse && user.WarehouseId is not > 0)
            return "Bạn chưa được gán vào kho. Vui lòng liên hệ quản trị viên.";
        return null;
    }

    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByUsernameAsync(username.Trim(), cancellationToken);
        PasswordVerificationResult verification;
        try { verification = passwords.Verify(user?.PasswordHash ?? DummyHash, password); }
        catch (FormatException) { verification = PasswordVerificationResult.Failed; }
        if (user is null || verification == PasswordVerificationResult.Failed)
            return new(null, "Tài khoản hoặc mật khẩu không đúng.");
        if (AccessError(user) is { } error) return new(null, error, true);

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            await users.UpdatePasswordHashAsync(user, passwords.Hash(password), cancellationToken);
            user = await users.FindByIdAsync(user.UserId, cancellationToken);
            if (user is null || AccessError(user) is not null
                || passwords.Verify(user.PasswordHash, password) == PasswordVerificationResult.Failed)
                return new(null, "Tài khoản đã thay đổi. Vui lòng đăng nhập lại.");
        }
        return new(user, null);
    }
}
