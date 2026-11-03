using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace WMS.Audit;
public sealed record AuditPage(AuditItem[] Items, int TotalCount, bool HasMore, string? NextCursor, int PageSize);
