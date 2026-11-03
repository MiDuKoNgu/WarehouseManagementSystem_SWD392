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

The suite always checks SQL translation for real receipt list/detail, serial, progress and pending Putaway queries without opening SQL connections. The SQL fixture also checks progress when no current receipt is selected, including reserved quantities from other drafts.

To additionally sample existing business data using read-only repository calls:

```powershell
$env:EWMS_DATA_READ_TEST_SERVER = 'MIDUKONGU'
dotnet run --project tests/Inventory.Inbound.Tests --no-restore -c Release
dotnet run --project tests/Procurement.Tests --no-restore -c Release
Remove-Item Env:EWMS_DATA_READ_TEST_SERVER
```

This option reads `InventoryDB` receipt lists, sampled details/items/receiving summaries and pending stock, plus `ProcurementDB` orders/lines and sampled details. It performs no business database writes. Disposable SQL fixtures remain separately controlled by the variables above and below.

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
