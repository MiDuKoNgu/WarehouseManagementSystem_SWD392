using System.Text.Json;
using System.Text.Json.Nodes;

namespace WMS.Audit;

public static class AuditValueRedactor
{
    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var node = JsonNode.Parse(value);
            // Scalars may be arbitrary free text containing secrets, so only structured payloads are exposed.
            if (node is not (JsonObject or JsonArray)) return Hidden();
            Redact(node);
            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException) { return Hidden(); }
    }

    private static string Hidden() => JsonSerializer.Serialize("Nội dung không có cấu trúc JSON hợp lệ; đã ẩn để bảo vệ thông tin nhạy cảm.");
    private static void Redact(JsonNode? node)
    {
        if (node is JsonObject obj)
            foreach (var pair in obj.ToArray())
            {
                var key = pair.Key.Replace("_", "").Replace("-", "").ToLowerInvariant();
                if (key.Contains("password") || key.Contains("secret") || key.Contains("token") ||
                    key is "importkey" or "apikey" or "privatekey" or "connectionstring" or "cookie" or "authorization" or "credentials")
                    obj[pair.Key] = "[Đã ẩn]";
                else Redact(pair.Value);
            }
        else if (node is JsonArray array)
            foreach (var item in array) Redact(item);
    }
}
