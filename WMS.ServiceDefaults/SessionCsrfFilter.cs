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

public sealed class SessionCsrfFilter(IHttpClientFactory clients) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (!http.Request.Headers.TryGetValue("X-CSRF-TOKEN", out var token) || string.IsNullOrWhiteSpace(token))
        {
            context.Result = new BadRequestObjectResult(new { message = "Phiên biểu mẫu đã hết hạn. Vui lòng tải lại trang." });
            return;
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/validate-csrf");
        request.Headers.TryAddWithoutValidation("Cookie", http.Request.Headers.Cookie.ToString());
        request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token.ToString());
        using var response = await clients.CreateClient("identity-session").SendAsync(request, http.RequestAborted);
        if (response.IsSuccessStatusCode) { await next(); return; }
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            context.Result = new ObjectResult(new { message = "Phiên đăng nhập hoặc biểu mẫu không còn hợp lệ. Vui lòng tải lại trang." }) { StatusCode = (int)response.StatusCode };
        else throw new ServiceUnavailableException("Cannot validate CSRF.");
    }
}
