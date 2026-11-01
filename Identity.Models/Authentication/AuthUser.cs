namespace Identity.Models.Authentication;

public static class UserRoles
{
    public const string Admin = "ADMIN";
    public const string Purchasing = "PURCHASING_STAFF";
    public const string Warehouse = "WAREHOUSE_STAFF";
    public static bool IsSupported(string role) => role is Admin or Purchasing or Warehouse;
}

public sealed record AuthUser(int UserId, string Username, string FullName, string PasswordHash,
    string Role, int? WarehouseId, string Status, byte[] RowVersion);

public sealed record UserDisplay(int UserId, string Username, string FullName);

public sealed record UserProfile(int UserId, string Username, string FullName, string Role, int? WarehouseId)
{
    public static UserProfile From(AuthUser user) =>
        new(user.UserId, user.Username, user.FullName, user.Role, user.WarehouseId);
}
