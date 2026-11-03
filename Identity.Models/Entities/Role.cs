namespace Identity.Models.Entities;

public sealed class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public ICollection<User> Users { get; set; } = new List<User>();
}
