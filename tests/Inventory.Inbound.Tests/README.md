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
