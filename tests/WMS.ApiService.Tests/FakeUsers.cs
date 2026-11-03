extern alias WmsApi;
extern alias IdentityApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Identity.BLL.Authentication;
using Identity.BLL.Security;
using Identity.Models.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace WMS.ApiService.Tests;

internal sealed class FakeUsers : IUserRepository
{
    public const string Password = "Gateway-test-only-password-723!";
    private AuthUser buyer = new(7, "buyer", "Gateway Buyer", new PasswordService().Hash(Password), UserRoles.Purchasing, null, "ACTIVE", [1]);
    public bool FailLookup { get; set; }
    public void ChangeVersion() => buyer = buyer with { RowVersion = [(byte)(buyer.RowVersion[0] + 1)] };
    public Task<AuthUser?> FindByUsernameAsync(string username, CancellationToken cancellationToken) => Task.FromResult<AuthUser?>(username == buyer.Username ? buyer : null);
    public Task<AuthUser?> FindByIdAsync(int id, CancellationToken cancellationToken) => FailLookup
        ? throw new InvalidOperationException("Simulated unavailable user store")
        : Task.FromResult<AuthUser?>(id == buyer.UserId ? buyer : null);
    public Task<UserDisplay[]> ListDisplayNamesAsync(CancellationToken cancellationToken) => Task.FromResult(new[] { new UserDisplay(buyer.UserId, buyer.Username, buyer.FullName) });
    public Task UpdatePasswordHashAsync(AuthUser user, string hash, CancellationToken cancellationToken)
    { buyer = buyer with { PasswordHash = hash }; return Task.CompletedTask; }
}
