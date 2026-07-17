using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Tests.Wms;

public class ImportExcelServiceTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly ImportExcelService _service;

    public ImportExcelServiceTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"import-excel-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new WarehouseDbContext(options);
        _service = new ImportExcelService(_db);
        SeedMaterial("MAT-001", "Vật tư A", "Cái");
        SeedMaterial("MAT-002", "Vật tư B", "Bộ");
    }

    [Fact]
    public void GenerateTemplate_returns_xlsx_with_headers()
    {
        var bytes = _service.GenerateTemplate();
        Assert.NotEmpty(bytes);

        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        var sheet = workbook.Worksheets.First();
        Assert.Equal("Mã vật tư", sheet.Cell(1, 1).GetString());
        Assert.Equal("Số lượng", sheet.Cell(1, 4).GetString());
        Assert.Equal("Đơn giá", sheet.Cell(1, 5).GetString());
    }

    [Fact]
    public async Task ParseImportFileAsync_maps_valid_rows()
    {
        var bytes = BuildWorkbookBytes(
            ["Mã vật tư", "Số lượng", "Đơn giá"],
            ["MAT-001", "5", "100000"],
            ["MAT-002", "2", "50000"]);

        await using var stream = new MemoryStream(bytes);
        var result = await _service.ParseImportFileAsync(stream, "import.xlsx");

        Assert.Equal(2, result.ValidRowCount);
        Assert.Empty(result.Errors);
        Assert.Equal("MAT-001", result.Lines[0].MaterialCode);
        Assert.Equal(5m, result.Lines[0].Qty);
        Assert.Equal(100000m, result.Lines[0].UnitPrice);
        Assert.Equal("Vật tư A", result.Lines[0].MaterialName);
        Assert.Equal("Cái", result.Lines[0].Unit);
    }

    [Fact]
    public async Task ParseImportFileAsync_reports_unknown_material_code()
    {
        var bytes = BuildWorkbookBytes(
            ["Mã vật tư", "Số lượng", "Đơn giá"],
            ["UNKNOWN", "1", "1000"]);

        await using var stream = new MemoryStream(bytes);
        var result = await _service.ParseImportFileAsync(stream, "import.xlsx");

        Assert.Equal(0, result.ValidRowCount);
        Assert.Single(result.Errors);
        Assert.Contains("không tồn tại", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParseImportFileAsync_merges_duplicate_codes()
    {
        var bytes = BuildWorkbookBytes(
            ["Mã vật tư", "Số lượng", "Đơn giá"],
            ["MAT-001", "3", "10000"],
            ["MAT-001", "7", "10000"]);

        await using var stream = new MemoryStream(bytes);
        var result = await _service.ParseImportFileAsync(stream, "import.xlsx");

        Assert.Equal(1, result.ValidRowCount);
        Assert.Equal(10m, result.Lines[0].Qty);
    }

    private void SeedMaterial(string code, string name, string unit)
    {
        _db.Materials.Add(new Material
        {
            MaterialCode = code,
            MaterialName = name,
            Unit = unit,
            CategoryId = 1,
            IsActive = true,
        });
        _db.SaveChanges();
    }

    private static byte[] BuildWorkbookBytes(params string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
                sheet.Cell(r + 1, c + 1).Value = rows[r][c];
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
