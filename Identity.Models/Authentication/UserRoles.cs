namespace Identity.Models.Authentication;

public static class UserRoles
{
    public const string Admin = "ADMIN";
    public const string Purchasing = "PURCHASING_STAFF";
    public const string Warehouse = "WAREHOUSE_STAFF";
    public static bool IsSupported(string role) => role is Admin or Purchasing or Warehouse;
}
