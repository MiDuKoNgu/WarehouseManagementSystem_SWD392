using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace WMS.Audit;

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
