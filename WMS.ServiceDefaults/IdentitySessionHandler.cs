using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WMS.ServiceDefaults;

public sealed partial class IdentitySessionHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IHttpClientFactory clients) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.ContainsKey("EWMS.Session")) return AuthenticateResult.NoResult();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.TryAddWithoutValidation("Cookie", Request.Headers.Cookie.ToString());
        using var response = await clients.CreateClient("identity-session").SendAsync(request, Context.RequestAborted);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return AuthenticateResult.Fail("Session expired.");
        if (!response.IsSuccessStatusCode) throw new ServiceUnavailableException("Cannot validate session.");
        var user = await response.Content.ReadFromJsonAsync<SessionUser>(cancellationToken: Context.RequestAborted);
        if (user is null || user.UserId <= 0 || user.Role is not ("ADMIN" or "PURCHASING_STAFF" or "WAREHOUSE_STAFF"))
            return AuthenticateResult.Fail("Invalid session profile.");
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()), new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role), new("full_name", user.FullName)
        };
        if (user.WarehouseId is { } warehouseId) claims.Add(new("warehouse_id", warehouseId.ToString()));
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name));
    }
}
