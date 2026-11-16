using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Controllers.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using System.Text;
using Xunit;

namespace NamViet.ERP.Tests.Purchasing;

public class ServiceExpenseImportExcelServiceTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly ServiceExpenseImportExcelService _service;
    private readonly ServiceExpenseItemsController _controller;

    public ServiceExpenseImportExcelServiceTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"service-import-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new WarehouseDbContext(options);
        _service = new ServiceExpenseImportExcelService(_db);
        _controller = new ServiceExpenseItemsController(_db, _service);
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

        var sheet = workbook.Worksheet("Dich vu & Chi phi");
        Assert.NotNull(sheet);

        // Verify headers
        Assert.Equal("Mã (*)", sheet.Cell(1, 1).GetString());
        Assert.Equal("Tên dịch vụ / chi phí (*)", sheet.Cell(1, 2).GetString());
        Assert.Equal("Phân loại (*) [Dịch vụ / Chi phí ngoài]", sheet.Cell(1, 3).GetString());
        Assert.Equal("Đơn vị tính (*)", sheet.Cell(1, 4).GetString());
        Assert.Equal("Đơn giá tham chiếu", sheet.Cell(1, 5).GetString());
        Assert.Equal("Ghi chú", sheet.Cell(1, 6).GetString());

        // Verify sample row 1
        Assert.Equal("DV-VC-01", sheet.Cell(2, 1).GetString());
        Assert.Equal("Vận chuyển thiết bị công trình", sheet.Cell(2, 2).GetString());
        Assert.Equal("Dịch vụ", sheet.Cell(2, 3).GetString());
        Assert.Equal("Chuyến", sheet.Cell(2, 4).GetString());
        Assert.Equal(15000000m, sheet.Cell(2, 5).GetValue<decimal>());
        Assert.Equal("Vận chuyển nội thành và liên tỉnh", sheet.Cell(2, 6).GetString());

        // Verify sample row 2
        Assert.Equal("CP-TK-01", sheet.Cell(3, 1).GetString());
        Assert.Equal("Chi phí tiếp khách và điều hành dự án", sheet.Cell(3, 2).GetString());
        Assert.Equal("Chi phí ngoài", sheet.Cell(3, 3).GetString());
        Assert.Equal("Gói", sheet.Cell(3, 4).GetString());
        Assert.Equal(5000000m, sheet.Cell(3, 5).GetValue<decimal>());
        Assert.Equal("Phê duyệt theo định mức dự toán", sheet.Cell(3, 6).GetString());

        // Verify guide sheet
        var guideSheet = workbook.Worksheet("Huong dan");
        Assert.NotNull(guideSheet);
        Assert.Contains("HƯỚNG DẪN", guideSheet.Cell(1, 1).GetString());
    }

    #endregion

    #region Parse Import File Tests

    [Fact]
    public async Task ParseImportFileAsync_ValidExcelFile_ReturnsCorrectParsedRows()
    {
        using var ms = CreateTestExcelStream(new[]
        {
            ("DV-BOC-XEP", "Bốc xếp hàng hóa tại cảng", "Dịch vụ", "Tấn", "250000", "Bốc xếp vật tư nặng"),
            ("CP-LE-PHI", "Lệ phí hải quan kiểm định", "Chi phí ngoài", "Lô", "1200000", "Lệ phí nhà nước"),
        });

        var result = await _service.ParseImportFileAsync(ms, "dich_vu.xlsx", CancellationToken.None);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ValidRows);
        Assert.Equal(0, result.ErrorRows);
        Assert.Equal(0, result.ExistingRows);

        var row1 = result.Rows[0];
        Assert.Equal("DV-BOC-XEP", row1.Code);
        Assert.Equal("Bốc xếp hàng hóa tại cảng", row1.Name);
        Assert.Equal("service", row1.Type);
        Assert.Equal("Tấn", row1.Unit);
        Assert.Equal(250000m, row1.ReferencePrice);
        Assert.True(row1.IsValid);
        Assert.Null(row1.ErrorMessage);
        Assert.False(row1.IsExisting);

        var row2 = result.Rows[1];
        Assert.Equal("CP-LE-PHI", row2.Code);
        Assert.Equal("Lệ phí hải quan kiểm định", row2.Name);
        Assert.Equal("expense", row2.Type);
        Assert.Equal("Lô", row2.Unit);
        Assert.Equal(1200000m, row2.ReferencePrice);
        Assert.True(row2.IsValid);
    }

    [Fact]
    public async Task ParseImportFileAsync_ValidCsvFile_ParsesCorrectly()
    {
        var csvContent =
            "Mã,Tên dịch vụ / chi phí,Phân loại,Đơn vị tính,Đơn giá tham chiếu,Ghi chú\n" +
            "DV-KHAO-SAT,Khảo sát địa hình mặt bằng,Dịch vụ,Gói,20000000,Khảo sát trước thi công\n" +
            "CP-CONG-TAC,Chi phí công tác kỹ sư dự án,Chi phí ngoài,Người/Ngày,1500000,Theo định mức";

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var result = await _service.ParseImportFileAsync(ms, "data.csv", CancellationToken.None);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ValidRows);
        Assert.Equal(0, result.ErrorRows);

        Assert.Equal("DV-KHAO-SAT", result.Rows[0].Code);
        Assert.Equal("service", result.Rows[0].Type);
        Assert.Equal("CP-CONG-TAC", result.Rows[1].Code);
        Assert.Equal("expense", result.Rows[1].Type);
    }

    [Fact]
    public async Task ParseImportFileAsync_IdentifiesExistingItemsInDb()
    {
        _db.ServiceExpenseItems.Add(new ServiceExpenseItem
        {
            Code = "DV-EXISTING",
            Name = "Dịch vụ đã có trong DB",
            Type = "service",
            Unit = "Lần",
            IsActive = true
        });
        await _db.SaveChangesAsync();

        using var ms = CreateTestExcelStream(new[]
        {
            ("DV-EXISTING", "Dịch vụ đã có", "Dịch vụ", "Lần", "1000000", "Đã có"),
            ("DV-NEW-ITEM", "Dịch vụ mới tinh", "Dịch vụ", "Hệ", "5000000", "Mới tinh"),
        });

        var result = await _service.ParseImportFileAsync(ms, "test.xlsx", CancellationToken.None);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ExistingRows);

        Assert.True(result.Rows[0].IsExisting);
        Assert.False(result.Rows[1].IsExisting);
    }

    [Fact]
    public async Task ParseImportFileAsync_DuplicateCodeInFile_FlagsDuplicateError()
    {
        using var ms = CreateTestExcelStream(new[]
        {
            ("DV-TRUNG", "Lần xuất hiện 1", "Dịch vụ", "Chuyến", "100000", ""),
            ("DV-TRUNG", "Lần xuất hiện 2 trùng mã", "Dịch vụ", "Chuyến", "200000", ""),
        });

        var result = await _service.ParseImportFileAsync(ms, "test.xlsx", CancellationToken.None);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ValidRows);
        Assert.Equal(1, result.ErrorRows);

        Assert.True(result.Rows[0].IsValid);
        Assert.False(result.Rows[1].IsValid);
        Assert.Contains("trùng lặp", result.Rows[1].ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParseImportFileAsync_MissingRequiredFields_FlagsValidationErrors()
    {
        using var ms = CreateTestExcelStream(new[]
        {
            ("", "Không có mã", "Dịch vụ", "Gói", "1000", ""),
            ("DV-NO-NAME", "", "Dịch vụ", "Gói", "1000", ""),
            ("DV-NO-UNIT", "Có tên nhưng không có đơn vị tính", "Dịch vụ", "", "1000", ""),
        });

        var result = await _service.ParseImportFileAsync(ms, "test.xlsx", CancellationToken.None);

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(0, result.ValidRows);
        Assert.Equal(3, result.ErrorRows);

        Assert.Contains("Mã không được để trống", result.Rows[0].ErrorMessage);
        Assert.Contains("Tên dịch vụ / chi phí không được để trống", result.Rows[1].ErrorMessage);
        Assert.Contains("Đơn vị tính không được để trống", result.Rows[2].ErrorMessage);
    }

    [Fact]
    public async Task ParseImportFileAsync_InvalidCodeFormat_FlagsError()
    {
        using var ms = CreateTestExcelStream(new[]
        {
            ("DV CO DAU CACH", "Mã có dấu cách", "Dịch vụ", "Gói", "1000", ""),
            ("DV@#$!", "Mã có ký tự lạ", "Dịch vụ", "Gói", "1000", ""),
        });

        var result = await _service.ParseImportFileAsync(ms, "test.xlsx", CancellationToken.None);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(0, result.ValidRows);
        Assert.False(result.Rows[0].IsValid);
        Assert.False(result.Rows[1].IsValid);
        Assert.Contains("ký tự đặc biệt", result.Rows[0].ErrorMessage);
    }

    [Fact]
    public async Task ParseImportFileAsync_VariousTypeFormats_NormalizedCorrectly()
    {
        using var ms = CreateTestExcelStream(new[]
        {
            ("T1", "Item 1", "DỊCH VỤ", "Cái", "100", ""),
            ("T2", "Item 2", "dich vu", "Cái", "100", ""),
            ("T3", "Item 3", "Service", "Cái", "100", ""),
            ("T4", "Item 4", "Chi phí ngoài", "Cái", "100", ""),
            ("T5", "Item 5", "CHI PHÍ", "Cái", "100", ""),
            ("T6", "Item 6", "expense", "Cái", "100", ""),
            ("T7", "Item 7", "", "Cái", "100", ""), // empty defaults to service
        });

        var result = await _service.ParseImportFileAsync(ms, "test.xlsx", CancellationToken.None);

        Assert.Equal(7, result.ValidRows);
        Assert.Equal("service", result.Rows[0].Type);
        Assert.Equal("service", result.Rows[1].Type);
        Assert.Equal("service", result.Rows[2].Type);
        Assert.Equal("expense", result.Rows[3].Type);
        Assert.Equal("expense", result.Rows[4].Type);
        Assert.Equal("expense", result.Rows[5].Type);
        Assert.Equal("service", result.Rows[6].Type);
    }

    [Fact]
    public async Task ParseImportFileAsync_InvalidPrice_FlagsError()
    {
        using var ms = CreateTestExcelStream(new[]
        {
            ("DV-AM", "Giá âm", "Dịch vụ", "Gói", "-500000", ""),
            ("DV-CHU", "Giá bằng chữ", "Dịch vụ", "Gói", "mot trieu", ""),
        });

        var result = await _service.ParseImportFileAsync(ms, "test.xlsx", CancellationToken.None);

        Assert.Equal(2, result.ErrorRows);
        Assert.Contains("lớn hơn hoặc bằng 0", result.Rows[0].ErrorMessage);
        Assert.Contains("không đúng định dạng", result.Rows[1].ErrorMessage);
    }

    [Fact]
    public async Task ParseImportFileAsync_UnsupportedExtension_ThrowsDomainRuleException()
    {
        using var ms = new MemoryStream();
        await Assert.ThrowsAsync<DomainRuleException>(() =>
            _service.ParseImportFileAsync(ms, "test.pdf", CancellationToken.None));
    }

    #endregion

    #region Import Batch Tests

    [Fact]
    public async Task ImportBatchAsync_InsertNewItems_InsertsSuccessfully()
    {
        var rows = new List<ServiceExpenseImportRowDto>
        {
            new(1, "DV-TEST-1", "Dịch vụ Test 1", "service", "Chuyến", 1000000m, "Ghi chú 1", true, null, false),
            new(2, "CP-TEST-2", "Chi phí Test 2", "expense", "Gói", 2000000m, "Ghi chú 2", true, null, false),
        };

        var request = new ServiceExpenseImportBatchRequest(rows, OverwriteExisting: false);
        var result = await _service.ImportBatchAsync(request, CancellationToken.None);

        Assert.Equal(2, result.TotalProcessed);
        Assert.Equal(2, result.InsertedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(0, result.SkippedCount);

        var item1 = await _db.ServiceExpenseItems.FirstOrDefaultAsync(s => s.Code == "DV-TEST-1");
        Assert.NotNull(item1);
        Assert.Equal("Dịch vụ Test 1", item1.Name);
        Assert.Equal("service", item1.Type);
        Assert.Equal("Chuyến", item1.Unit);
        Assert.Equal(1000000m, item1.ReferencePrice);
        Assert.True(item1.IsActive);

        var item2 = await _db.ServiceExpenseItems.FirstOrDefaultAsync(s => s.Code == "CP-TEST-2");
        Assert.NotNull(item2);
        Assert.Equal("expense", item2.Type);
    }

    [Fact]
    public async Task ImportBatchAsync_ExistingItem_OverwriteFalse_Skips()
    {
        _db.ServiceExpenseItems.Add(new ServiceExpenseItem
        {
            Code = "DV-OLD",
            Name = "Tên cũ ban đầu",
            Type = "service",
            Unit = "Lần",
            ReferencePrice = 500000m,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var rows = new List<ServiceExpenseImportRowDto>
        {
            new(1, "DV-OLD", "Tên mới cố tình sửa", "service", "Lần", 999999m, null, true, null, true),
        };

        var request = new ServiceExpenseImportBatchRequest(rows, OverwriteExisting: false);
        var result = await _service.ImportBatchAsync(request, CancellationToken.None);

        Assert.Equal(1, result.TotalProcessed);
        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.SkippedCount);

        var dbItem = await _db.ServiceExpenseItems.FirstAsync(s => s.Code == "DV-OLD");
        Assert.Equal("Tên cũ ban đầu", dbItem.Name);
        Assert.Equal(500000m, dbItem.ReferencePrice);
    }

    [Fact]
    public async Task ImportBatchAsync_ExistingItem_OverwriteTrue_UpdatesSuccessfully()
    {
        _db.ServiceExpenseItems.Add(new ServiceExpenseItem
        {
            Code = "DV-TO-UPDATE",
            Name = "Tên trước khi cập nhật",
            Type = "service",
            Unit = "Lần",
            ReferencePrice = 500000m,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var rows = new List<ServiceExpenseImportRowDto>
        {
            new(1, "DV-TO-UPDATE", "Tên sau khi cập nhật thành công", "expense", "Gói", 8500000m, "Đã cập nhật", true, null, true),
        };

        var request = new ServiceExpenseImportBatchRequest(rows, OverwriteExisting: true);
        var result = await _service.ImportBatchAsync(request, CancellationToken.None);

        Assert.Equal(1, result.TotalProcessed);
        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.SkippedCount);

        var dbItem = await _db.ServiceExpenseItems.FirstAsync(s => s.Code == "DV-TO-UPDATE");
        Assert.Equal("Tên sau khi cập nhật thành công", dbItem.Name);
        Assert.Equal("expense", dbItem.Type);
        Assert.Equal("Gói", dbItem.Unit);
        Assert.Equal(8500000m, dbItem.ReferencePrice);
        Assert.Equal("Đã cập nhật", dbItem.Description);
    }

    #endregion

    #region Controller Endpoints Integration Tests

    [Fact]
    public async Task Controller_DownloadImportTemplate_ReturnsFileResult()
    {
        var result = await _controller.DownloadImportTemplate(CancellationToken.None);
        var fileResult = Assert.IsType<FileContentResult>(result);

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
        Assert.Equal("mau-import-dich-vu-chi-phi.xlsx", fileResult.FileDownloadName);
        Assert.NotEmpty(fileResult.FileContents);
    }

    [Fact]
    public async Task Controller_ParseImportFile_EmptyFile_ReturnsBadRequest()
    {
        var result = await _controller.ParseImportFile(null, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Controller_ParseImportFile_ValidFile_ReturnsOk()
    {
        using var ms = CreateTestExcelStream(new[]
        {
            ("DV-API-1", "Dịch vụ qua API", "Dịch vụ", "Chuyến", "5000000", "Ghi chú"),
        });

        var file = new FormFile(ms, 0, ms.Length, "file", "test.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        var result = await _controller.ParseImportFile(file, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var parseResult = Assert.IsType<ServiceExpenseImportParseResultDto>(okResult.Value);

        Assert.Equal(1, parseResult.TotalRows);
        Assert.Equal(1, parseResult.ValidRows);
    }

    [Fact]
    public async Task Controller_ImportBatch_EmptyRows_ReturnsBadRequest()
    {
        var request = new ServiceExpenseImportBatchRequest([]);
        var result = await _controller.ImportBatch(request, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Controller_ImportBatch_ValidRows_ReturnsOkResult()
    {
        var rows = new List<ServiceExpenseImportRowDto>
        {
            new(1, "DV-API-BATCH", "Dịch vụ Batch", "service", "Ca", 3000000m, null, true, null, false),
        };

        var request = new ServiceExpenseImportBatchRequest(rows);
        var result = await _controller.ImportBatch(request, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var importResult = Assert.IsType<ServiceExpenseImportResultDto>(okResult.Value);

        Assert.Equal(1, importResult.InsertedCount);
    }

    #endregion

    #region Helper

    private static MemoryStream CreateTestExcelStream((string Code, string Name, string Type, string Unit, string Price, string Note)[] rows)
    {
        var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Data");

        sheet.Cell(1, 1).Value = "Mã (*)";
        sheet.Cell(1, 2).Value = "Tên dịch vụ / chi phí (*)";
        sheet.Cell(1, 3).Value = "Phân loại (*) [Dịch vụ / Chi phí ngoài]";
        sheet.Cell(1, 4).Value = "Đơn vị tính (*)";
        sheet.Cell(1, 5).Value = "Đơn giá tham chiếu";
        sheet.Cell(1, 6).Value = "Ghi chú";

        for (var i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            var rowIdx = i + 2;
            sheet.Cell(rowIdx, 1).Value = r.Code;
            sheet.Cell(rowIdx, 2).Value = r.Name;
            sheet.Cell(rowIdx, 3).Value = r.Type;
            sheet.Cell(rowIdx, 4).Value = r.Unit;
            sheet.Cell(rowIdx, 5).Value = r.Price;
            sheet.Cell(rowIdx, 6).Value = r.Note;
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    #endregion
}
