---
title: Inbound management
status: completed
priority: P1
effort: large
branch: workspace-without-git
tags: [inventory, inbound, procurement]
created: 2026-11-01
---
# Inbound implementation

## Reviewed scope

Complete existing .NET8 / React EWMS receiving from CONFIRMED PO, warehouse scoped cookie sessions and CSRF. Follow supplied prompt plus original business document and downloaded SQL schema; no PO-free inbound, expiry rules, internal serial generation, or full Putaway placement workflow. Deliver list/create/detail, manual/camera/image barcode+OCR confirmed serial input, month/year, draft edit/remove, atomic completion, audit, pending-putaway read endpoint. Preserve existing PO/product/auth/audit flows.

## Acceptance and design review

- Warehouse staff only, assigned warehouse enforced server-side. No purchasing mutations granted.
- DRAFT writes require header RowVersion; serialize Inventory writes to protect cross-receipt serial uniqueness and quantity reservations. Completed count recognizes DONE and historical COMPLETED.
- Completion creates SerialItem, links receipt items, grouped SupplySource and PURCHASE_RECEIPT movements, DONE metadata and audit in one Inventory transaction. Pending means IN_WAREHOUSE with NULL location, following existing schema; Putaway must move existing serials without creating quantities.
- Procurement durable reservation created under PO lock before Inventory receipt prevents close/cancel races; reserve/release bumps PO version to invalidate stale received-quantity snapshots. Retain retry token on client. Failed creation leaves recoverable reservation, never automatically releases pending work.
- Additive SQL migrations only. No destructive data edits or fake legacy metadata. Applied both migrations locally; reconciliation uses owning PO warehouse mapping, unknown historical input method remains UNKNOWN.
- Camera and image processing run client-side; explicit candidate confirmation, no OCR autosave. Lazy load scanner/OCR.
- User confirmed: only one DRAFT per PO, refuse severe damage, accept minor dents into quarantine with description, allow draft cancellation with reason. Config false/true/true enabled.
- SQL integrated authentication works outside sandbox. Real SQL integration tests passed on disposable databases; both local business migrations applied without deleting data.

## Work

- [x] Read prompt, original business requirements, schema, existing contracts and workflows.
- [x] Review implementation boundaries and concurrency design.
- [x] Implement repository/migration and procurement coordination.
- [x] Implement API orchestration and pending Putaway query.
- [x] Implement frontend/manual/camera/image/OCR.
- [x] Run backend/frontend tests and builds; review regressions.
- [x] Document migrations, use, verification and remaining environment limits.

## Verification and device follow-up

Implementation and automated checks complete. Solution build 0 warnings/errors; frontend41 tests and production build pass; Inbound/Procurement SQL rollback/concurrency tests pass on MIDUKONGU disposable databases. Identity, Excel, Audit regressions pass. Eight Chrome desktop/mobile mock-API page checks found no horizontal overflow or JS errors. All plan tasks are in this file; no phase files require sync-back.

Known preexisting Catalog test project references missing SqlProductTests and cannot compile; it is not in the solution and no Catalog product business code was changed. Physical mobile camera over HTTPS and real-label OCR quality still require device validation. OCR assets need internet. See reports/verification.md and docs/inbound.md for full evidence, limitations and usage.
