using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Api.Controllers.Wms;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;
using Xunit;

namespace NamViet.ERP.Tests.Warehouse;

public class OpeningStockAndSupplierImportTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly FakeSupplierGroupService _groupsService;
    private readonly TestCurrentUser _currentUser;
    private readonly SuppliersController _suppliersController;
    private readonly StockController _stockController;

    public OpeningStockAndSupplierImportTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);

        _groupsService = new FakeSupplierGroupService();
        _currentUser = new TestCurrentUser
        {
            Id = Guid.NewGuid(),
            Name = "Quản trị viên",
            Email = "admin@mayzer.com",
            Roles = ["Admin"],
            Permissions = ["admin.users.view:all", "wms.suppliers.view:all", "wms.suppliers.create", "wms.stock.view:all"]
        };

        _suppliersController = new SuppliersController(_db, _groupsService, _currentUser);
        _stockController = new StockController(_db, new FakeInventoryDomainService(), _currentUser);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public void DownloadOpeningBalanceTemplate_ReturnsExcelFile()
    {
        // Act
        var result = _stockController.DownloadOpeningBalanceTemplate();

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
        Assert.Equal("Mau_Nhap_Ton_Kho_Dau_Ky.xlsx", fileResult.FileDownloadName);
        Assert.NotEmpty(fileResult.FileContents);
    }

    [Fact]
    public void DownloadSupplierTemplate_ReturnsExcelFile()
    {
        // Act
        var result = _suppliersController.DownloadExcelTemplate();

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
        Assert.Equal("Mau_Nhap_NhaCungCap.xlsx", fileResult.FileDownloadName);
        Assert.NotEmpty(fileResult.FileContents);
    }

    [Fact]
    public async Task CreateSupplier_WithOnboardingFields_SavesAndReturnsFields()
    {
        // Arrange
        var req = new UpsertSupplierRequest(
            SupplierCode: null,
            SupplierName: "Công ty TNHH Bơm Grundfos Việt Nam",
            ContactPhone: "0912345678",
            MaxDebtDays: 60,
            PaymentTerms: "Net 60",
            CreditLimit: 1_000_000_000m,
            TaxCode: "0301122334",
            SupplierType: "Nhà sản xuất",
            Scope: "Quốc tế",
            Website: "https://grundfos.com");

        // Act
        var result = await _suppliersController.Create(req, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SupplierDto>(okResult.Value);
        Assert.Equal("Nhà sản xuất", dto.SupplierType);
        Assert.Equal("Quốc tế", dto.Scope);
        Assert.Equal("https://grundfos.com", dto.Website);
        Assert.Equal("0301122334", dto.TaxCode);

        var inDb = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == dto.Id);
        Assert.NotNull(inDb);
        Assert.Equal("Nhà sản xuất", inDb.SupplierType);
        Assert.Equal("Quốc tế", inDb.Scope);
        Assert.Equal("https://grundfos.com", inDb.Website);
    }

    [Fact]
    public async Task PreviewOpeningBalance_WithActualCompanyFile_ParsesRowsWithoutCrashing()
    {
        // Arrange
        var filePath = @"D:\WorkspaceRoot\NamViet\Test\261109 - SỐ LƯỢNG TỒN KHO.xlsx";
        if (!File.Exists(filePath)) return;

        // Seed some materials matching what is in the file
        _db.Materials.AddRange(
            new Material { MaterialCode = "TH-DUC-SA106-H-Ø51x3.68x6000", MaterialName = "Ống thép đúc SA106 Ø51x3.68x6000mm", Unit = "Cây", IsActive = true },
            new Material { MaterialCode = "LB-CYC-", MaterialName = "Lọc bụi Cyclo", Unit = "Cái", IsActive = true }
        );
        await _db.SaveChangesAsync();

        using var fileStream = File.OpenRead(filePath);
        var formFile = new FormFile(fileStream, 0, fileStream.Length, "file", Path.GetFileName(filePath))
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        // Act
        var result = await _stockController.PreviewOpeningBalance(formFile, null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var preview = Assert.IsType<OpeningStockPreviewResponseDto>(okResult.Value);

        Assert.True(preview.TotalRows > 0, "Total rows should be greater than 0");
        Assert.Equal(284, preview.TotalRows); // Exactly 284 rows with non-empty quantity
        Assert.True(preview.ValidRows >= 2, "Seeded materials should be valid");
        Assert.NotEmpty(preview.Items);
    }

    [Fact]
    public async Task PreviewOpeningBalance_WithStandardTemplate_ParsesCorrectly()
    {
        // Arrange
        _db.Materials.AddRange(
            new Material { MaterialCode = "VT-THEP-001", MaterialName = "Ống thép đúc DN100 SCH40", Unit = "Cây", IsActive = true },
            new Material { MaterialCode = "VT-VAN-002", MaterialName = "Van cổng ty chìm gang DN100 PN16", Unit = "Cái", IsActive = true }
        );
        await _db.SaveChangesAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("TonKhoDauKy");
        ws.Cell(1, 1).Value = "Mã vật tư";
        ws.Cell(1, 2).Value = "Tên vật tư";
        ws.Cell(1, 3).Value = "Đơn vị tính";
        ws.Cell(1, 4).Value = "Số lượng tồn";
        ws.Cell(1, 5).Value = "Đơn giá vốn";
        ws.Cell(1, 6).Value = "Ghi chú";

        // Row 2: Valid
        ws.Cell(2, 1).Value = "VT-THEP-001";
        ws.Cell(2, 2).Value = "Ống thép đúc DN100 SCH40";
        ws.Cell(2, 3).Value = "Cây";
        ws.Cell(2, 4).Value = "50";
        ws.Cell(2, 5).Value = "850000";
        ws.Cell(2, 6).Value = "Tồn kho đầu kỳ";

        // Row 3: Valid
        ws.Cell(3, 1).Value = "VT-VAN-002";
        ws.Cell(3, 2).Value = "Van cổng ty chìm gang DN100 PN16";
        ws.Cell(3, 3).Value = "Cái";
        ws.Cell(3, 4).Value = "20";
        ws.Cell(3, 5).Value = "1250000";

        // Row 4: Material code not found in db
        ws.Cell(4, 1).Value = "VT-NON-EXIST";
        ws.Cell(4, 2).Value = "Vật tư chưa có trong DB";
        ws.Cell(4, 3).Value = "Bộ";
        ws.Cell(4, 4).Value = "10";
        ws.Cell(4, 5).Value = "100000";

        // Row 5: Invalid qty (<= 0)
        ws.Cell(5, 1).Value = "VT-THEP-001";
        ws.Cell(5, 2).Value = "Ống thép đúc DN100 SCH40";
        ws.Cell(5, 3).Value = "Cây";
        ws.Cell(5, 4).Value = "-5";

        // Row 6: Valid with qty = 0 (empty stock is a valid state)
        ws.Cell(6, 1).Value = "VT-THEP-001";
        ws.Cell(6, 2).Value = "Ống thép đúc DN100 SCH40";
        ws.Cell(6, 3).Value = "Cây";
        ws.Cell(6, 4).Value = "0";
        ws.Cell(6, 5).Value = "850000";
        ws.Cell(6, 6).Value = "Hết hàng";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var formFile = new FormFile(ms, 0, ms.Length, "file", "test.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        // Act
        var result = await _stockController.PreviewOpeningBalance(formFile, null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var preview = Assert.IsType<OpeningStockPreviewResponseDto>(okResult.Value);

        Assert.Equal(5, preview.TotalRows);
        Assert.Equal(3, preview.ValidRows);
        Assert.Equal(2, preview.ErrorRows);
        Assert.Equal(70m, preview.TotalQty);
        Assert.Equal(50m * 850000m + 20m * 1250000m, preview.TotalAmount);

        var zeroItem = preview.Items.FirstOrDefault(i => i.Qty == 0);
        Assert.NotNull(zeroItem);
        Assert.True(zeroItem.IsValid);
        Assert.Null(zeroItem.Error);
        Assert.Equal("Hết hàng (Tồn 0 - Bỏ qua)", zeroItem.Note);
    }

    [Fact]
    public async Task PreviewOpeningBalance_WithZeroQuantity_IsValidAndSetsNote()
    {
        // Arrange
        _db.Materials.Add(new Material { MaterialCode = "VT-ZERO-001", MaterialName = "Vật tư hết hàng", Unit = "Cái", IsActive = true });
        await _db.SaveChangesAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("TonKhoDauKy");
        ws.Cell(1, 1).Value = "Mã vật tư";
        ws.Cell(1, 2).Value = "Tên vật tư";
        ws.Cell(1, 3).Value = "Đơn vị tính";
        ws.Cell(1, 4).Value = "Số lượng tồn";
        ws.Cell(1, 5).Value = "Đơn giá vốn";
        ws.Cell(1, 6).Value = "Ghi chú";

        // Row 2: Qty 0 without note
        ws.Cell(2, 1).Value = "VT-ZERO-001";
        ws.Cell(2, 2).Value = "Vật tư hết hàng";
        ws.Cell(2, 3).Value = "Cái";
        ws.Cell(2, 4).Value = "0";

        // Row 3: Qty 0 with existing note
        ws.Cell(3, 1).Value = "VT-ZERO-001";
        ws.Cell(3, 2).Value = "Vật tư hết hàng";
        ws.Cell(3, 3).Value = "Cái";
        ws.Cell(3, 4).Value = "0";
        ws.Cell(3, 6).Value = "Tạm hết";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var formFile = new FormFile(ms, 0, ms.Length, "file", "zero_test.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        // Act
        var result = await _stockController.PreviewOpeningBalance(formFile, null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var preview = Assert.IsType<OpeningStockPreviewResponseDto>(okResult.Value);

        Assert.Equal(2, preview.TotalRows);
        Assert.Equal(2, preview.ValidRows);
        Assert.Equal(0, preview.ErrorRows);
        Assert.Equal(0m, preview.TotalQty);

        Assert.Equal("Tồn 0 (Bỏ qua khi tạo phiếu)", preview.Items[0].Note);
        Assert.True(preview.Items[0].IsValid);
        Assert.Null(preview.Items[0].Error);

        Assert.Equal("Tạm hết (Tồn 0 - Bỏ qua)", preview.Items[1].Note);
        Assert.True(preview.Items[1].IsValid);
        Assert.Null(preview.Items[1].Error);
    }

    [Fact]
    public async Task ImportOpeningBalance_WithZeroQuantity_SkipsZeroQtyAndImportsOnlyPositiveQty()
    {
        // Arrange
        _db.Materials.AddRange(
            new Material { MaterialCode = "VT-ZERO-IMP-001", MaterialName = "Vật tư có tồn", Unit = "Cái", IsActive = true },
            new Material { MaterialCode = "VT-ZERO-IMP-002", MaterialName = "Vật tư tồn 0", Unit = "Cây", IsActive = true }
        );
        await _db.SaveChangesAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("TonKhoDauKy");
        ws.Cell(1, 1).Value = "Mã vật tư";
        ws.Cell(1, 2).Value = "Tên vật tư";
        ws.Cell(1, 3).Value = "Đơn vị tính";
        ws.Cell(1, 4).Value = "Số lượng tồn";
        ws.Cell(1, 5).Value = "Đơn giá vốn";
        ws.Cell(1, 6).Value = "Ghi chú";

        // Row 2: Qty 10
        ws.Cell(2, 1).Value = "VT-ZERO-IMP-001";
        ws.Cell(2, 2).Value = "Vật tư có tồn";
        ws.Cell(2, 3).Value = "Cái";
        ws.Cell(2, 4).Value = "10";
        ws.Cell(2, 5).Value = "100000";

        // Row 3: Qty 0 -> Should be skipped
        ws.Cell(3, 1).Value = "VT-ZERO-IMP-002";
        ws.Cell(3, 2).Value = "Vật tư tồn 0";
        ws.Cell(3, 3).Value = "Cây";
        ws.Cell(3, 4).Value = "0";
        ws.Cell(3, 5).Value = "200000";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var formFile = new FormFile(ms, 0, ms.Length, "file", "zero_import_test.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        // Act
        var result = await _stockController.ImportOpeningBalance(formFile, null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var resDto = Assert.IsType<OpeningStockImportResultDto>(okResult.Value);

        Assert.Equal(1, resDto.TotalItems);
        Assert.Equal(10m, resDto.TotalQty);
        Assert.Equal(1000000m, resDto.TotalAmount);

        // VT-ZERO-IMP-001 has inventory 10
        var inv1 = await _db.WarehouseInventories.FirstOrDefaultAsync(i => i.MaterialCode == "VT-ZERO-IMP-001");
        Assert.NotNull(inv1);
        Assert.Equal(10m, inv1.Qty);

        // VT-ZERO-IMP-002 should NOT have any inventory record
        var inv2 = await _db.WarehouseInventories.FirstOrDefaultAsync(i => i.MaterialCode == "VT-ZERO-IMP-002");
        Assert.Null(inv2);
    }

    [Fact]
    public async Task ImportOpeningBalance_WithStandardTemplate_CreatesTransactionAndInventory()
    {
        // Arrange
        _db.Materials.AddRange(
            new Material { MaterialCode = "VT-IMP-001", MaterialName = "Vật tư nhập 1", Unit = "Cái", IsActive = true },
            new Material { MaterialCode = "VT-IMP-002", MaterialName = "Vật tư nhập 2", Unit = "Cây", IsActive = true }
        );
        await _db.SaveChangesAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("TonKhoDauKy");
        ws.Cell(1, 1).Value = "Mã vật tư";
        ws.Cell(1, 2).Value = "Tên vật tư";
        ws.Cell(1, 3).Value = "Đơn vị tính";
        ws.Cell(1, 4).Value = "Số lượng tồn";
        ws.Cell(1, 5).Value = "Đơn giá vốn";
        ws.Cell(1, 6).Value = "Ghi chú";

        ws.Cell(2, 1).Value = "VT-IMP-001";
        ws.Cell(2, 2).Value = "Vật tư nhập 1";
        ws.Cell(2, 3).Value = "Cái";
        ws.Cell(2, 4).Value = "15";
        ws.Cell(2, 5).Value = "200000";

        ws.Cell(3, 1).Value = "VT-IMP-002";
        ws.Cell(3, 2).Value = "Vật tư nhập 2";
        ws.Cell(3, 3).Value = "Cây";
        ws.Cell(3, 4).Value = "25";
        ws.Cell(3, 5).Value = "400000";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var formFile = new FormFile(ms, 0, ms.Length, "file", "import_test.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        // Act
        var result = await _stockController.ImportOpeningBalance(formFile, null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var resDto = Assert.IsType<OpeningStockImportResultDto>(okResult.Value);

        Assert.Equal(2, resDto.TotalItems);
        Assert.Equal(40m, resDto.TotalQty);
        Assert.Equal(15m * 200000m + 25m * 400000m, resDto.TotalAmount);

        // Verify transaction in DB
        var tx = await _db.WarehouseTransactions.FirstOrDefaultAsync(t => t.TicketCode == resDto.TicketCode);
        Assert.NotNull(tx);
        Assert.Equal("OPENING-STOCK", tx.InvoiceNumber);

        // Verify inventories in general warehouse
        var inv1 = await _db.WarehouseInventories.FirstOrDefaultAsync(i => i.MaterialCode == "VT-IMP-001");
        Assert.NotNull(inv1);
        Assert.Equal(15m, inv1.Qty);

        var inv2 = await _db.WarehouseInventories.FirstOrDefaultAsync(i => i.MaterialCode == "VT-IMP-002");
        Assert.NotNull(inv2);
        Assert.Equal(25m, inv2.Qty);
    }

    private class FakeInventoryDomainService : IInventoryDomainService
    {
        public Task<string> GetGeneralWarehouseIdAsync(CancellationToken ct = default) => Task.FromResult("KHO-TONG");
        public Task<decimal> GetProjectAvailableQtyAsync(string projectCode, string materialCode, string? excludeStockDefectTicketCode = null, CancellationToken ct = default) => Task.FromResult(0m);
        public Task<IReadOnlyDictionary<string, decimal>> GetPendingStockDefectQtyByMaterialAsync(string projectCode, string? excludeStockDefectTicketCode = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<string, decimal>>(new Dictionary<string, decimal>());
        public Task<IReadOnlyList<PendingStockDefectHold>> ListPendingStockDefectHoldsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PendingStockDefectHold>>([]);
        public Task<ReservationResult> CreateReservationAsync(string warehouseId, string projectCode, string materialCode, decimal requestedQty, string sourceTicketCode, CancellationToken ct = default) => Task.FromResult(new ReservationResult(requestedQty, 0));
        public Task FulfillReservationsAsync(string sourceTicketCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task CancelReservationsAsync(string sourceTicketCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task CreateProjectImportPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task CancelProjectImportPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task CreateProjectExportPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task CancelProjectExportPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyApprovedImportAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyApprovedExportAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task CreateProjectExportReturnPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task CancelProjectExportReturnPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyApprovedExportReturnAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyDirectIssueAsync(WarehouseTransaction importTransaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsurePendingDirectExportAsync(WarehouseTransaction importTransaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task HealPendingDirectExportsAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyTransferAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyReturnToGeneralAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyCrossProjectTransferAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyProjectCloseTransferAsync(string projectCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyStocktakeAdjustmentAsync(string projectCode, string materialCode, decimal diffQty, CancellationToken ct = default) => Task.CompletedTask;
        public Task<decimal> GetAvailableQtyAsync(string warehouseId, string materialCode, CancellationToken ct = default) => Task.FromResult(0m);
        public Task EnsureGeneralWarehouseAsync(string? warehouseId = null, CancellationToken ct = default) => Task.CompletedTask;
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
        public string Name { get; set; } = "Quản trị viên";
        public string Email { get; set; } = "admin@mayzer.com";
        public string EmployeeCode => "EMP-ADMIN";
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
