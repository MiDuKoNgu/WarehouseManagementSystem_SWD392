using System.Globalization;
using System.IO.Compression;
using System.Xml;
using ClosedXML.Excel;
using Procurement.Models.PurchaseOrders;

namespace Procurement.Excel;

public sealed class WorkbookFormatException(string message) : Exception(message);

/// <summary>Reads only the PurchaseOrder sheet; never evaluates uploaded formulas or writes a PO.</summary>
public sealed class PurchaseOrderWorkbook : IPurchaseOrderWorkbook
{
    public const int MaxFileBytes = 2 * 1024 * 1024;
    public const int MaxExpandedBytes = 20 * 1024 * 1024;
    public const int MaxRows = 500;
    private static readonly string[] Headers = ["SupplierID", "WarehouseID", "ExpectedArrivalDate", "Note", "ProductCode", "Quantity"];
    private static readonly HashSet<string> AllowedSheets = new(StringComparer.OrdinalIgnoreCase)
        { "PurchaseOrder", "Instructions", "Suppliers", "Warehouses", "Products" };

    public byte[] CreateTemplate(PurchaseOrderOptions options)
    {
        using var workbook = new XLWorkbook();
        var input = workbook.Worksheets.Add("PurchaseOrder");
        WriteHeader(input, Headers);
        input.Columns(1, 2).Width = 16;
        input.Column(3).Width = 24;
        input.Column(4).Width = 48;
        input.Column(5).Width = 28;
        input.Column(6).Width = 16;
        input.Range("A2:F2").Style.Fill.BackgroundColor = XLColor.FromHtml("#faf9f7");
        input.Range("A2:F501").Style.Font.FontColor = XLColor.FromHtml("#0075de");
        input.Range("C2:C501").Style.NumberFormat.Format = "yyyy-mm-dd";
        input.Range("D2:E501").Style.NumberFormat.Format = "@";
        input.Row(2).Height = 28;
        input.Cell("A1").GetComment().AddText("ID nhà cung cấp trong sheet Suppliers. Lặp lại cùng ID ở mọi dòng của đơn.");
        input.Cell("B1").GetComment().AddText("ID kho nhận trong sheet Warehouses. Lặp lại cùng ID ở mọi dòng của đơn.");
        input.Cell("C1").GetComment().AddText("Không bắt buộc. Dùng yyyy-MM-dd hoặc ô ngày Excel. Giữ cùng ngày ở mọi dòng.");
        input.Cell("D1").GetComment().AddText("Không bắt buộc, tối đa 1000 ký tự. Giữ cùng ghi chú ở mọi dòng.");
        input.Cell("E1").GetComment().AddText("Mã sản phẩm trong sheet Products; phải thuộc nhà cung cấp đã chọn.");
        input.Cell("F1").GetComment().AddText("Số lượng nguyên lớn hơn 0, tối đa 2147483647.");
        input.SheetView.FreezeRows(1);
        input.Range("A1:F1").SetAutoFilter();

        var suppliers = workbook.Worksheets.Add("Suppliers");
        WriteHeader(suppliers, "SupplierID", "SupplierName");
        for (var index = 0; index < options.Suppliers.Length; index++)
        {
            suppliers.Cell(index + 2, 1).Value = options.Suppliers[index].SupplierId;
            suppliers.Cell(index + 2, 2).Value = options.Suppliers[index].SupplierName;
        }
        suppliers.Column(1).Width = 16;
        suppliers.Column(2).Width = 55;

        var warehouses = workbook.Worksheets.Add("Warehouses");
        WriteHeader(warehouses, "WarehouseID", "WarehouseName", "Address");
        for (var index = 0; index < options.Warehouses.Length; index++)
        {
            var warehouse = options.Warehouses[index];
            warehouses.Cell(index + 2, 1).Value = warehouse.WarehouseId;
            warehouses.Cell(index + 2, 2).Value = warehouse.WarehouseName;
            warehouses.Cell(index + 2, 3).Value = warehouse.Address;
        }
        warehouses.Column(1).Width = 16;
        warehouses.Columns(2, 3).Width = 40;

        var products = workbook.Worksheets.Add("Products");
        WriteHeader(products, "ProductCode", "ProductName", "Unit", "SupplierIDs");
        for (var index = 0; index < options.Products.Length; index++)
        {
            var product = options.Products[index];
            products.Cell(index + 2, 1).Value = product.ProductCode;
            products.Cell(index + 2, 2).Value = product.ProductName;
            products.Cell(index + 2, 3).Value = product.Unit;
            products.Cell(index + 2, 4).Value = string.Join(", ", product.SupplierIds);
        }
        products.Column(1).Width = 28;
        products.Column(2).Width = 55;
        products.Column(3).Width = 16;
        products.Column(4).Width = 24;

        var instructions = workbook.Worksheets.Add("Instructions");
        WriteHeader(instructions, "HƯỚNG DẪN NHẬP ĐƠN MUA HÀNG");
        string[] notes = [
            "Mỗi file tạo MỘT PO ở trạng thái Draft sau khi xem trước và xác nhận. File tải mẫu chưa chứa dòng sản phẩm.",
            "Chỉ nhập dữ liệu vào sheet PurchaseOrder, từ dòng 2. Không sửa tên sheet hoặc sáu tiêu đề cột.",
            "SupplierID và WarehouseID bắt buộc, lấy từ hai sheet tham chiếu Suppliers và Warehouses.",
            "ProductCode lấy từ sheet Products. Cột SupplierIDs cho biết các nhà cung cấp hợp lệ của sản phẩm.",
            "Nhập SupplierID, WarehouseID, ngày giao dự kiến và ghi chú giống nhau cho từng dòng của một đơn.",
            "ExpectedArrivalDate có thể để trống; nếu nhập, dùng yyyy-MM-dd (ví dụ 2027-01-15) hoặc ô ngày Excel.",
            "Quantity bắt buộc là số nguyên lớn hơn 0. Không nhập cùng ProductCode hai lần, kể cả khác chữ hoa/chữ thường.",
            "Note có thể để trống, tối đa 1000 ký tự. Không dùng công thức, macro hoặc liên kết ngoài.",
            "Dòng hoàn toàn trống được bỏ qua. Dòng điền dở được báo lỗi đúng số dòng Excel.",
            "Giới hạn: .xlsx, tối đa 2 MB và 500 dòng dữ liệu. Upload chỉ xem trước, không ghi đơn cho đến khi xác nhận.",
            "Các sheet tham chiếu phản ánh dữ liệu tại lúc tải mẫu; hệ thống kiểm tra lại dữ liệu hiện tại khi xác nhận."
        ];
        instructions.Column(1).Width = 112;
        for (var index = 0; index < notes.Length; index++)
        {
            instructions.Cell(index + 2, 1).Value = notes[index];
            instructions.Cell(index + 2, 1).Style.Alignment.WrapText = true;
            instructions.Row(index + 2).Height = 36;
        }
        foreach (var worksheet in workbook.Worksheets)
        {
            worksheet.Style.Font.FontName = "Calibri";
            worksheet.Style.Font.FontSize = 11;
            worksheet.SheetView.FreezeRows(1);
        }
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    public ParsedWorkbook Parse(Stream stream) => Parse(stream, "purchase-order.xlsx");

    public ParsedWorkbook Parse(Stream stream, string fileName)
    {
        if (!string.Equals(Path.GetExtension(fileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new WorkbookFormatException("Chỉ chấp nhận file .xlsx. Vui lòng tải mẫu Excel mới.");
        using var copy = ReadBounded(stream);
        ValidatePackage(copy);
        copy.Position = 0;
        try
        {
            using var workbook = new XLWorkbook(copy);
            var input = workbook.Worksheets.FirstOrDefault(sheet => sheet.Name == "PurchaseOrder")
                ?? throw new WorkbookFormatException("Không tìm thấy sheet PurchaseOrder. Vui lòng dùng file mẫu.");
            if (workbook.Worksheets.Any(sheet => !AllowedSheets.Contains(sheet.Name) && sheet.CellsUsed(XLCellsUsedOptions.Contents).Any()))
                throw new WorkbookFormatException("File có sheet dữ liệu ngoài mẫu. Mỗi file chỉ chứa một PO trong sheet PurchaseOrder.");
            for (var column = 1; column <= Headers.Length; column++)
            {
                var cell = input.Cell(1, column);
                if (cell.HasFormula || !string.Equals(cell.GetString().Trim(), Headers[column - 1], StringComparison.OrdinalIgnoreCase))
                    throw new WorkbookFormatException($"Tiêu đề cột {column} phải là {Headers[column - 1]}. Vui lòng dùng file mẫu.");
            }
            if (input.Row(1).CellsUsed(XLCellsUsedOptions.Contents).Any(cell => cell.Address.ColumnNumber > Headers.Length))
                throw new WorkbookFormatException("Sheet PurchaseOrder chỉ được có sáu cột theo file mẫu.");

            var usedRows = input.RowsUsed(XLCellsUsedOptions.Contents)
                .Where(row => row.RowNumber() > 1 && row.CellsUsed(XLCellsUsedOptions.Contents).Any(cell => !IsBlank(cell)))
                .Take(MaxRows + 1).ToArray();
            if (usedRows.Length > MaxRows)
                throw new WorkbookFormatException($"File vượt quá {MaxRows} dòng dữ liệu. Vui lòng chia thành nhiều file.");
            var rows = new List<ImportRow>();
            foreach (var excelRow in usedRows)
            {
                var rowNumber = excelRow.RowNumber();
                var cells = Enumerable.Range(1, Headers.Length).Select(column => input.Cell(rowNumber, column)).ToArray();
                var extraValues = excelRow.CellsUsed(XLCellsUsedOptions.Contents).Any(cell => cell.Address.ColumnNumber > Headers.Length);
                if (!extraValues && cells.All(IsBlank)) continue;
                var errors = new List<string>();
                if (extraValues) errors.Add("Có dữ liệu ngoài sáu cột của mẫu.");
                for (var index = 0; index < cells.Length; index++)
                {
                    if (cells[index].HasFormula) errors.Add($"{Headers[index]}: không được dùng công thức.");
                    else if (cells[index].DataType == XLDataType.Error) errors.Add($"{Headers[index]}: ô chứa lỗi Excel.");
                }
                var supplierId = PositiveInteger(cells[0], "SupplierID", errors);
                var warehouseId = PositiveInteger(cells[1], "WarehouseID", errors);
                var arrival = ArrivalDate(cells[2], errors);
                var note = SafeText(cells[3]).Trim();
                if (note.Length > 1000) errors.Add("Note: tối đa 1000 ký tự.");
                var productCode = SafeText(cells[4]).Trim();
                if (productCode.Length == 0) errors.Add("ProductCode: bắt buộc nhập mã sản phẩm.");
                else if (productCode.Length > 40) errors.Add("ProductCode: tối đa 40 ký tự.");
                var quantity = PositiveInteger(cells[5], "Quantity", errors);
                rows.Add(new ImportRow
                {
                    RowNumber = rowNumber, SupplierId = supplierId, WarehouseId = warehouseId,
                    ExpectedArrivalDate = arrival, Note = note.Length == 0 ? null : note,
                    ProductCode = productCode, Quantity = quantity, Errors = errors
                });
            }
            foreach (var group in rows.Where(row => row.ProductCode.Length > 0).GroupBy(row => row.ProductCode, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
                foreach (var row in group) row.Errors.Add("ProductCode bị trùng trong file. Mỗi sản phẩm chỉ được có một dòng.");
            if (rows.Count > 1)
            {
                var first = rows[0];
                foreach (var row in rows.Skip(1))
                    if (row.SupplierId != first.SupplierId || row.WarehouseId != first.WarehouseId
                        || row.ExpectedArrivalDate != first.ExpectedArrivalDate || row.Note != first.Note)
                        row.Errors.Add("SupplierID, WarehouseID, ExpectedArrivalDate và Note phải giống nhau ở tất cả dòng của một PO.");
            }
            return new ParsedWorkbook(rows.ToArray(), rows.Count == 0 ? ["File chưa có dòng sản phẩm. Hãy điền sheet PurchaseOrder từ dòng 2."] : []);
        }
        catch (WorkbookFormatException) { throw; }
        catch (Exception exception) when (exception is not OutOfMemoryException and not OperationCanceledException)
        {
            throw new WorkbookFormatException("Không đọc được file Excel. File có thể bị hỏng hoặc không đúng định dạng .xlsx.");
        }
    }

    private static bool IsBlank(IXLCell cell) => !cell.HasFormula && cell.DataType != XLDataType.Error && string.IsNullOrWhiteSpace(cell.GetString());
    private static string SafeText(IXLCell cell) => cell.HasFormula || cell.DataType == XLDataType.Error ? "" : cell.GetString();

    private static int? PositiveInteger(IXLCell cell, string name, List<string> errors)
    {
        if (cell.HasFormula || cell.DataType == XLDataType.Error) return null;
        if (cell.DataType == XLDataType.Number && cell.TryGetValue<double>(out var number)
            && double.IsFinite(number) && number >= 1 && number <= int.MaxValue && Math.Truncate(number) == number)
            return (int)number;
        if (cell.DataType == XLDataType.Text && int.TryParse(cell.GetString().Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var integer) && integer > 0)
            return integer;
        errors.Add($"{name}: phải là số nguyên lớn hơn 0, tối đa {int.MaxValue}.");
        return null;
    }

    private static DateOnly? ArrivalDate(IXLCell cell, List<string> errors)
    {
        if (cell.HasFormula || cell.DataType == XLDataType.Error || IsBlank(cell)) return null;
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var date)) return DateOnly.FromDateTime(date);
        if (cell.DataType == XLDataType.Text && DateOnly.TryParseExact(cell.GetString().Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return parsed;
        errors.Add("ExpectedArrivalDate: dùng định dạng yyyy-MM-dd hoặc ô ngày Excel hợp lệ.");
        return null;
    }

    private static MemoryStream ReadBounded(Stream source)
    {
        var copy = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (copy.Length + count > MaxFileBytes)
            {
                copy.Dispose();
                throw new WorkbookFormatException("File vượt quá 2 MB.");
            }
            copy.Write(buffer, 0, count);
        }
        if (copy.Length == 0) { copy.Dispose(); throw new WorkbookFormatException("File rỗng. Vui lòng chọn file .xlsx đã điền dữ liệu."); }
        copy.Position = 0;
        return copy;
    }

    private static void ValidatePackage(Stream stream)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > 256 || archive.Entries.Select(entry => entry.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != archive.Entries.Count)
                throw new WorkbookFormatException("Cấu trúc file Excel không hợp lệ hoặc quá phức tạp.");
            long expanded = 0;
            long actualExpanded = 0;
            var buffer = new byte[81920];
            foreach (var entry in archive.Entries)
            {
                expanded += entry.Length;
                if (expanded > MaxExpandedBytes) throw new WorkbookFormatException("Dung lượng nội dung giải nén vượt giới hạn 20 MB.");
                // Verify actual decompression too; do not rely exclusively on attacker-controlled ZIP lengths.
                using (var contents = entry.Open())
                {
                    int count;
                    while ((count = contents.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        actualExpanded += count;
                        if (actualExpanded > MaxExpandedBytes)
                            throw new WorkbookFormatException("Dung lượng nội dung giải nén vượt giới hạn 20 MB.");
                    }
                }
                var name = entry.FullName;
                if (name.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || name.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase))
                    throw new WorkbookFormatException("File không được chứa macro hoặc liên kết workbook bên ngoài.");
                if (name.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                {
                    using var xmlStream = entry.Open();
                    using var reader = XmlReader.Create(xmlStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxExpandedBytes });
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element) continue;
                        if (reader.GetAttribute("TargetMode") == "External"
                            || reader.GetAttribute("ContentType")?.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase) == true)
                            throw new WorkbookFormatException("File không được chứa macro hoặc liên kết ngoài.");
                    }
                }
            }
            if (archive.GetEntry("[Content_Types].xml") is null || archive.GetEntry("xl/workbook.xml") is null)
                throw new WorkbookFormatException("File không phải workbook .xlsx hợp lệ.");
        }
        catch (WorkbookFormatException) { throw; }
        catch (Exception exception) when (exception is InvalidDataException or IOException or XmlException)
        {
            throw new WorkbookFormatException("File không phải .xlsx hợp lệ hoặc đã bị hỏng.");
        }
    }

    private static void WriteHeader(IXLWorksheet sheet, params string[] values)
    {
        for (var index = 0; index < values.Length; index++) sheet.Cell(1, index + 1).Value = values[index];
        var header = sheet.Range(1, 1, 1, values.Length);
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#f0efed");
        header.Style.Font.FontColor = XLColor.FromHtml("#31302e");
        header.Style.Font.Bold = true;
        header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        header.Style.Border.BottomBorderColor = XLColor.FromHtml("#e6e6e6");
        sheet.Row(1).Height = 30;
    }
}
