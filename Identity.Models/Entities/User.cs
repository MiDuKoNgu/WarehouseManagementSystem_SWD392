namespace Identity.Models.Entities;

public sealed class User
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public int? WarehouseId { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Role Role { get; set; } = null!;
}
