using Microsoft.AspNetCore.Identity;

namespace Identity.BLL.Security;

/// <summary>Shared password format for account creation and login verification.</summary>
public sealed class PasswordService
{
    private readonly PasswordHasher<object> _hasher = new();
    private readonly object _user = new();

    public string Hash(string password) => _hasher.HashPassword(_user, password);

    // SuccessRehashNeeded is also a valid password; the login service should then save a new hash.
    public PasswordVerificationResult Verify(string storedHash, string password) =>
        _hasher.VerifyHashedPassword(_user, storedHash, password);
}
