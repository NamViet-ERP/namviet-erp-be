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

        _db.ProjectInventories.AddRange(
            new ProjectInventory { ProjectCode = "DA-2026-230", MaterialCode = "IN-INH-SU304-N1-50x50x4x6000", ActualImportQty = 2m },
            new ProjectInventory { ProjectCode = "DA-2026-230", MaterialCode = "IN-INL-SU304-N1-06x6000", ActualImportQty = 8m },
            new ProjectInventory { ProjectCode = "DA-2026-230", MaterialCode = "IN-INT-SU304-2B-1500x3x6000", ActualImportQty = 2m },
            new ProjectInventory { ProjectCode = "DA-2026-230", MaterialCode = "IN-INT-SU304-2B-1500x4x6000", ActualImportQty = 1m });

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

    [Fact]
    public async Task Create_UsesServerQuantity_AndApprovalLeavesExactlyTheCountedStock()
    {
        var result = await _controller.Create(CountRequest(systemQty: 0, actualQty: 1), default);
        var created = Assert.IsType<StocktakeDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2m, created.Items.Single().SystemQty);
        Assert.Equal(-1m, created.Items.Single().Diff);

        var approval = await _controller.Approve(created.Id, default);
        Assert.IsType<OkObjectResult>(approval.Result);
        Assert.Equal(1m, (await _db.ProjectInventories.FirstAsync(i => i.MaterialCode == CountedMaterial)).ActualInventoryQty);

        var repeated = await _controller.Approve(created.Id, default);
        Assert.IsType<BadRequestObjectResult>(repeated.Result);
        Assert.Equal(1m, (await _db.ProjectInventories.FirstAsync(i => i.MaterialCode == CountedMaterial)).ActualInventoryQty);
    }

    [Fact]
    public async Task Update_FreezeToggleDoesNotRebaseAnOldCount_AfterAnotherStocktakeChangesInventory()
    {
        var initial = StocktakeResponse(await _controller.Create(CountRequest(0, 10), default));
        Assert.IsType<OkObjectResult>((await _controller.Approve(initial.Id, default)).Result);
        var counted = StocktakeResponse(await _controller.Create(CountRequest(0, 8) with { IsFrozen = true }, default));
        Assert.Equal(10m, counted.Items.Single().SystemQty);

        // The browser resends all item values when only toggling the freeze flag.
        StocktakeResponse(await _controller.Update(counted.Id, counted with { IsFrozen = false }, default));
        var adjustment = StocktakeResponse(await _controller.Create(CountRequest(0, 15), default));
        Assert.IsType<OkObjectResult>((await _controller.Approve(adjustment.Id, default)).Result);

        var refrozen = StocktakeResponse(await _controller.Update(counted.Id, counted with { IsFrozen = true }, default));
        Assert.True(refrozen.IsFrozen);
        Assert.Equal(10m, refrozen.Items.Single().SystemQty);
        Assert.Equal(8m, refrozen.Items.Single().ActualQty);
        Assert.Equal(-2m, refrozen.Items.Single().Diff);

        var failure = Assert.IsType<BadRequestObjectResult>((await _controller.Approve(counted.Id, default)).Result);
        Assert.Contains("đã thay đổi", Assert.IsType<string>(failure.Value));
        Assert.Equal(15m, (await _db.ProjectInventories.FirstAsync(i => i.MaterialCode == CountedMaterial)).ActualInventoryQty);
        Assert.Equal("pending", (await _db.Stocktakes.FirstAsync(i => i.Id == counted.Id)).Status);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("cancelled")]
    public async Task Update_MetadataOrCancellationPreservesTheOriginalCountSnapshot(string status)
    {
        var counted = StocktakeResponse(await _controller.Create(CountRequest(0, 1), default));
        var inventory = await _db.ProjectInventories.FirstAsync(i => i.MaterialCode == CountedMaterial);
        inventory.ApplyActualImport(3);
        await _db.SaveChangesAsync();

        var updated = StocktakeResponse(await _controller.Update(counted.Id, counted with
        {
            Status = status,
            Note = "Updated note",
            Items = [counted.Items.Single() with { Reason = "Updated reason", SystemQty = 500m, Diff = 499m }],
        }, default));

        Assert.Equal(status, updated.Status);
        Assert.Equal("Updated note", updated.Note);
        Assert.Equal("Updated reason", updated.Items.Single().Reason);
        Assert.Equal(2m, updated.Items.Single().SystemQty);
        Assert.Equal(1m, updated.Items.Single().ActualQty);
        Assert.Equal(-1m, updated.Items.Single().Diff);
        _db.ChangeTracker.Clear();
        var saved = await _db.StocktakeItems.SingleAsync(i => i.StocktakeId == counted.Id);
        Assert.Equal(2m, saved.SystemQty);
        Assert.Equal(-1m, saved.Diff);
    }

    [Fact]
    public async Task Update_CountCorrectionDoesNotRebaseSnapshotAfterInventoryMovement()
    {
        var result = await _controller.Create(CountRequest(0, 2), default);
        var created = Assert.IsType<StocktakeDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var inventory = await _db.ProjectInventories.FirstAsync(i => i.MaterialCode == CountedMaterial);
        inventory.ApplyActualImport(3);
        await _db.SaveChangesAsync();

        var updatedResult = await _controller.Update(created.Id, created with { Items = CountRequest(0, 4).Items }, default);
        var updated = Assert.IsType<StocktakeDto>(Assert.IsType<OkObjectResult>(updatedResult.Result).Value);
        Assert.Equal(2m, updated.Items.Single().SystemQty);
        Assert.Equal(2m, updated.Items.Single().Diff);
        var failure = Assert.IsType<BadRequestObjectResult>((await _controller.Approve(created.Id, default)).Result);
        Assert.Contains("kiểm đếm lại", Assert.IsType<string>(failure.Value));
        Assert.Equal(5m, (await _db.ProjectInventories.FirstAsync(i => i.MaterialCode == CountedMaterial)).ActualInventoryQty);
    }

    [Fact]
    public async Task Update_CountCorrectionWithoutInventoryMovement_CanStillBeApproved()
    {
        var counted = StocktakeResponse(await _controller.Create(CountRequest(0, 1), default));

        var updated = StocktakeResponse(await _controller.Update(counted.Id, counted with { Items = CountRequest(500, 3).Items }, default));

        Assert.Equal(2m, updated.Items.Single().SystemQty);
        Assert.Equal(3m, updated.Items.Single().ActualQty);
        Assert.Equal(1m, updated.Items.Single().Diff);
        Assert.IsType<OkObjectResult>((await _controller.Approve(counted.Id, default)).Result);
        Assert.Equal(3m, (await _db.ProjectInventories.FirstAsync(i => i.MaterialCode == CountedMaterial)).ActualInventoryQty);
    }

    [Fact]
    public async Task Update_AddingMaterialSnapshotsOnlyTheNewLine()
    {
        const string addedMaterial = "IN-INL-SU304-N1-06x6000";
        var counted = StocktakeResponse(await _controller.Create(CountRequest(0, 1), default));
        var inventory = await _db.ProjectInventories.FirstAsync(i => i.MaterialCode == CountedMaterial);
        inventory.ApplyActualImport(3);
        await _db.SaveChangesAsync();

        var updated = StocktakeResponse(await _controller.Update(counted.Id, counted with
        {
            Items = [counted.Items.Single(), new(addedMaterial, addedMaterial, "Inox added", 500m, 7m, 499m)],
        }, default));

        Assert.Equal(2m, updated.Items.Single(i => i.Sku == CountedMaterial).SystemQty);
        Assert.Equal(-1m, updated.Items.Single(i => i.Sku == CountedMaterial).Diff);
        Assert.Equal(8m, updated.Items.Single(i => i.Sku == addedMaterial).SystemQty);
        Assert.Equal(-1m, updated.Items.Single(i => i.Sku == addedMaterial).Diff);
        Assert.IsType<BadRequestObjectResult>((await _controller.Approve(counted.Id, default)).Result);
        Assert.Equal(8m, (await _db.ProjectInventories.FirstAsync(i => i.MaterialCode == addedMaterial)).ActualInventoryQty);
    }

    [Fact]
    public async Task Update_RejectsChangingWarehouse_WithoutChangingTheCountOrSnapshot()
    {
        const string otherProject = "DA-OTHER";
        _db.Projects.Add(new Project
        {
            ProjectCode = otherProject,
            ProjectName = "Another project",
            PmName = "Test PM",
            StartDate = new DateOnly(2026, 1, 1),
            Status = ProjectStatus.Active,
        });
        _db.ProjectInventories.Add(new ProjectInventory { ProjectCode = otherProject, MaterialCode = CountedMaterial, ActualImportQty = 20m });
        await _db.SaveChangesAsync();
        var counted = StocktakeResponse(await _controller.Create(CountRequest(0, 1), default));

        var result = await _controller.Update(counted.Id, counted with { WarehouseId = otherProject }, default);

        var failure = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("tạo phiếu kiểm kê mới", Assert.IsType<string>(failure.Value));
        _db.ChangeTracker.Clear();
        var saved = await _db.Stocktakes.Include(s => s.Items).SingleAsync(s => s.Id == counted.Id);
        Assert.Equal(counted.WarehouseId, saved.WarehouseId);
        Assert.Equal(2m, saved.Items.Single().SystemQty);
        Assert.Equal(1m, saved.Items.Single().ActualQty);
    }

    [Fact]
    public async Task RecountWithTheSameQuantity_RequiresANewVoucherAndCanBeApproved()
    {
        var counted = StocktakeResponse(await _controller.Create(CountRequest(0, 1), default));
        var inventory = await _db.ProjectInventories.FirstAsync(i => i.MaterialCode == CountedMaterial);
        inventory.ApplyActualImport(3);
        await _db.SaveChangesAsync();

        var cancelled = StocktakeResponse(await _controller.Update(counted.Id, counted with { Status = "cancelled" }, default));
        Assert.Equal(2m, cancelled.Items.Single().SystemQty);
        var recounted = StocktakeResponse(await _controller.Create(CountRequest(0, 1), default));

        Assert.NotEqual(counted.Id, recounted.Id);
        Assert.Equal(5m, recounted.Items.Single().SystemQty);
        Assert.Equal(1m, recounted.Items.Single().ActualQty);
        Assert.IsType<OkObjectResult>((await _controller.Approve(recounted.Id, default)).Result);
        Assert.Equal(1m, (await _db.ProjectInventories.FirstAsync(i => i.MaterialCode == CountedMaterial)).ActualInventoryQty);
    }

    [Fact]
    public async Task Approve_WhenInventoryMovedSinceCount_RejectsWithoutChangingStockOrStatus()
    {
        var result = await _controller.Create(CountRequest(2, 1), default);
        var created = Assert.IsType<StocktakeDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var inventory = await _db.ProjectInventories.FirstAsync(i => i.MaterialCode == CountedMaterial);
        inventory.ApplyActualImport(3);
        await _db.SaveChangesAsync();

        var failure = Assert.IsType<BadRequestObjectResult>((await _controller.Approve(created.Id, default)).Result);
        Assert.Contains("đã thay đổi", Assert.IsType<string>(failure.Value));
        Assert.Equal(5m, (await _db.ProjectInventories.FirstAsync(i => i.MaterialCode == CountedMaterial)).ActualInventoryQty);
        Assert.Equal("pending", (await _db.Stocktakes.FirstAsync(i => i.Id == created.Id)).Status);
    }

    [Fact]
    public async Task Create_RejectsDuplicateMaterialsAndNegativeCounts()
    {
        var request = CountRequest(2, 1);
        var duplicate = await _controller.Create(request with { Items = [request.Items[0], request.Items[0]] }, default);
        Assert.IsType<BadRequestObjectResult>(duplicate.Result);
        Assert.IsType<BadRequestObjectResult>((await _controller.Create(CountRequest(2, -1), default)).Result);
        Assert.Empty(await _db.Stocktakes.ToListAsync());
    }

    private const string CountedMaterial = "IN-INH-SU304-N1-50x50x4x6000";

    private static StocktakeDto StocktakeResponse(ActionResult<StocktakeDto> response) =>
        Assert.IsType<StocktakeDto>(Assert.IsType<OkObjectResult>(response.Result).Value);

    private static CreateStocktakeDto CountRequest(decimal systemQty, decimal actualQty) =>
        new(null, "27/10/2026", null, "pending", "DA-2026-230", "Liên Hạnh", "counting", false, "Count regression",
            [new(CountedMaterial, CountedMaterial, "Inox 304", systemQty, actualQty, 999m)]);

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
