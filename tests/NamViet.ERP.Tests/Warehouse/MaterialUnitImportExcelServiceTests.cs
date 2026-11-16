using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Controllers.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using System.Text;
using Xunit;

namespace NamViet.ERP.Tests.Warehouse;

public class MaterialUnitImportExcelServiceTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly MaterialUnitImportExcelService _service;
    private readonly MaterialUnitsController _controller;

    public MaterialUnitImportExcelServiceTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"material-unit-import-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new WarehouseDbContext(options);
        _service = new MaterialUnitImportExcelService(_db);
        _controller = new MaterialUnitsController(_db, _service);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Template Generation Tests

    [Fact]
    public async Task GenerateTemplateAsync_GeneratesValidExcelWithHeadersAndSampleRows()
    {
        var bytes = await _service.GenerateTemplateAsync(CancellationToken.None);
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);

        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);

        var sheet = workbook.Worksheet("Don vi tinh");
        Assert.NotNull(sheet);

        Assert.Equal("Tên đơn vị tính (*)", sheet.Cell(1, 1).GetString());
        Assert.Equal("Thứ tự sắp xếp", sheet.Cell(1, 2).GetString());
        Assert.Equal("Trạng thái hoạt động", sheet.Cell(1, 3).GetString());
        Assert.Equal("Ghi chú", sheet.Cell(1, 4).GetString());

        // Check sample rows
        Assert.Equal("Bộ", sheet.Cell(2, 1).GetString());
        Assert.Equal("Mét dài", sheet.Cell(3, 1).GetString());

        var guideSheet = workbook.Worksheet("Huong dan");
        Assert.NotNull(guideSheet);
        Assert.Contains("HƯỚNG DẪN", guideSheet.Cell(1, 1).GetString());
    }

    #endregion

    #region Parse Import File Tests

    [Fact]
    public async Task ParseImportFileAsync_ValidExcel_ReturnsParsedRows()
    {
        using var ms = CreateTestExcelStream(new[]
        {
            ("Thùng", "10", "Bật", "Thùng carton 5 lớp"),
            ("Can", "20", "Tắt", "Can nhựa 20 lít"),
        });

        var result = await _service.ParseImportFileAsync(ms, "units.xlsx", CancellationToken.None);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ValidRowCount);
        Assert.Equal(0, result.ErrorRowCount);

        var r1 = result.Rows[0];
        Assert.Equal("Thùng", r1.Name);
        Assert.Equal(10, r1.SortOrder);
        Assert.True(r1.IsActive);
        Assert.True(r1.IsValid);

        var r2 = result.Rows[1];
        Assert.Equal("Can", r2.Name);
        Assert.Equal(20, r2.SortOrder);
        Assert.False(r2.IsActive);
        Assert.True(r2.IsValid);
    }

    [Fact]
    public async Task ParseImportFileAsync_DuplicateNamesInFile_MarksDuplicateAsInvalid()
    {
        using var ms = CreateTestExcelStream(new[]
        {
            ("Kiện", "1", "Bật", "Kiện hàng"),
            ("kiện", "2", "Bật", "Kiện hàng trùng"),
        });

        var result = await _service.ParseImportFileAsync(ms, "units.xlsx", CancellationToken.None);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ValidRowCount);
        Assert.Equal(1, result.ErrorRowCount);
        Assert.False(result.Rows[1].IsValid);
        Assert.Contains("trùng lặp", result.Rows[1].ErrorMessage);
    }

    [Fact]
    public async Task ParseImportFileAsync_ExistingInDb_MarksIsExistingTrue()
    {
        _db.MaterialUnits.Add(new MaterialUnit
        {
            Name = "Mét vuông",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        using var ms = CreateTestExcelStream(new[]
        {
            ("mét vuông", "1", "Bật", "Đã có trong DB"),
            ("Gram", "2", "Bật", "Chưa có trong DB"),
        });

        var result = await _service.ParseImportFileAsync(ms, "units.xlsx", CancellationToken.None);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ExistingRowCount);

        Assert.True(result.Rows[0].IsExisting);
        Assert.True(result.Rows[0].IsValid);
        Assert.False(result.Rows[1].IsExisting);
    }

    #endregion

    #region Import Batch Tests

    [Fact]
    public async Task ImportBatchAsync_InsertsNewUnitsAndRespectsOverwrite()
    {
        _db.MaterialUnits.Add(new MaterialUnit
        {
            Name = "Bao",
            SortOrder = 5,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var batchReq = new MaterialUnitImportBatchRequest(
            OverwriteExisting: true,
            Rows: new List<MaterialUnitImportRowDto>
            {
                new(RowNumber: 1, Name: "Bao", SortOrder: 99, IsActive: true, Note: "Cập nhật", IsValid: true, ErrorMessage: null, IsExisting: true),
                new(RowNumber: 2, Name: "Pallet", SortOrder: 15, IsActive: true, Note: "Mới", IsValid: true, ErrorMessage: null, IsExisting: false),
            });

        var res = await _service.ImportBatchAsync(batchReq, CancellationToken.None);

        Assert.Equal(1, res.CreatedCount);
        Assert.Equal(1, res.UpdatedCount);
        Assert.Equal(0, res.SkippedCount);

        var updatedBao = await _db.MaterialUnits.FirstAsync(u => u.Name == "Bao");
        Assert.True(updatedBao.IsActive);
        Assert.Equal(99, updatedBao.SortOrder);

        var newPallet = await _db.MaterialUnits.FirstAsync(u => u.Name == "Pallet");
        Assert.True(newPallet.IsActive);
        Assert.Equal(15, newPallet.SortOrder);
    }

    [Fact]
    public async Task ImportBatchAsync_WithoutOverwrite_SkipsExisting()
    {
        _db.MaterialUnits.Add(new MaterialUnit
        {
            Name = "Ống",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var batchReq = new MaterialUnitImportBatchRequest(
            OverwriteExisting: false,
            Rows: new List<MaterialUnitImportRowDto>
            {
                new(RowNumber: 1, Name: "Ống", SortOrder: 50, IsActive: false, Note: null, IsValid: true, ErrorMessage: null, IsExisting: true),
            });

        var res = await _service.ImportBatchAsync(batchReq, CancellationToken.None);

        Assert.Equal(0, res.CreatedCount);
        Assert.Equal(0, res.UpdatedCount);
        Assert.Equal(1, res.SkippedCount);

        var ong = await _db.MaterialUnits.FirstAsync(u => u.Name == "Ống");
        Assert.True(ong.IsActive);
        Assert.Equal(1, ong.SortOrder);
    }

    #endregion

    #region Controller Endpoints Tests

    [Fact]
    public async Task Controller_DownloadTemplate_ReturnsExcelFile()
    {
        var actionResult = await _controller.DownloadImportTemplate(CancellationToken.None);
        var fileResult = Assert.IsType<FileContentResult>(actionResult);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
        Assert.Equal("mau-import-don-vi-tinh.xlsx", fileResult.FileDownloadName);
        Assert.NotEmpty(fileResult.FileContents);
    }

    [Fact]
    public async Task Controller_ParseImportFile_ReturnsOkWithParsedDto()
    {
        using var ms = CreateTestExcelStream(new[]
        {
            ("Chai", "1", "Bật", "Chai thủy tinh"),
        });

        var fileMock = new FormFile(ms, 0, ms.Length, "file", "units.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        var actionResult = await _controller.ParseImportFile(fileMock, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var parsed = Assert.IsType<MaterialUnitImportParseResultDto>(okResult.Value);
        Assert.Equal(1, parsed.TotalRows);
        Assert.Equal(1, parsed.ValidRowCount);
        Assert.Equal("Chai", parsed.Rows[0].Name);
    }

    #endregion

    private static MemoryStream CreateTestExcelStream(IEnumerable<(string Name, string SortOrder, string Status, string Note)> rows)
    {
        var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Don vi tinh");
        sheet.Cell(1, 1).Value = "Tên đơn vị tính (*)";
        sheet.Cell(1, 2).Value = "Thứ tự sắp xếp";
        sheet.Cell(1, 3).Value = "Trạng thái hoạt động";
        sheet.Cell(1, 4).Value = "Ghi chú";

        var rowIdx = 2;
        foreach (var r in rows)
        {
            sheet.Cell(rowIdx, 1).Value = r.Name;
            sheet.Cell(rowIdx, 2).Value = r.SortOrder;
            sheet.Cell(rowIdx, 3).Value = r.Status;
            sheet.Cell(rowIdx, 4).Value = r.Note;
            rowIdx++;
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }
}
