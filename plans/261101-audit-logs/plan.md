# Admin audit history

## Requirements and reviewed scope

Admin can read, filter and inspect real PO/Inbound/Putaway audit records. No editing/deleting UI or mutation APIs. Preserve transactional PO audit writes. SerialMovement is excluded. No fake history for business workflows not implemented yet.

## Architecture

Source read endpoints in Procurement.API and Inventory.API use their own database only. WMS.API aggregates them using forwarded cookie authentication through WMS.ServiceDefaults. Identity.API supplies an admin-only employee lookup. Root owns shared WMS.Audit contracts/query/SQL reader, source registration, gateway/Identity integration, startup/proxy/solution/docs. Frontend agent owns frontend. Gateway agent owns WMS.API audit aggregator/controllers/tests, consuming root contracts.

## Contracts

Shared WMS.Audit library .NET8. Namespace WMS.Audit. Source strings `procurement`, `inventory`. Groups `PO`, `INBOUND`, `PUTAWAY`.

`AuditQuery`: properties int? UserId; string? BusinessGroup; string? Action; string? DocumentCode; DateTimeOffset? From; DateTimeOffset? To; int PageSize=20; string? Cursor. Validate size1..50, positive user, supported group, action<=60, code<=100, from<=to. From inclusive/To exclusive UTC; UI end-date conversion adds1day in Vietnam offset.

`AuditItem`: string Source, long AuditLogId, int UserId, string? ActorName, string BusinessGroup, string Action, string EntityName, string EntityId, string? DocumentCode, string Status, DateTimeOffset CreatedAt.

`AuditDetail`: same metadata via `AuditItem Item`, plus string? OldValues, string? NewValues. JSON strings may be absent/malformed; show safely without HTML. Gateway redacts password/token/secret fields recursively, do not expose internal importKey.

`AuditPage`: AuditItem[] Items, int TotalCount, bool HasMore, string? NextCursor, int PageSize.

`AuditEmployee`: int UserId, string Username, string FullName. `AuditOptions`: AuditEmployee[] Employees, string[] Actions. Groups/labels frontend static; actions option includes supported known actions plus observed actual distinct actions.

Source GET `/api/audit-logs` accepts AuditQuery and returns AuditPage. Source GET `/api/audit-logs/{id:long}` returns AuditDetail or404. Source GET `/api/audit-logs/actions` returns string[] distinct actions. All ADMIN only, GET only.

Gateway GET `/api/admin/audit-logs` accepts same query and returns AuditPage. GET `/api/admin/audit-logs/{source}/{id}` returns AuditDetail or404. GET `/api/admin/audit-logs/options` returns AuditOptions, obtains employees via Identity GET `/api/auth/audit-users` (ADMIN-only).

Cursor is base64url JSON of `AuditCursor(DateTimeOffset CreatedAt,string Source,long AuditLogId)`, static AuditCursor.Encode/Decode. Ordering globally: CreatedAt DESC, Source ordinal ASC (`inventory` before `procurement`), AuditLogId DESC. Each source applies keyset bound with same global comparator; retrieve at most pageSize+1. Gateway merges first pageSize, next cursor from last returned record only if more. Totals count filters without cursor. Frontend next/previous buttons keep cursor stack and reset on filters; no arbitrary unbounded OFFSET scan or loading all logs.

Filter DocumentCode uses current owning PO.POCode/Inbound.ReceiptCode where available, then audit JSON poCode/receiptCode fallback then EntityID. User/action/group all parameterized. Lists omit old/new JSON payloads. Audit ActorName prefers creation snapshot name in NewValues when present; gateway fills missing names from current employee lookup, fallback user ID.

## Phases

1. Shared contracts/cursor/validation/SQL reader and source read APIs.
2. Gateway aggregation, safe detail output, role protection, Identity employees.
3. Admin navigation, filters, paginated table and detail before/after comparison in existing Notion style with client-side navigation.
4. Extend launcher to WMS.API5062 and Inventory.API5159; proxy `/api/admin` to WMS.API. Keep existing ports/routes unchanged.
5. Tests: source SQL filters/pagination on generated disposable DB, gateway merge/cursor boundaries/filter forwarding/forbidden users/invalid inputs/unavailable upstream, frontend filter and paging/details/read-only UI; existing auth, PO and frontend builds.

## Review

Reviewed plan against existing split databases and AuditLog schema. Cursor approach bounds source reads and avoids incorrect merged OFFSET pagination. No write endpoints added, no live log seed, no SerialMovement access. User request authorizes feature implementation; review and tests required before reporting complete.

## Completion evidence

- Shared read-only source APIs, WMS aggregator, Identity employee lookup and Admin frontend implemented.
- Source and gateway role tests passed, including direct Procurement and Inventory denial for non-admin users and absent mutation routes.
- Disposable SQL tests passed: filters, literal wildcard search, UTC+7 boundaries, keyset timestamp ties, actor mapping, Putaway/Inbound ID collision, source JSON redaction.
- Gateway merge/pagination/filter forwarding/error tests passed. All 9 frontend tests and production build passed.
- Existing Identity regression tests passed; PO and Excel regression verification retained.
- Independent review completed; source redaction, actor name and document mapping feedback resolved.
- Launcher extended to six services, mock lifecycle tests passed. Projects/frontend/docs registered in solution; proxy added.
- Read-only live count confirmed zero audit records in ProcurementDB and InventoryDB. No fake history added. Browser visual verification not performed. NuGet audit-feed network warnings remain; builds completed without errors.
