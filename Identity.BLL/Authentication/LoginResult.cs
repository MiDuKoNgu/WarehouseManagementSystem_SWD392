using Identity.BLL.Security;
using Identity.Models.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Identity.BLL.Authentication;

public sealed record LoginResult(AuthUser? User, string? Error, bool Forbidden = false);
