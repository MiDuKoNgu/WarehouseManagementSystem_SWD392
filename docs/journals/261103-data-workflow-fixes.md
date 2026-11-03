---
date: 2026-11-03
scope: Data loading and existing business workflows
status: verified
---

# Restore existing data workflows

## Context and changes

Login, warehouse-dependent PO options and audit history failed at different layers.
The Identity lookup filtered a positional record projection that EF could not
translate; filtering entities before projection fixes username and ID lookups.
Warehouse's two public DbContext constructors made factory activation ambiguous;
the options constructor now explicitly participates in dependency injection.
Parameterized audit SQL treated the literal JSON braces as format placeholders;
the CTE now constructs the same empty object with `NCHAR(123) + NCHAR(125)`.

Frontend fixes finish loading after successful login/logout and a failed login
that superseded the initial session check, retry the selected PO
preview as well as the eligible-order list, and retain ASP.NET field validation
messages alongside business validation arrays. Regression tests cover each case.
Catalog's test executable now selects its default entry point while retaining the
profile-only test mode.

VS Code now exposes `EWMS: Stop backend` for orphaned launcher processes. The stop
script matches the quoted backend DLL paths from this checkout and ignores other
projects, suffix matches and unavailable command lines; a mocked process test
checks that scope without stopping real processes.

Inventory's conditional counts for current and draft receipt quantities generated
invalid `COUNT(NULL)` SQL. Conditional sums now express those quantities correctly.
Rollback fixtures use CHECK constraints instead of triggers so EF's OUTPUT clause
does not reject the fixture before the intended failing write is exercised.

## Verification

- Frontend: 85 tests across 12 files and production build passed, including a
  stale failed-login check that cannot end a newer session refresh.
- All existing backend suites passed. The final Debug solution build completed
  with zero warnings and zero errors.
- Identity, Catalog, Audit, Procurement and Inbound SQL fixtures passed against
  disposable databases; Putaway SQL verification passed 102 assertions.
- Catalog storage profiles and Warehouse layouts passed reads against the actual
  source databases, including warehouse scope from the authenticated claim.
- Read-only checks of actual ProcurementDB and InventoryDB passed for five POs,
  two warehouses and four receipts: lists, details, items, summaries and pending
  Putaway data.
- Old backend processes were stopped and all six services restarted through
  `start-ewms.ps1 -WindowsAuthentication -SkipBuild` after verifying Windows SQL
  authentication. Gateway CSRF, Vite login and proxied CSRF requests returned 200.
- A real login request through Vite and WMS with valid CSRF and a randomly named
  nonexistent account returned the expected 401 after the SQL lookup, not 503.

Production business data was not modified by these checks. Temporary SQL databases
were owned by their test runs; checks against existing source databases used reads only.

## Remaining work and reflection

The verified fixes restore existing implemented workflows. Dashboard cards or
sections without implemented APIs and workflows remain unimplemented features;
this work does not claim to complete them. The running services now use the rebuilt
assemblies; future rebuilds require restarting the affected backend processes.

Fake repository tests alone missed runtime factory activation and SQL translation
failures. Keep the no-connection compilation/factory regressions and the optional
SQL integration checks so those failures are exercised directly.
