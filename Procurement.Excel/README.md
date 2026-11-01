# Purchase order Excel template and parser

`PurchaseOrderWorkbook` implements `IPurchaseOrderWorkbook` from Procurement.Models. The library only creates or reads workbooks; it never accesses a database, creates a PO or evaluates an uploaded formula.

## Workbook format

The `PurchaseOrder` sheet has six headers in this order: `SupplierID`, `WarehouseID`, `ExpectedArrivalDate`, `Note`, `ProductCode`, `Quantity`. Enter data from row 2. Each file represents one PO. Supplier, receiving warehouse, expected date and note must be identical on every product row. Expected date and note are optional. A date may be ISO `yyyy-MM-dd` text or a real Excel date cell.

The generated template starts with an empty input row and includes current supplier, warehouse and product reference sheets plus Vietnamese instructions. Reference sheets are informational only; the backend validates current master data again before saving.

## Validation boundary

- `Parse(Stream)` returns original Excel row numbers and row-local errors for missing fields, integer IDs/quantities, invalid dates, long notes/codes, formulas, Excel error cells, duplicate products and mixed PO headers.
- Fully empty/whitespace rows are ignored. Maximum 500 data rows, including partially filled rows.
- `WorkbookFormatException` signals file-level issues: wrong extension (using filename overload), empty/corrupt workbook, wrong sheet/headers, unexpected data sheets or upload limits.
- Input is limited to 2 MB. ZIP entries are checked for both declared and actual expanded size (20 MB cumulative), a maximum of 256 entries and duplicate names before ClosedXML loads the workbook.
- Macro packages, external workbook parts and external relationships are rejected.
- Supplier/product validity, supplier associations, warehouse activity and date business rules belong to Procurement.BLL; this parser only validates workbook content.

## Run focused checks

From the solution directory:

```powershell
dotnet run --project ./tests/Procurement.Excel.Tests
```

Tests create workbooks in memory and do not read or modify SQL data.
