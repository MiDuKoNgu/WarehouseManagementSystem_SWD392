# Purchase orders: manual draft and Excel import

## Context and scope

React/TypeScript frontend and .NET 8 services. Identity authentication already works. SQL databases exist; Catalog contains no master data; Warehouse contains Kho A and Kho B. Keep identity accounts and existing warehouses unchanged.

## Reviewed implementation plan

1. Keep business databases independent: Catalog.API owns lookup reads from CatalogDB, Warehouse.API owns WarehouseDB lookup reads, Procurement.API owns ProcurementDB transactions. Business APIs authenticate the existing cookie by validating it with Identity.API; mutations validate CSRF with Identity.API. No shared browser tokens or trusted user-ID headers.
2. Add idempotent SQL seed for a few suppliers, brands, categories, products and SupplierProduct associations. Product creator is an existing ACTIVE ADMIN. Execute only the seed; do not recreate schema or accounts.
3. Procurement API provides options, list, create Draft, detail, Excel template, preview and confirm. Validate supplier/warehouse ACTIVE, supplier-product association, positive integer quantities, duplicate lines, date/length limits. Derive creator from authenticated claims.
4. Single workbook = one PO. Sheet PurchaseOrder has SupplierID, WarehouseID, ExpectedArrivalDate (yyyy-MM-dd, optional), Note, ProductCode, Quantity. All nonempty lines must share header values. Fully blank rows ignored; partially filled rows receive errors. Formula cells rejected, .xlsx only, maximum 2MB/500 data rows. IDs and allowed product codes documented in generated reference sheets. Preview never writes. Confirmation revalidates and rejects tampering through normal server validation; file/preview retry uses a user-bound single-use token to prevent duplicate PO writes.
5. SQL transaction saves header, all lines and AuditLog (CREATE_PO or IMPORT_PO_EXCEL) together. POCode generated server-side with unique suffix, Status Draft, ClosedQuantity 0. No approval/confirmation/receipt workflow or price fields this round.
6. Frontend purchasing area includes list, create, detail and Excel preview/errors/confirm in existing Notion style. No fake statistics or dead enabled actions. Retain login, logout and role protection.
7. Test business validation, spreadsheet parser, preview-no-write, unauthorized/wrong-role/CSRF, transaction rollback on audit failure, idempotent confirmation and existing auth regression. Verify frontend and all changed backend builds. Link source files/projects into solution and document launch commands.

## API contract

Base `/api/purchase-orders`, only PURCHASING_STAFF.

- GET `/options` => `{ suppliers:[{supplierId,supplierName}], warehouses:[{warehouseId,warehouseName,address}], products:[{productId,productCode,productName,unit,supplierIds:number[]}] }`
- GET `/` => array of `{purchaseOrderId,poCode,supplierId,supplierName,warehouseId,warehouseName,orderDate,expectedArrivalDate,status,createdAt,lineCount,totalQuantity}`
- POST `/` JSON `{supplierId,warehouseId,expectedArrivalDate:null|string,note:null|string,lines:[{productId,quantity}]}` => detail, 201.
- GET `/{id}` => header above + `{createdByUserId,createdByName,note,lines:[{poLineId,productId,productCode,productName,unit,quantity}]}`. ISO dates, long IDs within JS safe range for current demo.
- GET `/import/template` => xlsx attachment.
- POST `/import/preview` multipart `file` => `{token:string|null,valid:boolean,rows:[{rowNumber,supplierId?,warehouseId?,expectedArrivalDate?,note?,productCode,productName?,quantity?,errors:string[]}],errors:string[]}`. Token only when valid, scoped to user, expires after 15 min. Display format issues with 400 `{message}`; row errors return 200 preview invalid.
- POST `/import/confirm` JSON `{token}` => created detail. Reuse returns existing result or 409; no duplicate PO. Expired/unknown token => 409.
- Validation errors => 400 `{message,errors?:string[]}`; 401/403 role/session; 503 service/DB failure. POSTs require fresh X-CSRF-TOKEN from Identity `/api/auth/csrf`.

## Review

Reviewed against schema and user workflow: no price column introduced; one supplier and receiving warehouse per PO; supplier association enforced; cross-service IDs validated via API; audit transaction is mandatory; Excel confirmation is distinct from preview. Authorized implementation under current user request. Functional validation and independent code review remain required before completion.

## Completion evidence

- Implemented manual/list/detail and Excel template/preview/confirm, purchasing-only UI and APIs.
- Independent review completed; syntax/expiry and preview-note feedback resolved.
- Procurement API/business tests passed; 15 Excel parser scenarios passed.
- SQL integration passed on an isolated generated database: audit failure rolls back header/lines/audit, success commits all, same import retry creates one PO/audit only.
- Authentication regression and cross-service CSRF validation tests passed. Frontend TypeScript/build passed.
- Seeded 2 suppliers, 3 brands, 3 categories, 6 products plus associations. No live PO created; original accounts/warehouses retained.
- No visual browser verification performed. NuGet vulnerability-feed lookup generated NU1900 network warnings, builds otherwise pass.
