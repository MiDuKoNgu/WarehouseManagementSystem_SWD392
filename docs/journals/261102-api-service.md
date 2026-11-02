---
date: 2026-11-02
scope: WMS API Service gateway
---

# Route frontend requests through WMS.API

## Context

The architecture places API Service between the browser and business services.
Vite previously forwarded each API family directly to its business service, while
WMS.API only aggregated admin audit history.

## What changed

WMS.API uses YARP direct forwarding for explicit API prefixes and configured service
origins. Vite now targets WMS.API for `/api`. Local admin audit routes retain their
existing aggregation and authorization. Domain services continue owning business
rules, roles and CSRF validation.

## Decisions and review

Forwarding runs before WMS's audit session authentication so login, CSRF issuance
and logout keep Identity's deliberate behavior, including logout when user lookup
is unavailable. HTTP methods, bodies, cookies, CSRF, files and response statuses are
passed through. Unknown API paths return 404 before session validation; review caught
the stale-cookie failure case and integration tests now cover it.

## Verification and reflection

Gateway tests passed against loopback upstreams and real Identity controllers with
fake persistence. Existing Identity and audit regressions passed; 79 frontend tests
and production build passed. The complete solution built with zero warnings/errors.
No SQL/business data was modified. Details are in the
[verification report](../../plans/261102-api-service/reports/verification.md).

This resolves the common API entry-point mismatch. It does not refactor existing
business-layer boundaries or cross-database Putaway reads. Deployment must separately
control backend exposure and HTTPS/proxy settings.
