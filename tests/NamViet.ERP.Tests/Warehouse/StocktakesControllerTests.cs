using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Controllers.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Services;
using NamViet.ERP.SharedKernel.Authorization;
using NamViet.ERP.SharedKernel.Interfaces;
using System.Security.Claims;

namespace NamViet.ERP.Tests.Warehouse;

public class StocktakesControllerTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly StocktakesController _controller;
    private readonly TestCurrentUser _currentUser;

    public StocktakesControllerTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);

        _currentUser = new TestCurrentUser
        {
            Id = Guid.NewGuid(),
            Name = "Thủ kho test",
            Roles = [RolePermissionTemplates.AdminRole],
        };

        var projectAccess = new ProjectWarehouseAccessService(_db, _currentUser);
        var inventoryService = new InventoryDomainService(_db);
        var stocktakeAppService = new StocktakeApplicationService(_db, inventoryService);

        _controller = new StocktakesController(_db, _currentUser, projectAccess, stocktakeAppService);

        SeedBaseline();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private void SeedBaseline()
    {
        _db.Projects.Add(new Project
        {
            ProjectCode = "DA-2026-230",
            ProjectName = "Liên Hạnh",
            ProjectType = ProjectType.Project,
            PmName = "PM Test",
            StartDate = new DateOnly(2026, 1, 1),
            Status = ProjectStatus.Active,
        });

        _db.WmsWarehouses.Add(new WmsWarehouse
        {
            Id = "DA-2026-230",
            Code = "DA-2026-230",
            Name = "Liên Hạnh",
            Group = "Kho dự án",
            Status = "active",
        });

        _db.Materials.AddRange(
            new Material { MaterialCode = "IN-INH-SU304-N1-50x50x4x6000", MaterialName = "Inox 304 - Hộp CN", Unit = "cây" },
            new Material { MaterialCode = "IN-INL-SU304-N1-06x6000", MaterialName = "Inox 304 - Vê đúc", Unit = "cây" },
            new Material { MaterialCode = "IN-INT-SU304-2B-1500x3x6000", MaterialName = "Inox 304 - Tấm 1500x3", Unit = "tấm" },
            new Material { MaterialCode = "IN-INT-SU304-2B-1500x4x6000", MaterialName = "Inox 304 - Tấm 1500x4", Unit = "tấm" }
        );

        _db.SaveChanges();
    }

    [Fact]
    public async Task Create_generates_sequential_code_even_when_previous_stocktake_was_deleted()
    {
        // Giả lập phiếu KK-0001 đã tạo và sau đó bị xóa mềm
        _db.Stocktakes.Add(new Stocktake
        {
            Id = "st-deleted1",
            VoucherCode = "KK-0001",
            WarehouseId = "DA-2026-230",
            DeletedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var dto = new CreateStocktakeDto(
            VoucherCode: null,
            Date: "24/09/2026",
            CreatedBy: null,
            Status: "pending",
            WarehouseId: "DA-2026-230",
            WarehouseName: "Liên Hạnh",
            Condition: "counting",
            IsFrozen: true,
            Note: "Kiểm kê định kỳ",
            Items:
            [
                new StocktakeItemDto("IN-INH-SU304-N1-50x50x4x6000", "IN-INH-SU304-N1-50x50x4x6000", "Inox 304", 2m, 3m, 1m, null),
            ]);

        var actionResult = await _controller.Create(dto, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var created = Assert.IsType<StocktakeDto>(okResult.Value);

        // Phải sinh mã KK-0002 để không trùng với KK-0001 đã xóa
        Assert.Equal("KK-0002", created.VoucherCode);
    }

    [Fact]
    public async Task Create_auto_ensures_project_warehouse_even_if_soft_deleted()
    {
        var wh = await _db.WmsWarehouses.FirstAsync(w => w.Id == "DA-2026-230");
        wh.DeletedAt = DateTime.UtcNow;
        wh.Status = "inactive";
        await _db.SaveChangesAsync();

        var dto = new CreateStocktakeDto(
            VoucherCode: null,
            Date: "24/09/2026",
            CreatedBy: null,
            Status: "pending",
            WarehouseId: "DA-2026-230",
            WarehouseName: "Liên Hạnh",
            Condition: "counting",
            IsFrozen: true,
            Note: "Kiểm tra khôi phục kho",
            Items:
            [
                new StocktakeItemDto("IN-INH-SU304-N1-50x50x4x6000", "IN-INH-SU304-N1-50x50x4x6000", "Inox 304", 2m, 3m, 1m, null),
            ]);

        var actionResult = await _controller.Create(dto, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var created = Assert.IsType<StocktakeDto>(okResult.Value);

        Assert.Equal("DA-2026-230", created.WarehouseId);

        // Kho dự án phải được bỏ đánh dấu soft-delete và active trở lại
        var restoredWh = await _db.WmsWarehouses.IgnoreQueryFilters().FirstAsync(w => w.Id == "DA-2026-230");
        Assert.Null(restoredWh.DeletedAt);
        Assert.Equal("active", restoredWh.Status);
    }

    [Fact]
    public async Task Create_auto_ensures_missing_materials_so_fk_succeeds()
    {
        // Mặt hàng PK-CTC-SA105-PN16-DN150-90° chưa có trong danh mục materials
        const string unseededCode = "PK-CTC-SA105-PN16-DN150-90°";
        Assert.False(await _db.Materials.AnyAsync(m => m.MaterialCode == unseededCode));

        var dto = new CreateStocktakeDto(
            VoucherCode: null,
            Date: "24/09/2026",
            CreatedBy: null,
            Status: "pending",
            WarehouseId: "DA-2026-230",
            WarehouseName: "Liên Hạnh",
            Condition: "counting",
            IsFrozen: true,
            Note: "Test auto seed material",
            Items:
            [
                new StocktakeItemDto(unseededCode, unseededCode, "Co thép đúc 90 độ", 0m, 0m, 0m, ""),
            ]);

        var actionResult = await _controller.Create(dto, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var created = Assert.IsType<StocktakeDto>(okResult.Value);

        Assert.Single(created.Items);
        Assert.Equal(unseededCode, created.Items[0].Sku);

        // Vật tư mới đã được tự động thêm vào materials
        var material = await _db.Materials.FirstOrDefaultAsync(m => m.MaterialCode == unseededCode);
        Assert.NotNull(material);
        Assert.Equal("Co thép đúc 90 độ", material.MaterialName);
    }

    [Fact]
    public async Task Create_replicates_user_screenshot_payload_successfully()
    {
        // Tái lập chính xác 5 vật tư từ hình ảnh người dùng gặp lỗi
        var dto = new CreateStocktakeDto(
            VoucherCode: null,
            Date: "24/09/2026",
            CreatedBy: null,
            Status: "pending",
            WarehouseId: "DA-2026-230",
            WarehouseName: "Liên Hạnh",
            Condition: "counting",
            IsFrozen: true,
            Note: "Kiểm kê tháng",
            Items:
            [
                new StocktakeItemDto("IN-INH-SU304-N1-50x50x4x6000", "IN-INH-SU304-N1-50x50x4x6000", "Inox 304 - Hộp CN 50x50x4", 2m, 3m, 1m, ""),
                new StocktakeItemDto("IN-INL-SU304-N1-06x6000", "IN-INL-SU304-N1-06x6000", "Inox 304 - Vê đúc 60x60x6", 8m, 8m, 0m, ""),
                new StocktakeItemDto("IN-INT-SU304-2B-1500x3x6000", "IN-INT-SU304-2B-1500x3x6000", "Inox 304 - Tấm 1500x3", 2m, 1m, -1m, "Vẫn test"),
                new StocktakeItemDto("IN-INT-SU304-2B-1500x4x6000", "IN-INT-SU304-2B-1500x4x6000", "Inox 304 - Tấm 1500x4", 1m, 3m, 2m, ""),
                new StocktakeItemDto("PK-CTC-SA105-PN16-DN150-90°", "PK-CTC-SA105-PN16-DN150-90°", "Co thép đúc 90°", 0m, 0m, 0m, ""),
            ]);

        var actionResult = await _controller.Create(dto, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var created = Assert.IsType<StocktakeDto>(okResult.Value);

        Assert.Equal("KK-0001", created.VoucherCode);
        Assert.Equal("DA-2026-230", created.WarehouseId);
        Assert.Equal("Kiểm kê tháng", created.Note);
        Assert.Equal(5, created.Items.Count);

        var itemWithReason = created.Items.First(i => i.Sku == "IN-INT-SU304-2B-1500x3x6000");
        Assert.Equal(-1m, itemWithReason.Diff);
        Assert.Equal("Vẫn test", itemWithReason.Reason);
    }

    [Fact]
    public async Task Create_truncates_reason_longer_than_500_chars()
    {
        var longReason = new string('A', 600);
        var dto = new CreateStocktakeDto(
            VoucherCode: null,
            Date: "24/09/2026",
            CreatedBy: null,
            Status: "pending",
            WarehouseId: "DA-2026-230",
            WarehouseName: "Liên Hạnh",
            Condition: "counting",
            IsFrozen: true,
            Note: "Test reason max length",
            Items:
            [
                new StocktakeItemDto("IN-INH-SU304-N1-50x50x4x6000", "IN-INH-SU304-N1-50x50x4x6000", "Inox 304", 2m, 3m, 1m, longReason),
            ]);

        var actionResult = await _controller.Create(dto, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var created = Assert.IsType<StocktakeDto>(okResult.Value);

        Assert.Equal(500, created.Items[0].Reason?.Length);
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
