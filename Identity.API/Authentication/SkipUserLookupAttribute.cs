using System.Security.Claims;
using Identity.BLL.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Identity.API.Authentication;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipUserLookupAttribute : Attribute { }
