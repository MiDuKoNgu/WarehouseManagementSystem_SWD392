using Microsoft.AspNetCore.Mvc;
using WMS.Audit;

namespace Inventory.API.Controllers;

[Route("api/audit-logs")]
public sealed class AuditLogsController(SqlAuditReader reader) : AuditReadControllerBase(reader);
