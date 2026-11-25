using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Controllers.Wms;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Services;
using NamViet.ERP.SharedKernel.Authorization;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Tests.Wms;

public class OpeningStockImportTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly InventoryDomainService _inventory;
    private readonly AdminCurrentUser _adminUser;
    private readonly StockController _controller;

    public OpeningStockImportTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"opening-stock-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new WarehouseDbContext(options);
        _inventory = new InventoryDomainService(_db);
        _adminUser = new AdminCurrentUser();
        _controller = new StockController(_db, _inventory, _adminUser);

        _db.Materials.AddRange(
            new Material
            {
                MaterialCode = "THEP-8.18-X",
                MaterialName = "Ống thép đúc 8.18mm",
                Unit = "Cây",
                IsActive = true,
            },
            new Material
            {
                MaterialCode = "VAN-DN100",
                MaterialName = "Van cổng gang DN100 PN16",
                Unit = "Cái",
                IsActive = true,
            });
        _db.Projects.Add(new Project
        {
            ProjectCode = "DA-OPEN-01",
            ProjectName = "Nhà máy Nhiệt điện Sông Hậu",
            PmName = "Nguyễn Văn Minh",
            StartDate = new DateOnly(2026, 1, 1),
        });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ImportOpeningBalance_CentralWarehouse_PersistsInventoryAndTransaction()
    {
        using var fileStream = BuildWorkbookStream(
            ("THEP-8.18-X", "Ống thép đúc 8.18mm", "Cây", "12", "500000", "Kiểm kê kho trung tâm"),
            ("VAN-DN100", "Van cổng gang DN100 PN16", "Cái", "8", "1200000", null));
        var formFile = CreateFormFile(fileStream, "central.xlsx");

        var result = await _controller.ImportOpeningBalance(formFile, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<OpeningStockImportResultDto>(ok.Value);
        Assert.Equal(2, dto.TotalItems);
        Assert.Equal(20m, dto.TotalQty);
        Assert.Equal(12m * 500000m + 8m * 1200000m, dto.TotalAmount);
        Assert.Equal(0, dto.SkippedCount);

        var inv1 = await _db.WarehouseInventories.SingleAsync(i => i.MaterialCode == "THEP-8.18-X");
        var inv2 = await _db.WarehouseInventories.SingleAsync(i => i.MaterialCode == "VAN-DN100");
        Assert.Equal(WmsWarehouseIds.General, inv1.WarehouseId);
        Assert.Equal(12m, inv1.Qty);
        Assert.Equal(8m, inv2.Qty);
    }

    [Fact]
    public async Task ImportOpeningBalance_ProjectWarehouse_PersistsProjectInventory()
    {
        using var fileStream = BuildWorkbookStream(
            ("THEP-8.18-X", "Ống thép đúc 8.18mm", "Cây", "25", "500000", "Kho công trường"),
            ("VAN-DN100", "Van cổng gang DN100 PN16", "Cái", "5", "1200000", null));
        var formFile = CreateFormFile(fileStream, "project.xlsx");

        var result = await _controller.ImportOpeningBalance(formFile, "DA-OPEN-01", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<OpeningStockImportResultDto>(ok.Value);
        Assert.Equal(2, dto.TotalItems);
        Assert.Equal(30m, dto.TotalQty);

        var pi1 = await _db.ProjectInventories.SingleAsync(
            p => p.ProjectCode == "DA-OPEN-01" && p.MaterialCode == "THEP-8.18-X");
        var pi2 = await _db.ProjectInventories.SingleAsync(
            p => p.ProjectCode == "DA-OPEN-01" && p.MaterialCode == "VAN-DN100");
        Assert.Equal(25m, pi1.ActualImportQty);
        Assert.Equal(5m, pi2.ActualImportQty);
    }

    [Fact]
    public async Task ImportOpeningBalance_WithDuplicateMaterialRowsInCentralWarehouse_AccumulatesIntoSingleInventoryRecord()
    {
        using var fileStream = BuildWorkbookStream(
            ("THEP-8.18-X", "Ống thép đúc 8.18mm", "Cây", "10", "500000", "Lô A"),
            ("THEP-8,18-X", "Ống thép đúc 8.18mm", "Cây", "15", "500000", "Lô B"));
        var formFile = CreateFormFile(fileStream, "dup_central.xlsx");

        var result = await _controller.ImportOpeningBalance(formFile, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<OpeningStockImportResultDto>(ok.Value);
        Assert.Equal(25m, dto.TotalQty);

        var inv = await _db.WarehouseInventories.SingleAsync(i => i.MaterialCode == "THEP-8.18-X");
        Assert.Equal(25m, inv.Qty);
    }

    [Fact]
    public async Task ImportOpeningBalance_WithDuplicateMaterialRowsInProjectWarehouse_AccumulatesIntoSingleInventoryRecord()
    {
        using var fileStream = BuildWorkbookStream(
            ("VAN-DN100", "Van cổng gang DN100 PN16", "Cái", "4", "1200000", "Kệ 1"),
            ("VAN-DN100", "Van cổng gang DN100 PN16", "Cái", "6", "1200000", "Kệ 2"));
        var formFile = CreateFormFile(fileStream, "dup_project.xlsx");

        var result = await _controller.ImportOpeningBalance(formFile, "DA-OPEN-01", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<OpeningStockImportResultDto>(ok.Value);
        Assert.Equal(10m, dto.TotalQty);

        var pi = await _db.ProjectInventories.SingleAsync(
            p => p.ProjectCode == "DA-OPEN-01" && p.MaterialCode == "VAN-DN100");
        Assert.Equal(10m, pi.ActualImportQty);
    }

    [Fact]
    public async Task ImportOpeningBalance_WithInvalidAndValidRows_ImportsValidRowsAndReportsSkippedCount()
    {
        using var fileStream = BuildWorkbookStream(
            ("THEP-8.18-X", "Ống thép đúc 8.18mm", "Cây", "30", "500000", "Hợp lệ"),
            ("MA-KHONG-TON-TAI", "Vật tư không tồn tại", "Cái", "5", "100000", "Sai mã"),
            ("VAN-DN100", "Van cổng gang DN100 PN16", "Cái", "-3", "1200000", "Âm số lượng"));
        var formFile = CreateFormFile(fileStream, "mixed.xlsx");

        var result = await _controller.ImportOpeningBalance(formFile, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<OpeningStockImportResultDto>(ok.Value);
        Assert.Equal(1, dto.TotalItems);
        Assert.Equal(30m, dto.TotalQty);
        Assert.Equal(2, dto.SkippedCount);

        var inv = await _db.WarehouseInventories.SingleAsync(i => i.MaterialCode == "THEP-8.18-X");
        Assert.Equal(30m, inv.Qty);
        Assert.False(await _db.WarehouseInventories.AnyAsync(i => i.MaterialCode == "VAN-DN100"));
    }

    private static MemoryStream BuildWorkbookStream(
        params (string Code, string Name, string Unit, string Qty, string Price, string? Note)[] rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("TonKhoDauKy");
        ws.Cell(1, 1).Value = "Mã vật tư";
        ws.Cell(1, 2).Value = "Tên vật tư";
        ws.Cell(1, 3).Value = "Đơn vị tính";
        ws.Cell(1, 4).Value = "Số lượng tồn";
        ws.Cell(1, 5).Value = "Đơn giá vốn";
        ws.Cell(1, 6).Value = "Ghi chú";

        for (var i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            var rowIdx = i + 2;
            ws.Cell(rowIdx, 1).Value = r.Code;
            ws.Cell(rowIdx, 2).Value = r.Name;
            ws.Cell(rowIdx, 3).Value = r.Unit;
            ws.Cell(rowIdx, 4).Value = r.Qty;
            ws.Cell(rowIdx, 5).Value = r.Price;
            if (r.Note is not null)
                ws.Cell(rowIdx, 6).Value = r.Note;
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static FormFile CreateFormFile(MemoryStream stream, string fileName)
    {
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        };
    }

    private sealed class AdminCurrentUser : ICurrentUser
    {
        private static readonly ClaimsPrincipal EmptyPrincipal = new();

        public Guid Id => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string Name => "Quản trị viên kho";
        public string Email => "admin@mayzer.vn";
        public string EmployeeCode => "NV-ADMIN";
        public int? EmployeeId => 1;
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles { get; } = [RolePermissionTemplates.AdminRole];
        public IReadOnlyList<string> Permissions { get; } = ["wms.stock.view:all"];
        public ClaimsPrincipal Principal => EmptyPrincipal;

        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        public bool HasPermission(string permission) => Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        public bool HasAllPermissions(params string[] permissions) => permissions.All(HasPermission);
        public bool HasAnyPermission(params string[] permissions) => permissions.Any(HasPermission);
    }
}
