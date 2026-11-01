using System.Security.Claims;
using Identity.BLL.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Identity.API.Authentication;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipUserLookupAttribute : Attribute { }

public sealed class SessionValidation(IUserRepository users) : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        // CSRF issuance and cookie deletion need the signed cookie identity, not SQL.
        // Keep that identity so antiforgery tokens remain bound to the same user.
        if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<SkipUserLookupAttribute>() is not null)
            return;

        var idText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = int.TryParse(idText, out var id)
            ? await users.FindByIdAsync(id, context.HttpContext.RequestAborted) : null;
        if (user is null || LoginService.AccessError(user) is not null
            || context.Principal?.FindFirstValue("row_version") != Convert.ToBase64String(user.RowVersion))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
            return;
        }
        context.HttpContext.Items["AuthUser"] = user;
    }
}
