# Inbound verification

```powershell
dotnet run --project tests/Inventory.Inbound.Tests
$env:EWMS_INBOUND_TEST_SERVER = 'MIDUKONGU'
dotnet run --project tests/Inventory.Inbound.Tests
Remove-Item Env:EWMS_INBOUND_TEST_SERVER
```

Without the variable, runs rules, API role/warehouse/CSRF checks and Procurement client forwarding/error tests. With it, also creates a uniquely named `EWMS_InboundTests_<guid>` database, installs the reduced existing schema and the actual Inbound migration twice, tests repository transactions and concurrency, then removes only that generated database. Requires Windows Authentication with permission to create a temporary database. No application databases are modified.

SQL cases cover multi-batch receipt, draft reservations and over-receipt, duplicate serials and concurrent requests, row versions, edit/remove/cancel rules, completion audit failure rollback, stock/source/movement/audit creation, damaged quarantine, pending Putaway reads, and failed-creation token fencing.

Camera/device/OCR tests belong to the frontend suite; these backend tests do not claim physical-device verification.

## Putaway verification

```powershell
$env:EWMS_INBOUND_TEST_SERVER = 'localhost'
$env:EWMS_PUTAWAY_TEST_SERVER = 'localhost'
dotnet run --project tests/Inventory.Inbound.Tests -c Release -p:UseSharedCompilation=false
Remove-Item Env:EWMS_INBOUND_TEST_SERVER
Remove-Item Env:EWMS_PUTAWAY_TEST_SERVER
```

Putaway adds pure rules and HTTP security checks to the same executable. The second variable enables SQL concurrency,
capacity, two-warehouse/six-profile, immutable-quantity and movement/audit rollback checks in three GUID-named
`EWMS_PutawayTests_<guid>_*` fixture databases. Only these generated databases are created, seeded and removed;
the application databases and supplied DAMAGED UPDATE script are never changed by these tests.
Both SQL variables use Windows Authentication and require permission to create disposable databases.
Release avoids DLL locks when the user's Debug API hosts are already running.

See [Putaway implementation and manual checks](../../docs/putaway.md) for API examples and SSMS/React/Swagger steps.
