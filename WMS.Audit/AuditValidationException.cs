using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace WMS.Audit;

public sealed class AuditValidationException(string[] errors) : Exception("Bộ lọc lịch sử không hợp lệ.")
{
    public string[] Errors { get; } = errors;
}
