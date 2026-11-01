using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace WMS.Audit;

public sealed class AuditValidationException(string[] errors) : Exception("Bộ lọc lịch sử không hợp lệ.")
{
    public string[] Errors { get; } = errors;
}

public sealed class AuditQuery
{
    public int? UserId { get; set; }
    public string? BusinessGroup { get; set; }
    public string? Action { get; set; }
    public string? DocumentCode { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int PageSize { get; set; } = 20;
    public string? Cursor { get; set; }

    public void Validate()
    {
        BusinessGroup = string.IsNullOrWhiteSpace(BusinessGroup) ? null : BusinessGroup.Trim().ToUpperInvariant();
        Action = string.IsNullOrWhiteSpace(Action) ? null : Action.Trim();
        DocumentCode = string.IsNullOrWhiteSpace(DocumentCode) ? null : DocumentCode.Trim();
        var errors = new List<string>();
        if (UserId is <= 0) errors.Add("Nhân viên không hợp lệ.");
        if (BusinessGroup is not (null or "PO" or "INBOUND" or "PUTAWAY")) errors.Add("Nhóm nghiệp vụ không hợp lệ.");
        if (Action?.Length > 60) errors.Add("Hành động tối đa 60 ký tự.");
        if (DocumentCode?.Length > 100) errors.Add("Mã chứng từ tối đa 100 ký tự.");
        if (PageSize is < 1 or > 50) errors.Add("Mỗi trang cần từ 1 đến 50 bản ghi.");
        if (From is { } from && To is { } to && from >= to) errors.Add("Thời điểm kết thúc phải sau thời điểm bắt đầu.");
        if (Cursor is { Length: > 0 })
        {
            try { AuditCursor.Decode(Cursor); }
            catch (AuditValidationException) { errors.Add("Mã phân trang không hợp lệ."); }
        }
        if (errors.Count > 0) throw new AuditValidationException(errors.ToArray());
    }
}

public sealed record AuditItem(string Source, long AuditLogId, int UserId, string? ActorName,
    string BusinessGroup, string Action, string EntityName, string EntityId, string? DocumentCode,
    string Status, DateTimeOffset CreatedAt);
public sealed record AuditDetail(AuditItem Item, string? OldValues, string? NewValues);
public sealed record AuditPage(AuditItem[] Items, int TotalCount, bool HasMore, string? NextCursor, int PageSize);
public sealed record AuditEmployee(int UserId, string Username, string FullName);
public sealed record AuditOptions(AuditEmployee[] Employees, string[] Actions);

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
