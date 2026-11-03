namespace Identity.Models.Authentication;

public sealed record UserProfile(int UserId, string Username, string FullName, string Role, int? WarehouseId)
{
    public static UserProfile From(AuthUser user) =>
        new(user.UserId, user.Username, user.FullName, user.Role, user.WarehouseId);
}
