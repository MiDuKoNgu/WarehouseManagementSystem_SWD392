using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.BLL.Authentication;
using Identity.BLL.Security;
using Identity.Models.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;

namespace Identity.Authentication.Tests;

internal sealed class FakeUsers : IUserRepository
{
    public bool FailUserLookup { get; set; }
    public const string Password = "Test-only-Password-491!";
    private readonly List<AuthUser> _users;
    public FakeUsers()
    {
        var hash = new PasswordService().Hash(Password);
        _users = [
            new(1, "admin", "Admin Test", hash, UserRoles.Admin, null, "ACTIVE", [1]),
            new(2, "buyer", "Buyer Test", hash, UserRoles.Purchasing, null, "ACTIVE", [2]),
            new(3, "keeper", "Keeper Test", hash, UserRoles.Warehouse, 7, "ACTIVE", [3]),
            new(4, "disabled", "Disabled", hash, UserRoles.Admin, null, "INACTIVE", [4]),
            new(5, "unassigned", "Unassigned", hash, UserRoles.Warehouse, null, "ACTIVE", [5]),
            new(6, "invalidWarehouse", "Invalid", hash, UserRoles.Warehouse, 0, "ACTIVE", [6]),
            new(7, "unknownRole", "Unknown", hash, "OTHER", null, "ACTIVE", [7])
        ];
    }
    public void RemoveWarehouse() => _users[2] = _users[2] with { WarehouseId = null, RowVersion = [8] };
    public Task<AuthUser?> FindByUsernameAsync(string username, CancellationToken cancellationToken) => Task.FromResult(_users.Find(user => user.Username == username));
    public Task<AuthUser?> FindByIdAsync(int id, CancellationToken cancellationToken) => FailUserLookup
        ? throw new InvalidOperationException("Simulated SQL outage during session validation.")
        : Task.FromResult(_users.Find(user => user.UserId == id));
    public Task<UserDisplay[]> ListDisplayNamesAsync(CancellationToken cancellationToken) => Task.FromResult(_users.Select(user => new UserDisplay(user.UserId, user.Username, user.FullName)).ToArray());
    public Task UpdatePasswordHashAsync(AuthUser user, string hash, CancellationToken cancellationToken) => Task.CompletedTask;
}
