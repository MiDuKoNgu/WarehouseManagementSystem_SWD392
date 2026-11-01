using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;
using Procurement.Excel;
using Procurement.Models.PurchaseOrders;

var parser = new PurchaseOrderWorkbook();
var references = new PurchaseOrderOptions([new(11, "Nhà cung cấp thử")], [new(22, "Kho thử", "Hà Nội")],
    [new(33, "TEST-001", "Màn hình thử", "Cái", [11]), new(34, "TEST-002", "Bàn phím thử", "Cái", [11])]);
var count = 0;

Test("template has blank input and populated references", () =>
{
    var bytes = parser.CreateTemplate(references);
    using var workbook = new XLWorkbook(new MemoryStream(bytes));
    Check(workbook.Worksheet("PurchaseOrder").Cell("A2").IsEmpty(), "input row must be empty");
    Check(workbook.Worksheet("Suppliers").Cell("A2").GetValue<int>() == 11, "supplier reference missing");
    Check(workbook.Worksheet("Products").Cell("A2").GetString() == "TEST-001", "product reference missing");
    Check(parser.Parse(new MemoryStream(bytes)).Errors.Length == 1, "empty template should return a useful error");
});
Test("valid ISO and Excel dates, preserving row number and blank gaps", () =>
{
    var result = Parse(workbook =>
    {
        Fill(workbook, 2, "TEST-001", 4);
        Fill(workbook, 6, "TEST-002", 8);
        workbook.Worksheet("PurchaseOrder").Cell("C6").Value = new DateTime(2027, 1, 15);
    });
    Check(result.Errors.Length == 0 && result.Rows.All(row => row.Errors.Count == 0), "valid rows should have no errors: " + string.Join("; ", result.Rows.SelectMany(row => row.Errors)));
    Check(result.Rows.Length == 2 && result.Rows[1].RowNumber == 6, "blank row gaps must preserve Excel line numbers");
    Check(result.Rows[0].ExpectedArrivalDate == new DateOnly(2027, 1, 15), "ISO date parsed incorrectly");
});
Test("blank and whitespace-only rows skipped", () =>
{
    var result = Parse(workbook =>
    {
        Fill(workbook, 2, "TEST-001", 3);
        workbook.Worksheet("PurchaseOrder").Cell("A3").Value = "   ";
    });
    Check(result.Rows.Length == 1, "whitespace row should not create an error row");
});
Test("incomplete rows and noninteger quantities reported on row", () =>
{
    var result = Parse(workbook => workbook.Worksheet("PurchaseOrder").Cell("F8").Value = 1.5);
    Check(result.Rows.Single().RowNumber == 8 && result.Rows[0].Errors.Count >= 4, "incomplete row errors missing");
});
Test("negative zero excessive and text quantities rejected", () =>
{
    foreach (var number in new[] { 0d, -2d, 2147483648d })
    {
        var result = Parse(workbook => Fill(workbook, 2, "TEST-001", number));
        Check(result.Rows[0].Errors.Any(error => error.Contains("Quantity")), "invalid quantity accepted");
    }
    var text = Parse(workbook => { Fill(workbook, 2, "TEST-001", 1); workbook.Worksheet("PurchaseOrder").Cell("F2").Value = "abc"; });
    Check(text.Rows[0].Errors.Any(error => error.Contains("Quantity")), "text quantity accepted");
});
Test("case-insensitive duplicate products flag each row", () =>
{
    var result = Parse(workbook => { Fill(workbook, 2, "TEST-001", 2); Fill(workbook, 3, "test-001", 5); });
    Check(result.Rows.All(row => row.Errors.Any(error => error.Contains("trùng"))), "every duplicate row must be marked");
});
Test("mixed PO headers flagged", () =>
{
    var result = Parse(workbook =>
    {
        Fill(workbook, 2, "TEST-001", 2); Fill(workbook, 3, "TEST-002", 5);
        workbook.Worksheet("PurchaseOrder").Cell("B3").Value = 99;
    });
    Check(result.Rows[1].Errors.Any(error => error.Contains("giống nhau")), "multiple headers accepted");
});
Test("formula rejected without evaluating", () =>
{
    var result = Parse(workbook => { Fill(workbook, 2, "TEST-001", 2); workbook.Worksheet("PurchaseOrder").Cell("F2").FormulaA1 = "1/0"; });
    Check(result.Rows[0].Errors.Any(error => error.Contains("công thức")), "formula accepted");
});
Test("invalid date and long note rejected", () =>
{
    var result = Parse(workbook =>
    {
        Fill(workbook, 2, "TEST-001", 2);
        workbook.Worksheet("PurchaseOrder").Cell("C2").Value = "15/01/2027";
        workbook.Worksheet("PurchaseOrder").Cell("D2").Value = new string('a', 1001);
    });
    Check(result.Rows[0].Errors.Any(error => error.Contains("ExpectedArrivalDate")), "ambiguous date accepted");
    Check(result.Rows[0].Errors.Any(error => error.Contains("Note")), "long note accepted");
});
Test("headers and extra sheets rejected", () =>
{
    FormatFailure(() => Parse(workbook => workbook.Worksheet("PurchaseOrder").Cell("A1").Value = "Supplier"));
    FormatFailure(() => Parse(workbook => workbook.Worksheets.Add("AnotherPO").Cell("A1").Value = "Unexpected"));
});
Test("500 row limit enforced", () =>
{
    FormatFailure(() => Parse(workbook => { for (var row = 2; row <= 502; row++) Fill(workbook, row, $"P{row}", 1); }));
});
Test("500 valid rows accepted and whitespace gaps do not count", () =>
{
    var result = Parse(workbook => { for (var row = 2; row <= 501; row++) Fill(workbook, row, $"P{row}", 1); });
    Check(result.Rows.Length == 500 && result.Rows.All(row => row.Errors.Count == 0), "500 rows should be accepted");
    var sparse = Parse(workbook =>
    {
        Fill(workbook, 2, "TEST-001", 1);
        for (var row = 3; row <= 600; row++) workbook.Worksheet("PurchaseOrder").Cell(row, 1).Value = " ";
    });
    Check(sparse.Rows.Length == 1, "fully blank rows should not count toward 500 limit");
});
Test("optional date and note can be empty", () =>
{
    var result = Parse(workbook =>
    {
        Fill(workbook, 2, "TEST-001", 1);
        workbook.Worksheet("PurchaseOrder").Cell("C2").Clear();
        workbook.Worksheet("PurchaseOrder").Cell("D2").Clear();
    });
    Check(result.Rows[0].Errors.Count == 0 && result.Rows[0].ExpectedArrivalDate is null && result.Rows[0].Note is null, "optional fields rejected");
});
Test("extension, empty, corrupt and file byte limit enforced", () =>
{
    FormatFailure(() => parser.Parse(new MemoryStream(parser.CreateTemplate(references)), "file.xls"));
    FormatFailure(() => parser.Parse(new MemoryStream()));
    FormatFailure(() => parser.Parse(new MemoryStream(Encoding.UTF8.GetBytes("not an excel workbook"))));
    FormatFailure(() => parser.Parse(new MemoryStream(new byte[PurchaseOrderWorkbook.MaxFileBytes + 1])));
});
Test("expanded ZIP limit, macros, external links and duplicate entries rejected", () =>
{
    FormatFailure(() => parser.Parse(Package(archive =>
    {
        using var entry = archive.CreateEntry("large.xml", CompressionLevel.Optimal).Open();
        var buffer = new byte[81920];
        for (var index = 0; index <= PurchaseOrderWorkbook.MaxExpandedBytes / buffer.Length; index++) entry.Write(buffer);
    })));
    FormatFailure(() => parser.Parse(Package(archive => archive.CreateEntry("xl/vbaProject.bin"))));
    FormatFailure(() => parser.Parse(Package(archive => archive.CreateEntry("xl/externalLinks/externalLink1.xml"))));
    FormatFailure(() => parser.Parse(Package(archive =>
    {
        var rels = archive.CreateEntry("xl/_rels/workbook.xml.rels");
        using var writer = new StreamWriter(rels.Open());
        writer.Write("<Relationships><Relationship TargetMode=\"External\" Target=\"https://example.test\" /></Relationships>");
    })));
    FormatFailure(() => parser.Parse(Package(archive => { archive.CreateEntry("same.xml"); archive.CreateEntry("same.xml"); })));
    FormatFailure(() => parser.Parse(Package(archive => { for (var index = 0; index < 257; index++) archive.CreateEntry($"item-{index}.xml"); })));
    FormatFailure(() => parser.Parse(Package(archive =>
    {
        using (var writer = new StreamWriter(archive.CreateEntry("[Content_Types].xml").Open())) writer.Write("<Types />");
        using (var writer = new StreamWriter(archive.CreateEntry("xl/workbook.xml").Open())) writer.Write("<broken");
    })));
});
Console.WriteLine($"PASS: {count} Excel template/parser scenarios. In-memory workbooks only; no SQL data modified.");

