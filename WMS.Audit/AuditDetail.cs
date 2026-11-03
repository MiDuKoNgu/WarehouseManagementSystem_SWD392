using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace WMS.Audit;
public sealed record AuditDetail(AuditItem Item, string? OldValues, string? NewValues);
