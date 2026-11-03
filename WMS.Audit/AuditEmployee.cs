using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace WMS.Audit;
public sealed record AuditEmployee(int UserId, string Username, string FullName);