ParsedWorkbook Parse(Action<XLWorkbook> modify)
{
    using var workbook = new XLWorkbook(new MemoryStream(parser.CreateTemplate(references)));
    modify(workbook);
    using var output = new MemoryStream();
    workbook.SaveAs(output, new SaveOptions { EvaluateFormulasBeforeSaving = false });
    output.Position = 0;
    return parser.Parse(output);
}
void Fill(XLWorkbook workbook, int row, string product, double quantity)
{
    var sheet = workbook.Worksheet("PurchaseOrder");
    sheet.Cell(row, 1).Value = 11;
    sheet.Cell(row, 2).Value = 22;
    sheet.Cell(row, 3).Value = "2027-01-15";
    sheet.Cell(row, 4).Value = "Ghi chú thử";
    sheet.Cell(row, 5).Value = product;
    sheet.Cell(row, 6).Value = quantity;
}
void Test(string name, Action action) { try { action(); count++; } catch (Exception exception) { throw new Exception($"FAIL {name}: {exception.Message}", exception); } }
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void FormatFailure(Action action)
{
    try { action(); } catch (WorkbookFormatException) { return; }
    throw new Exception("Expected WorkbookFormatException.");
}
MemoryStream Package(Action<ZipArchive> modify)
{
    var output = new MemoryStream();
    using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true)) modify(archive);
    output.Position = 0;
    return output;
}
