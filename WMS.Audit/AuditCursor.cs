using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace WMS.Audit;

public sealed record AuditCursor(DateTimeOffset CreatedAt, string Source, long AuditLogId)
{
    public static string Encode(AuditCursor cursor) => WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(cursor));
    public static AuditCursor? Decode(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        try
        {
            if (token.Length > 512) throw new FormatException();
            var value = JsonSerializer.Deserialize<AuditCursor>(WebEncoders.Base64UrlDecode(token));
            if (value is null || value.Source is not ("procurement" or "inventory") || value.AuditLogId <= 0 || value.CreatedAt == default)
                throw new FormatException();
            return value;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            throw new AuditValidationException(["Mã phân trang không hợp lệ. Vui lòng về trang đầu."]);
        }
    }
}
