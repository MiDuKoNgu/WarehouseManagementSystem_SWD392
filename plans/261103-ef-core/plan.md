# EF Core migration

Status: in progress

Reviewed implementation plan: retain .NET 8, existing database schema, DTOs and HTTP contracts. Map service-owned entities from the existing SQL schema and additive scripts. Use EF Core SQL Server with per-operation contexts, explicit transactions and rowversion concurrency. Retain parameterized SQL through EF for SQL Server locks and cross-database read projections where needed.

- [ ] Catalog and Identity entities, contexts, repositories and seed utility
- [ ] Procurement entities, context, purchase orders and reservation transactions
- [ ] Warehouse entities/context and audit reader
- [ ] Inventory entities/context, inbound, receiving and putaway transactions
- [ ] Build, existing regression suites, SQL integration checks and mapping verification
- [ ] Architecture documentation and final review

No automatic schema migration, database recreation, or production seed execution is part of this change.
