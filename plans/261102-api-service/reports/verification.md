---
date: 2026-11-02
status: completed
scope: frontend-api-entry-point
---

# API Service verification

## Summary

WMS.API now provides the shared entry point for existing frontend API families.
This changes request routing, not business rules, SQL schemas, cross-service
repository access or frontend package organization.

## Findings

| Check | Result | Evidence |
|---|---|---|
| Gateway integration | PASS | `dotnet run --project tests/WMS.ApiService.Tests/WMS.ApiService.Tests.csproj --no-restore --no-launch-profile -p:UseSharedCompilation=false -m:1` |
| Existing Identity authentication | PASS | Regression validation agent ran existing test project |
| Existing audit gateway | PASS | Rerun after explicit routing and unknown-API handling |
| Frontend | PASS | `npm.cmd test`: 11 files, 79 tests; `npm.cmd run build`: TypeScript and Vite passed |
| Full solution build | PASS | `dotnet build WarehouseManagementSystem_SWD392.slnx --no-restore --nologo -m:1 -p:UseSharedCompilation=false`: zero warnings/errors |
| Independent review | Addressed | Unknown API routes with stale cookies now return 404 before session validation |
| Documentation | Complete | API Service architecture, frontend/run instructions and journal updated |

Gateway tests exercise all 10 exact and nested route families, encoded serial query
strings, repeated query parameters, POST/PUT/DELETE JSON, multipart Excel upload,
binary download and filename, multiple Set-Cookie headers, independent clients,
CSRF/session header forwarding, upstream error status/body/Retry-After, redirects,
offline-upstream 502 JSON and unknown paths.

Real Identity controller integration uses a fake user repository behind a loopback
adapter. It verifies CSRF issuance, invalid credentials, successful login, remember-me
cookie attributes, `/me`, workspace role restrictions, logout during user lookup
failure, RowVersion revocation and stale-session logout. No SQL connections were made.

Unknown `/api/products-other`, `/api/admin/unknown`, `/api/admin/audit-logs/unknown`
and `/api/unknown` return 404 with a stale session cookie and zero calls to an
unavailable session service. A valid local audit route still calls that service,
preserving its authorization pipeline.

## Recommendations and limits

- Restart WMS.API and Vite to load the new routing configuration.
- Production deployment still needs private domain-service networking and suitable
  HTTPS or trusted forwarded-header configuration; these tests do not prove network isolation.
- No live SQL/browser business workflow was exercised. Transport, real Identity
  controllers with fake persistence, existing regressions and builds provide the evidence.
- Frontend bundling emits React Router `use client` directive warnings; TypeScript
  and production bundling still complete successfully.
- This implementation does not claim the entire backend architecture matches every
  layer shown in the original diagram.

## Unresolved checks

No checks pending within the routing scope. Live SQL/browser deployment validation
and production network isolation were not part of this verification.
