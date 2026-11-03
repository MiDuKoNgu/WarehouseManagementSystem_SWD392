namespace Identity.Models.Authentication;

public sealed record AuthUser(int UserId, string Username, string FullName, string PasswordHash,
    string Role, int? WarehouseId, string Status, byte[] RowVersion);
