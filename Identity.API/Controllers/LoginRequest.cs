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

public sealed record LoginRequest(
    [Required, StringLength(100)] string Username,
    [Required, StringLength(1024)] string Password,
    bool RememberMe);
