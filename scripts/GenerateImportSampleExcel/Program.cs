using ClosedXML.Excel;

var headers = new[] { "Mã vật tư", "Tên vật tư", "Đơn vị tính", "Số lượng", "Đơn giá" };
var rows = new (string Code, string Name, string Unit, decimal Qty, decimal Price)[]
{
    ("VT-DEMO-0001", "Thép phi 12", "Kg", 25, 18500),
    ("VT-DEMO-0002", "Xi măng PCB40", "Bao", 10, 95000),
    ("VT-DEMO-0003", "Ống PVC D63", "M", 50, 42000),
    ("VT-DEMO-0006", "Gạch ống 4 lỗ", "Cái", 200, 3500),
    ("VT-DEMO-0010", "Keo dán gạch", "Bao", 5, 125000),
};

var outDir = args.Length > 0
    ? args[0]
    : Path.Combine(
        Directory.GetCurrentDirectory(),
        "..",
        "..",
        "..",
        "WareHouse_Management",
        "samples");

Directory.CreateDirectory(outDir);
var outPath = Path.Combine(outDir, "phieu-nhap-import-thu.xlsx");

using (var workbook = new XLWorkbook())
{
    var sheet = workbook.Worksheets.Add("Phieu nhap");

    for (var col = 0; col < headers.Length; col++)
    {
        var cell = sheet.Cell(1, col + 1);
        cell.Value = headers[col];
        cell.Style.Font.Bold = true;
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
        cell.Style.Font.FontColor = XLColor.White;
    }

    for (var r = 0; r < rows.Length; r++)
    {
        var row = rows[r];
        var excelRow = r + 2;
        sheet.Cell(excelRow, 1).Value = row.Code;
        sheet.Cell(excelRow, 2).Value = row.Name;
        sheet.Cell(excelRow, 3).Value = row.Unit;
        sheet.Cell(excelRow, 4).Value = row.Qty;
        sheet.Cell(excelRow, 5).Value = row.Price;
    }

    sheet.Columns().AdjustToContents();
    sheet.SheetView.FreezeRows(1);
    workbook.SaveAs(outPath);
}

Console.WriteLine($"Created: {Path.GetFullPath(outPath)}");
