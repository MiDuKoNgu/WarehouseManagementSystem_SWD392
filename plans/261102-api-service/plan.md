---
title: Route frontend API traffic through WMS API Service
status: completed
priority: P1
effort: medium
tags: [gateway, architecture, authentication]
created: 2026-11-02
---

# WMS API Service implementation

## Scope and acceptance

WMS.API becomes the common frontend API entry point. Preserve existing service URLs,
payloads, role checks, CSRF, cookies, Excel uploads/downloads and audit aggregation.
Frontend uses one `/api` proxy to port 5062. Explicit gateway routes use configured
service origins; unknown routes return 404. Proxy requests do not run WMS's audit
session lookup, so Identity still owns login/logout/session behavior. All business
rules, SQL schemas, cross-service calls and frontend package layout stay unchanged.

## Reviewed approach

Use YARP direct forwarding for HTTP method/path/query/body/header/response transport.
Map auth/workspaces to Identity, products/catalog to Catalog, warehouses to Warehouse,
purchase-orders/receiving-orders to Procurement and inbounds/inventory/putaway to Inventory.
Keep `/api/admin/audit-logs` in WMS with its existing authorization pipeline.
Do not introduce a blanket `/api` fallback or arbitrary user-supplied destinations.
Plan reviewed against current imports, Vite routes, API controllers, shared session
middleware and audit tests; separate planning review confirmed routing coverage and
the need to forward before WMS's audit authentication middleware.

## Work

- [x] Inspect routing, auth, existing tests, launch script and documentation.
- [x] Review concrete implementation plan and compatibility constraints.
- [x] Implement proxy, service configuration and single frontend API target.
- [x] Verify forwarding, cookies/CSRF, files, failures and audit authorization.
- [x] Complete independent code review and resolve unknown-route authentication issue.
- [x] Run existing Identity, audit and frontend regressions; frontend production build.
- [x] Complete solution build verification.
- [x] Update architecture/run instructions and record verification evidence.

## Validation

Use local test servers/fake repositories only; no production database writes.
Check all route prefixes, encoded queries, mutation bodies, multipart uploads, binary
downloads, independent client cookies, stale-session logout, service errors and
unknown prefixes. Existing audit tests verify local admin authorization is unchanged.

## Evidence and sync-back

- Gateway transport and real Identity integration through WMS passed after the final
  routing fix, using loopback upstreams and a fake account repository.
- Existing Identity authentication regression and audit gateway regression passed.
- Frontend: 79 tests across 11 files passed; TypeScript/Vite production build passed
  with existing React Router `use client` bundling warnings.
- Full solution build passed with zero warnings and zero errors, including the new
  gateway test project. Architecture/run documentation and journal are complete.
- Target plan folder was scanned: no `phase-*.md` files exist; this checklist is the
  authoritative scope. No unrelated plans were modified.
- Detailed outcomes: [verification report](reports/verification.md).
