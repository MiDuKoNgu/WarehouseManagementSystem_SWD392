using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Identity.BLL.Authentication;
using Identity.API.Authentication;
using Identity.Models.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(LoginService loginService, IAntiforgery antiforgery) : ControllerBase
{
    [Authorize(Roles = "ADMIN")]
    [HttpGet("audit-users")]
    public async Task<IActionResult> AuditUsers([FromServices] IUserRepository users, CancellationToken ct) =>
        Ok(await users.ListDisplayNamesAsync(ct));

    [Authorize]
    [HttpPost("validate-csrf")]
    public async Task<IActionResult> ValidateCsrf() =>
        await antiforgery.IsRequestValidAsync(HttpContext)
            ? NoContent() : BadRequest(new { message = "Phiên biểu mẫu không hợp lệ." });

    [Authorize(Roles = "PURCHASING_STAFF,ADMIN")]
    [HttpGet("users/{id:int}/display-name")]
    public async Task<IActionResult> DisplayName(int id, [FromServices] IUserRepository users, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(new { userId = user.UserId, fullName = user.FullName });
    }

    [HttpGet("csrf")]
    [SkipUserLookup]
    public IActionResult Csrf() => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
            return BadRequest(new { message = "Phiên biểu mẫu đã hết hạn. Vui lòng thử lại." });
        var result = await loginService.LoginAsync(request.Username, request.Password, cancellationToken);
        if (result.User is not { } user)
            return StatusCode(result.Forbidden ? 403 : 401, new { message = result.Error });

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username), new(ClaimTypes.Role, user.Role),
            new("row_version", Convert.ToBase64String(user.RowVersion))
        };
        if (user.WarehouseId is { } warehouseId) claims.Add(new("warehouse_id", warehouseId.ToString()));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties
            {
                IsPersistent = request.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(request.RememberMe ? TimeSpan.FromDays(14) : TimeSpan.FromHours(8)),
                AllowRefresh = false
            });
        return Ok(UserProfile.From(user));
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(UserProfile.From((AuthUser)HttpContext.Items["AuthUser"]!));

    [HttpPost("logout")]
    [SkipUserLookup]
    public async Task<IActionResult> Logout()
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
            return BadRequest(new { message = "Phiên biểu mẫu đã hết hạn. Vui lòng thử lại." });
        await HttpContext.SignOutAsync();
        return NoContent();
    }
}
