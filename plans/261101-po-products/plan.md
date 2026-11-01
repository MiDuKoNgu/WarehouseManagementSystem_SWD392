# Complete PO workflow and purchasing product management

## Reviewed scope and explicit decisions

User authorized complete PO workflow and create/edit/list/search/filter/detail Product for Purchasing Staff. User explicitly confirmed cancellation/short-close: Purchasing cancels only when no Inbound exists; closes CONFIRMED PO only when no open Inbound; unreceived quantity becomes ClosedQuantity; both require reason. No Inbound creation or Putaway implementation in this round.

Keep current React Router, Notion styling, .NET8 split databases, cookie session + CSRF, existing accounts/warehouses/products. No product delete or category/brand/supplier CRUD requested. Product dimensions stay neutral existing fields; no assumption about packaging/stacking. ProductCode immutable after creation to preserve identifiers.

## PO

States normalized in responses and new writes: DRAFT, CONFIRMED, CANCELLED, CLOSED. Existing Draft reads/guards case-insensitively; no destructive migration of old logs. Edit only DRAFT; confirm revalidates active supplier/warehouse/product association, requires lines; CONFIRMED cannot edit. Cancel DRAFT/CONFIRMED only if no Inbound ever exists. Close CONFIRMED only with no open Inbound, computes each ClosedQuantity = OrderedQuantity - distinct valid received serial count; reject inconsistent over-receipt/mismatched lines. Reason 1–1000 chars for cancel/close, schema CloseReason/ClosedAt reused for terminal transition metadata.

Use RowVersion optimistic concurrency (base64 token) on updates/transitions, SQL transaction/UPDLOCK to avoid double transition; record UPDATE_PO/CONFIRM_PO/CANCEL_PO/CLOSE_PO with actor and old/new in same transaction. Snapshot createdByName must not be reused as editor; use performedByName. List/detail preserve existing contracts plus version/transition fields/line closed+received quantities. Add frontend edit page/actions, confirmation/reason dialogs, status filters/search in PO list where practical. Existing manual/import still work and DRAFT is not CONFIRMED. No price fields or approval role.

Inventory readonly integration GET `/api/inventory/purchase-orders/{id}/receiving-summary`, authenticated PURCHASING_STAFF: `{purchaseOrderId,hasAnyInbound,openInboundCount,lines:[{poLineId,receivedQuantity}]}`. Open = any status other than COMPLETED or CANCELLED; completed serials from IsActive=1 InboundItems + existing SerialItem, count distinct SerialItemID by line where completed status/time. Future Inbound mutation implementation must coordinate PO state/receiving guards to prevent cross-service races; current round adds no Inbound writes.

PO API: PUT `/api/purchase-orders/{id}` `{...createFields,rowVersion}`; POST `/{id}/confirm` `{rowVersion}`; POST `/{id}/cancel` and `/{id}/close` `{rowVersion,reason}`. Detail adds rowVersion, confirmedAt, closedAt, closeReason; lines add closedQuantity/receivedQuantity/remainingQuantity. Backend/ frontend agents coordinate exact optional record fields to preserve old tests.

## Product contract (root backend, frontend agent)

Purchasing-only `/api/products`:

- GET `?search=&categoryId=&brandId=&supplierId=&status=&page=1&pageSize=20` => `{items:ProductSummary[],totalCount,page,pageSize}` (max50 rows; validated positive paging/IDs, search<=100). Summary includes productId,productCode,productName,model,unit,categoryId,categoryName,brandId,brandName,status.
- GET `/options` => `{categories:[{categoryId,categoryName}],brands:[{brandId,brandName}],suppliers:[{supplierId,supplierName}]}`. Categories existing, brands/suppliers ACTIVE.
- GET `/{id}` => ProductDetail = summary fields + specifications,lengthCm,widthCm,heightCm,grossWeightKg,suppliers:[{supplierId,supplierName}],createdByUserId,createdAt,updatedAt,rowVersion(base64).
- POST => detail201; PUT `/{id}` => detail200. JSON `{productCode,productName,model,unit,categoryId,brandId,status,specifications,lengthCm,widthCm,heightCm,grossWeightKg,supplierIds:number[],rowVersion?:string}`.
- Code ASCII letters/numbers plus dot/underscore/slash/hyphen,1–40 chars, trim+uppercase; unique. Name<=200, model<=100,unit<=30 required; specs optional<=10000; status ACTIVE/INACTIVE; at least1 distinct active supplier; positive decimal dimensions within decimal(10,2), weight decimal(10,3), reject excess precision. Product creator from server session. Update validates RowVersion and rejects code change. Product and SupplierProduct saved together transaction. Conflict409, validation400, notfound404; all writes CSRF. No product delete endpoint.

Root registers Catalog BLL/DAL/services and adds Inventory summary. No Catalog AuditLog table added: current requested Admin audit scope stays PO/Inbound/Putaway; no invented cross-service audit write.

## Execution and verification

1. PO backend agent owns Procurement Models/BLL/DAL/API and tests, consuming Inventory summary.
2. Frontend agent owns PO changes and new Product screens/navigation/tests.
3. Root owns Catalog product backend, inventory summary, docs/solution/integration and tests.
4. Independent reviewer checks state transitions/audit/concurrency/product association/roles; fix findings.
5. API and SQL tests on disposable DB verify rollback, rowversion conflict, immutable code, duplicate product, state guards, received/closed maths. Existing audit/auth/PO Excel regression tests + frontend tests/build. No live business data changes needed for implementation.

Reviewed against actual schema (Product.RowVersion, PO.RowVersion, ConfirmedAt, ClosedAt, CloseReason, ClosedQuantity exist). Authorization for rules comes from user confirmation this turn. Keep current data and source service boundaries.
