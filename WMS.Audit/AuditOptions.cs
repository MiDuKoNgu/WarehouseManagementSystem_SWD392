using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace WMS.Audit;
public sealed record AuditOptions(AuditEmployee[] Employees, string[] Actions);
