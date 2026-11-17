using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;
using System.Security.Claims;
using Xunit;

namespace NamViet.ERP.Tests.Purchasing;

public class SupplierExcelImportTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly FakeSupplierGroupService _groupsService;
    private readonly TestCurrentUser _currentUser;
    private readonly SuppliersController _controller;

    public SupplierExcelImportTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);

        _groupsService = new FakeSupplierGroupService();
        _currentUser = new TestCurrentUser
        {
            Id = Guid.NewGuid(),
            Name = "Chuyên viên Mua hàng",
            Email = "buyer@mayzer.com"
        };

        _controller = new SuppliersController(_db, _groupsService, _currentUser);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private static string? FindTestExcelPath(string fileName)
    {
        var current = AppContext.BaseDirectory;
        // 1. Prioritize Fixtures directory first (hermetic test data)
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(current, "Fixtures", fileName);
            if (File.Exists(candidate) && ValidateSupplierFile(candidate)) return candidate;

            var candidateInTests = Path.Combine(current, "tests", "NamViet.ERP.Tests", "Fixtures", fileName);
            if (File.Exists(candidateInTests) && ValidateSupplierFile(candidateInTests)) return candidateInTests;

            var candidateInBeTests = Path.Combine(current, "WareHouse_Management_BE", "tests", "NamViet.ERP.Tests", "Fixtures", fileName);
            if (File.Exists(candidateInBeTests) && ValidateSupplierFile(candidateInBeTests)) return candidateInBeTests;

            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        // 2. Fallback to Test directory if valid and not locked/single-sheet
        current = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(current, "Test", fileName);
            if (File.Exists(candidate) && ValidateSupplierFile(candidate)) return candidate;

            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        // 3. Fallback to any Fixtures file found regardless of validation
        current = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(current, "Fixtures", fileName);
            if (File.Exists(candidate)) return candidate;

            var candidateInTests = Path.Combine(current, "tests", "NamViet.ERP.Tests", "Fixtures", fileName);
            if (File.Exists(candidateInTests)) return candidateInTests;

            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        return null;
    }

    private static bool ValidateSupplierFile(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var wb = new XLWorkbook(fs);
            return wb.Worksheets.Any(w => string.Equals(w.Name.Trim(), "SC-MÃ NCC-OPTION 3", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static FormFile CreateFormFileFromDisk(string path)
    {
        var ms = new MemoryStream();
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            fs.CopyTo(ms);
        }
        ms.Position = 0;
        return new FormFile(ms, 0, ms.Length, "file", Path.GetFileName(path))
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }

    [Fact]
    public async Task PreviewExcel_WithRealFile_Option3Sheet_ExtractsAccurateSupplierCodesAndNames()
    {
        // Arrange
        var filePath = FindTestExcelPath("261111-BẢN ĐẨY LÊN APP-FINAL5.xlsx");
        Assert.NotNull(filePath);
        var formFile = CreateFormFileFromDisk(filePath);

        // Act
        var result = await _controller.PreviewExcel(formFile, "SC-MÃ NCC-OPTION 3", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var preview = Assert.IsType<SupplierImportPreviewResponseDto>(okResult.Value);

        Assert.Equal("SC-MÃ NCC-OPTION 3", preview.SelectedSheet);
        Assert.NotNull(preview.AvailableSheets);
        Assert.Contains("SC-MÃ NCC-OPTION 3", preview.AvailableSheets);
        Assert.Contains("TỔNG HỢP MÃ NCC", preview.AvailableSheets);

        Assert.Equal(35, preview.TotalRows);
        Assert.Equal(35, preview.ValidRows);
        Assert.Equal(0, preview.ErrorRows);

        // Row 1: ATP
        var row1 = preview.Rows[0];
        Assert.Equal("SUP-ATP-DC", row1.SupplierCode);
        Assert.Equal("ATP", row1.SupplierName);
        Assert.Equal("Động cơ - Đầu đốt", row1.IndustryGroups);
        Assert.Null(row1.TaxCode);
        Assert.Null(row1.ContactPhone);
        Assert.True(row1.IsValid);

        // Row 2: BẢO SƠN
        var row2 = preview.Rows[1];
        Assert.Equal("SUP-BAOSON-TX-BAO", row2.SupplierCode);
        Assert.Equal("BẢO SƠN", row2.SupplierName);
        Assert.Equal("Thể xây và bảo ôn - Gạch bông", row2.IndustryGroups);
        Assert.Null(row2.TaxCode);
        Assert.Null(row2.ContactPhone);
        Assert.True(row2.IsValid);

        // Row 3: BƠM VN
        var row3 = preview.Rows[2];
        Assert.Equal("SUP-BOMVN-DC", row3.SupplierCode);
        Assert.Equal("BƠM VN", row3.SupplierName);

        // Row 4: ĐẠI THÀNH CÔNG
        var row4 = preview.Rows[3];
        Assert.Equal("SUP-DAITHANHCONG-HV", row4.SupplierCode);
        Assert.Equal("ĐẠI THÀNH CÔNG", row4.SupplierName);

        // Last row: VŨ VẠN PHÁT
        var lastRow = preview.Rows[34];
        Assert.Equal("SUP-VUVANPHAT-IN", lastRow.SupplierCode);
        Assert.Equal("VŨ VẠN PHÁT", lastRow.SupplierName);
        Assert.True(lastRow.IsValid);
    }

    [Fact]
    public async Task PreviewExcel_WithRealFile_DefaultSheet_AutoSelectsOption3()
    {
        // Arrange
        var filePath = FindTestExcelPath("261111-BẢN ĐẨY LÊN APP-FINAL5.xlsx");
        Assert.NotNull(filePath);
        var formFile = CreateFormFileFromDisk(filePath);

        // Act - Không truyền sheetName -> Hệ thống phải tự ưu tiên Option 3
        var result = await _controller.PreviewExcel(formFile, null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var preview = Assert.IsType<SupplierImportPreviewResponseDto>(okResult.Value);

        Assert.Equal("SC-MÃ NCC-OPTION 3", preview.SelectedSheet);
        Assert.Equal(35, preview.TotalRows);
        Assert.Equal("SUP-ATP-DC", preview.Rows[0].SupplierCode);
    }

    [Fact]
    public async Task PreviewExcel_WithRealFile_TongHopSheet_ExtractsVer3Codes()
    {
        // Arrange
        var filePath = FindTestExcelPath("261111-BẢN ĐẨY LÊN APP-FINAL5.xlsx");
        Assert.NotNull(filePath);
        var formFile = CreateFormFileFromDisk(filePath);

        // Act
        var result = await _controller.PreviewExcel(formFile, "TỔNG HỢP MÃ NCC", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var preview = Assert.IsType<SupplierImportPreviewResponseDto>(okResult.Value);

        Assert.Equal("TỔNG HỢP MÃ NCC", preview.SelectedSheet);
        Assert.Equal(35, preview.TotalRows);
        Assert.Equal(35, preview.ValidRows);

        // Row 1: ATP with VER.3 code
        var row1 = preview.Rows[0];
        Assert.Equal("SUP-DC-DAUDOT-ATP", row1.SupplierCode);
        Assert.Equal("ATP", row1.SupplierName);
        Assert.Null(row1.TaxCode);
        Assert.Null(row1.ContactPhone);

        // Row 2: BAOSON with VER.3 code
        var row2 = preview.Rows[1];
        Assert.Equal("SUP-TX-BAO-GACHBONG-BAOSON", row2.SupplierCode);
        Assert.Equal("BAOSON", row2.SupplierName);
        Assert.Null(row2.TaxCode);
        Assert.Null(row2.ContactPhone);
    }

    [Fact]
    public async Task ImportExcel_WithRealFile_Option3Sheet_PersistsSuppliersCorrectly()
    {
        // Arrange
        var filePath = FindTestExcelPath("261111-BẢN ĐẨY LÊN APP-FINAL5.xlsx");
        Assert.NotNull(filePath);
        var formFile = CreateFormFileFromDisk(filePath);

        // Act
        var result = await _controller.ImportExcel(formFile, "SC-MÃ NCC-OPTION 3", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var resDto = Assert.IsType<SupplierImportResultDto>(okResult.Value);

        Assert.Equal(35, resDto.TotalRows);
        Assert.Equal(35, resDto.ImportedCount);
        Assert.Equal(0, resDto.FailedCount);

        // Verify database records
        var atp = await _db.Suppliers.FirstOrDefaultAsync(s => s.SupplierCode == "SUP-ATP-DC");
        Assert.NotNull(atp);
        Assert.Equal("ATP", atp.SupplierName);
        Assert.Null(atp.TaxCode);
        Assert.Null(atp.ContactPhone);

        var baoson = await _db.Suppliers.FirstOrDefaultAsync(s => s.SupplierCode == "SUP-BAOSON-TX-BAO");
        Assert.NotNull(baoson);
        Assert.Equal("BẢO SƠN", baoson.SupplierName);
        Assert.Null(baoson.TaxCode);

        var vuvanphat = await _db.Suppliers.FirstOrDefaultAsync(s => s.SupplierCode == "SUP-VUVANPHAT-IN");
        Assert.NotNull(vuvanphat);
        Assert.Equal("VŨ VẠN PHÁT", vuvanphat.SupplierName);
    }

    [Fact]
    public async Task PreviewExcel_WithStandardTemplate_ExtractsAllFieldsCorrectly()
    {
        // Arrange: Tạo in-memory workbook chuẩn
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("DanhSachNCC");
        ws.Cell(1, 1).Value = "Mã NCC (để trống tự sinh)";
        ws.Cell(1, 2).Value = "Tên nhà cung cấp";
        ws.Cell(1, 3).Value = "Mã số thuế";
        ws.Cell(1, 4).Value = "Số điện thoại";
        ws.Cell(1, 5).Value = "Website";
        ws.Cell(1, 6).Value = "Loại hình";
        ws.Cell(1, 7).Value = "Phạm vi";
        ws.Cell(1, 8).Value = "Hạn nợ (ngày)";
        ws.Cell(1, 9).Value = "Hạn mức nợ (tiền)";
        ws.Cell(1, 10).Value = "Điều khoản thanh toán";
        ws.Cell(1, 11).Value = "Nhóm ngành hàng";

        ws.Cell(2, 1).Value = "";
        ws.Cell(2, 2).Value = "Công ty TNHH Thép Hòa Phát Hưng Yên";
        ws.Cell(2, 3).Value = "0100123456";
        ws.Cell(2, 4).Value = "0987654321";
        ws.Cell(2, 5).Value = "https://hoaphat.com.vn";
        ws.Cell(2, 6).Value = "Nhà sản xuất";
        ws.Cell(2, 7).Value = "Trong nước";
        ws.Cell(2, 8).Value = 45;
        ws.Cell(2, 9).Value = 500000000;
        ws.Cell(2, 10).Value = "Thanh toán trong vòng 45 ngày";
        ws.Cell(2, 11).Value = "Hệ ống thép & phụ kiện hàn";

        ws.Cell(3, 1).Value = "SUP-KITZ-001";
        ws.Cell(3, 2).Value = "Công ty Cổ phần Van Kitz Nhật Bản";
        ws.Cell(3, 3).Value = "0309988776";
        ws.Cell(3, 4).Value = "0903123456";
        ws.Cell(3, 5).Value = "https://kitz.co.jp";
        ws.Cell(3, 6).Value = "Nhà phân phối";
        ws.Cell(3, 7).Value = "Nhập khẩu";
        ws.Cell(3, 8).Value = 30;
        ws.Cell(3, 9).Value = 300000000;
        ws.Cell(3, 10).Value = "Net 30 kể từ ngày giao hàng";
        ws.Cell(3, 11).Value = "Hệ van công nghiệp & phụ kiện mặt bích";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var formFile = new FormFile(ms, 0, ms.Length, "file", "template_test.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        // Act
        var result = await _controller.PreviewExcel(formFile, null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var preview = Assert.IsType<SupplierImportPreviewResponseDto>(okResult.Value);

        Assert.Equal(2, preview.TotalRows);
        Assert.Equal(2, preview.ValidRows);

        var r1 = preview.Rows[0];
        Assert.Equal("(Tự sinh)", r1.SupplierCode);
        Assert.Equal("Công ty TNHH Thép Hòa Phát Hưng Yên", r1.SupplierName);
        Assert.Equal("0100123456", r1.TaxCode);
        Assert.Equal("0987654321", r1.ContactPhone);
        Assert.Equal("https://hoaphat.com.vn", r1.Website);
        Assert.Equal("Nhà sản xuất", r1.SupplierType);
        Assert.Equal("Trong nước", r1.Scope);
        Assert.Equal(45, r1.MaxDebtDays);
        Assert.Equal(500000000m, r1.CreditLimit);
        Assert.Equal("Thanh toán trong vòng 45 ngày", r1.PaymentTerms);
        Assert.Equal("Hệ ống thép & phụ kiện hàn", r1.IndustryGroups);
        Assert.True(r1.IsValid);

        var r2 = preview.Rows[1];
        Assert.Equal("SUP-KITZ-001", r2.SupplierCode);
        Assert.Equal("Công ty Cổ phần Van Kitz Nhật Bản", r2.SupplierName);
        Assert.Equal("0309988776", r2.TaxCode);
        Assert.Equal("0903123456", r2.ContactPhone);
        Assert.Equal("https://kitz.co.jp", r2.Website);
        Assert.Equal("Nhà phân phối", r2.SupplierType);
        Assert.Equal("Nhập khẩu", r2.Scope);
        Assert.Equal(30, r2.MaxDebtDays);
        Assert.Equal(300000000m, r2.CreditLimit);
        Assert.Equal("Net 30 kể từ ngày giao hàng", r2.PaymentTerms);
        Assert.Equal("Hệ van công nghiệp & phụ kiện mặt bích", r2.IndustryGroups);
        Assert.True(r2.IsValid);
    }

    [Fact]
    public async Task PreviewExcel_WithDuplicateCodeAndTax_FlagsErrors()
    {
        // Arrange
        _db.Suppliers.Add(new Supplier
        {
            SupplierCode = "SUP-EXIST-01",
            SupplierName = "Công ty Tồn Tại",
            TaxCode = "0109999999",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "Mã NCC";
        ws.Cell(1, 2).Value = "Tên NCC";
        ws.Cell(1, 3).Value = "MST";

        // Row 2: Trùng mã NCC đã có trong DB
        ws.Cell(2, 1).Value = "SUP-EXIST-01";
        ws.Cell(2, 2).Value = "Công ty Mới A";
        ws.Cell(2, 3).Value = "0101111111";

        // Row 3: Trùng MST đã có trong DB
        ws.Cell(3, 1).Value = "SUP-NEW-02";
        ws.Cell(3, 2).Value = "Công ty Mới B";
        ws.Cell(3, 3).Value = "0109999999";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var formFile = new FormFile(ms, 0, ms.Length, "file", "dup_test.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        // Act
        var result = await _controller.PreviewExcel(formFile, null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var preview = Assert.IsType<SupplierImportPreviewResponseDto>(okResult.Value);

        Assert.Equal(2, preview.TotalRows);
        Assert.Equal(0, preview.ValidRows);
        Assert.Equal(2, preview.ErrorRows);

        Assert.False(preview.Rows[0].IsValid);
        Assert.Contains("Mã NCC 'SUP-EXIST-01' đã tồn tại", preview.Rows[0].Error);

        Assert.False(preview.Rows[1].IsValid);
        Assert.Contains("MST '0109999999' đã tồn tại", preview.Rows[1].Error);
    }

    private class FakeSupplierGroupService : ISupplierGroupService
    {
        public Task<SupplierGroupPagedResponse> ListAsync(string? search, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(new SupplierGroupPagedResponse([], 0, page, pageSize, 0));

        public Task<SupplierGroupDto> GetAsync(int id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<SupplierGroupDto> CreateAsync(UpsertSupplierGroupRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<SupplierGroupDto> UpdateAsync(int id, UpsertSupplierGroupRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task DeleteAsync(int id, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<SupplierGroupMemberDto>> ListMembersAsync(int groupId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SupplierGroupMemberDto>>([]);

        public Task<IReadOnlyList<SupplierGroupMemberDto>> SetMembersAsync(int groupId, SetSupplierGroupMembersRequest request, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SupplierGroupMemberDto>>([]);

        public Task<IReadOnlyList<SupplierGroupRefDto>> ListGroupsOfSupplierAsync(int supplierId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SupplierGroupRefDto>>([]);

        public Task<IReadOnlyList<SupplierGroupRefDto>> SetGroupsOfSupplierAsync(int supplierId, SetSupplierGroupsRequest request, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SupplierGroupRefDto>>([]);
    }

    private class TestCurrentUser : ICurrentUser
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Test User";
        public string Email { get; set; } = "test@namviet.vn";
        public string EmployeeCode => "EMP-001";
        public int? EmployeeId => null;
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles { get; set; } = [];
        public IReadOnlyList<string> Permissions { get; set; } = [];
        public ClaimsPrincipal Principal => new();

        public bool IsInRole(string role) => Roles.Contains(role);
        public bool HasPermission(string permission) => true;
        public bool HasAllPermissions(params string[] permissions) => true;
        public bool HasAnyPermission(params string[] permissions) => true;
    }
}
