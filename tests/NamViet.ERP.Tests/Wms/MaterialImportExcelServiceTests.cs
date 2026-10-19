using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using Xunit;

namespace NamViet.ERP.Tests.Wms;

public class MaterialImportExcelServiceTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly MaterialImportExcelService _service;

    public MaterialImportExcelServiceTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"material-import-excel-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new WarehouseDbContext(options);
        _service = new MaterialImportExcelService(_db);

        // Seed initial categories
        var catL1 = new MaterialCategory { Id = 101, Code = "CAT-METALS", Name = "Kim khí & Inox", Level = 1, IsActive = true };
        var catL2 = new MaterialCategory { Id = 102, Code = "CAT-INOX", Name = "Inox 304", Level = 2, ParentId = 101, IsActive = true };
        _db.MaterialCategories.AddRange(catL1, catL2);

        // Seed initial material
        _db.Materials.Add(new Material
        {
            MaterialCode = "IN-EXISTING-01",
            MaterialName = "Inox cũ",
            CategoryId = 102,
            Unit = "Mét",
            IsActive = true
        });

        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GenerateTemplateAsync_returns_xlsx_with_sheets_and_headers()
    {
        var bytes = await _service.GenerateTemplateAsync();
        Assert.NotEmpty(bytes);

        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        Assert.Contains("Danh muc vat tu", workbook.Worksheets.Select(w => w.Name));
        Assert.Contains("Huong dan & Tra cuu", workbook.Worksheets.Select(w => w.Name));

        var sheet = workbook.Worksheet("Danh muc vat tu");
        Assert.Equal("Mã vật tư (*)", sheet.Cell(1, 1).GetString());
        Assert.Equal("Tên vật tư (*)", sheet.Cell(1, 2).GetString());
        Assert.Equal("Đơn vị tính (*)", sheet.Cell(1, 3).GetString());
        Assert.Equal("Giá trần (VNĐ)", sheet.Cell(1, 13).GetString());
        Assert.True(sheet.Cell(1, 14).IsEmpty());

        // Đảm bảo không chứa 2 cột không có trên FE
        var allHeaders = sheet.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Equal(13, allHeaders.Count);
        Assert.DoesNotContain("Phân loại ABC", string.Join(" ", allHeaders));
        Assert.DoesNotContain("Tiêu hao", string.Join(" ", allHeaders));
    }

    [Fact]
    public async Task ParseImportFileAsync_detects_valid_and_duplicate_rows()
    {
        var bytes = BuildWorkbookBytes(
            ["Mã vật tư", "Tên vật tư", "Đơn vị tính", "Danh mục", "Nhóm vật tư", "Xuất xứ", "Giá trần"],
            ["IN-NEW-01", "Inox 304 Tấm", "Tấm", "Kim khí & Inox", "Inox 304", "Nhật Bản", "250000"],
            ["IN-EXISTING-01", "Inox 304 Cập nhật", "Mét", "Kim khí & Inox", "Inox 304", "Việt Nam", "180000"],
            ["", "Vật tư không mã", "Cái", "Kim khí & Inox", "Inox 304", "Việt Nam", "50000"]);

        await using var stream = new MemoryStream(bytes);
        var result = await _service.ParseImportFileAsync(stream, "test_materials.xlsx");

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(2, result.ValidRowCount);
        Assert.Equal(1, result.ExistingRowCount); // IN-EXISTING-01
        Assert.Equal(1, result.ErrorRowCount);    // Missing code

        var validRow = result.Rows.First(r => r.MaterialCode == "IN-NEW-01");
        Assert.True(validRow.IsValid);
        Assert.False(validRow.IsExisting);
        Assert.Equal("Inox 304 Tấm", validRow.MaterialName);
        Assert.Equal("Tấm", validRow.Unit);
        Assert.Equal(250000m, validRow.CeilingPrice);

        var existingRow = result.Rows.First(r => r.MaterialCode == "IN-EXISTING-01");
        Assert.True(existingRow.IsValid);
        Assert.True(existingRow.IsExisting);
    }

    [Fact]
    public async Task ImportBatchAsync_inserts_and_auto_creates_category_branch()
    {
        var request = new MaterialImportBatchRequest(
            [
                new MaterialImportRowDto(
                    2,
                    "TH-NEW-AUTO-CAT",
                    "Thép tròn trơn D20",
                    "Cây",
                    "Hệ thép công trình", // Cấp 1 mới
                    "Thép xây dựng",       // Cấp 2 mới
                    "Thép tròn",           // Cấp 3 mới
                    null,
                    null,
                    "Hòa Phát",
                    "D20 x 6000mm",
                    null,
                    "TCVN 1651",
                    320000m,
                    "A",
                    false,
                    true,
                    false,
                    true,
                    [],
                    []
                )
            ],
            OverwriteExisting: false
        );

        var result = await _service.ImportBatchAsync(request);

        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Contains("TH-NEW-AUTO-CAT", result.ImportedCodes);

        // Verify in DB
        var savedMat = await _db.Materials.FirstOrDefaultAsync(m => m.MaterialCode == "TH-NEW-AUTO-CAT");
        Assert.NotNull(savedMat);
        Assert.Equal("Thép tròn trơn D20", savedMat.MaterialName);
        Assert.Equal("Cây", savedMat.Unit);
        Assert.Equal("Hòa Phát", savedMat.Origin);

        // Verify Category hierarchy was automatically created
        var cat = await _db.MaterialCategories.FindAsync(savedMat.CategoryId);
        Assert.NotNull(cat);
        Assert.Equal("Thép tròn", cat.Name);
        Assert.Equal(3, cat.Level);

        var parentL2 = await _db.MaterialCategories.FindAsync(cat.ParentId);
        Assert.NotNull(parentL2);
        Assert.Equal("Thép xây dựng", parentL2.Name);
        Assert.Equal(2, parentL2.Level);

        var parentL1 = await _db.MaterialCategories.FindAsync(parentL2.ParentId);
        Assert.NotNull(parentL1);
        Assert.Equal("Hệ thép công trình", parentL1.Name);
        Assert.Equal(1, parentL1.Level);
    }

    [Fact]
    public async Task ImportBatchAsync_respects_overwrite_flag()
    {
        var requestWithoutOverwrite = new MaterialImportBatchRequest(
            [
                new MaterialImportRowDto(
                    2,
                    "IN-EXISTING-01",
                    "Tên mới ghi đè",
                    "Bộ",
                    "Kim khí & Inox",
                    "Inox 304",
                    null, null, null, null, null, null, null, null, null, false, true, true, false, [], []
                )
            ],
            OverwriteExisting: false
        );

        var resultSkip = await _service.ImportBatchAsync(requestWithoutOverwrite);
        Assert.Equal(0, resultSkip.CreatedCount);
        Assert.Equal(0, resultSkip.UpdatedCount);
        Assert.Equal(1, resultSkip.SkippedCount);

        var unchangedMat = await _db.Materials.FindAsync("IN-EXISTING-01");
        Assert.Equal("Inox cũ", unchangedMat!.MaterialName);

        // Now with OverwriteExisting = true
        var requestWithOverwrite = requestWithoutOverwrite with { OverwriteExisting = true };
        var resultOverwrite = await _service.ImportBatchAsync(requestWithOverwrite);
        Assert.Equal(0, resultOverwrite.CreatedCount);
        Assert.Equal(1, resultOverwrite.UpdatedCount);
        Assert.Equal(0, resultOverwrite.SkippedCount);

        var updatedMat = await _db.Materials.FindAsync("IN-EXISTING-01");
        Assert.Equal("Tên mới ghi đè", updatedMat!.MaterialName);
        Assert.Equal("Bộ", updatedMat.Unit);
    }

    [Fact]
    public async Task ParseImportFileAsync_parses_company_multi_row_headers_with_origin_and_specs()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("FINAL");

        // Row 1: Header
        ws.Cell(1, 1).Value = "STT";
        ws.Cell(1, 2).Value = "Danh mục";
        ws.Cell(1, 3).Value = "Hạng mục";
        ws.Cell(1, 4).Value = "Quy cách";
        ws.Cell(1, 5).Value = "Độ dày";
        ws.Cell(1, 6).Value = "Chiều dài";
        ws.Cell(1, 7).Value = "Xuất xứ";
        ws.Cell(1, 8).Value = "Nước SX";
        ws.Cell(1, 9).Value = "Nhóm nước";
        ws.Cell(1, 10).Value = "Mô tả";
        ws.Cell(1, 11).Value = "MÃ HÀNG HÓA";
        ws.Cell(1, 12).Value = "TÊN HÀNG HÓA";

        // Row 2: Subheader F-codes
        ws.Cell(2, 11).Value = "F1-F2-F3-F4-F5-F6-F7-F8";

        // Row 3: Subheader Steel & Origin
        ws.Cell(3, 4).Value = "phi/chiều rộng";
        ws.Cell(3, 5).Value = "độ dày";
        ws.Cell(3, 6).Value = "chiều dài";
        ws.Cell(3, 7).Value = "NHÃN HIỆU";
        ws.Cell(3, 8).Value = "NƯỚC SẢN XUẤT";
        ws.Cell(3, 9).Value = "NHÓM NƯỚC";

        // Row 4: Subheader Equipment
        ws.Cell(4, 4).Value = "Công suất kw";
        ws.Cell(4, 5).Value = "Lưu lượng m3/h";
        ws.Cell(4, 6).Value = "cột áp Pa";

        // Row 5: Data Pump/Fan
        ws.Cell(5, 1).Value = "1";
        ws.Cell(5, 2).Value = "Động cơ";
        ws.Cell(5, 3).Value = "Hệ quạt";
        ws.Cell(5, 4).Value = "160kw";
        ws.Cell(5, 5).Value = "71500 m3/h";
        ws.Cell(5, 6).Value = "5600Pa";
        ws.Cell(5, 7).Value = "SYSTEM FAN";
        ws.Cell(5, 8).Value = "VIỆT NAM";
        ws.Cell(5, 9).Value = "ĐNA";
        ws.Cell(5, 10).Value = "Quạt hút ly tâm";
        ws.Cell(5, 11).Value = "ĐC-QAT-H-SS400-160kw";
        ws.Cell(5, 12).Value = "Quạt hút SS400 160kw";

        // Row 6: Data Steel
        ws.Cell(6, 1).Value = "2";
        ws.Cell(6, 2).Value = "Nguyên vật liệu";
        ws.Cell(6, 3).Value = "Thép ống đúc";
        ws.Cell(6, 4).Value = "Ø355";
        ws.Cell(6, 5).Value = "9";
        ws.Cell(6, 6).Value = "6000";
        ws.Cell(6, 7).Value = "POSCO";
        ws.Cell(6, 8).Value = "HÀN QUỐC";
        ws.Cell(6, 9).Value = "G7";
        ws.Cell(6, 11).Value = "TH-DUC-SA106-Ø355";
        ws.Cell(6, 12).Value = "Ống thép đúc SA106 Ø355";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var result = await _service.ParseImportFileAsync(ms, "SC-MZ-MA-VATTU.xlsx");

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ValidRowCount);

        var fanRow = result.Rows.First(r => r.MaterialCode == "ĐC-QAT-H-SS400-160kw");
        Assert.Equal("SYSTEM FAN • VIỆT NAM (ĐNA)", fanRow.Origin);
        Assert.Contains("160kw", fanRow.GeneralTechnicalSpec);
        Assert.Contains("71500", fanRow.GeneralTechnicalSpec);
        Assert.Equal("Bộ", fanRow.Unit);

        var steelRow = result.Rows.First(r => r.MaterialCode == "TH-DUC-SA106-Ø355");
        Assert.Equal("POSCO • HÀN QUỐC (G7)", steelRow.Origin);
        Assert.Equal("Ø355x9x6000mm", steelRow.TechnicalSpec);
        Assert.Equal("Cây", steelRow.Unit);
    }

    [Fact]
    public async Task ParseImportFileAsync_parses_real_company_excel_file_if_present()
    {
        var realPath = @"D:\WorkspaceRoot\NamViet\Copy of SC-MZ-MA VAT TU.xlsx";
        if (!File.Exists(realPath)) return;

        await using var stream = File.OpenRead(realPath);
        var result = await _service.ParseImportFileAsync(stream, Path.GetFileName(realPath));

        Assert.NotNull(result);
        Assert.True(result.TotalRows > 0, "Real file must have rows");
        Assert.True(result.ValidRowCount > 0, "Real file must have valid rows");

        // Verify that parsed rows have properly formatted origins and specs
        var validRowsWithOrigin = result.Rows.Where(r => r.IsValid && !string.IsNullOrWhiteSpace(r.Origin)).ToList();
        Assert.NotEmpty(validRowsWithOrigin);
    }

    private static byte[] BuildWorkbookBytes(string[] headers, params string[][] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        for (var r = 0; r < rows.Length; r++)
        {
            var row = rows[r];
            for (var c = 0; c < row.Length; c++)
                ws.Cell(r + 2, c + 1).Value = row[c];
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
