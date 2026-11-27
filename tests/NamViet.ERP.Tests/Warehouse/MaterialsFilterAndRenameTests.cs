using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Tests.Warehouse;

public class MaterialsFilterAndRenameTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly MaterialsController _controller;

    private const string ActiveCode = "ONG-PVC-D90-C2";
    private const string InactiveCode = "TH-TAM-SS400-1500";
    private const string SoftDeletedCode = "IN-INH-SU304-OLD";

    public MaterialsFilterAndRenameTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);

        _db.MaterialCategories.AddRange(
            new MaterialCategory { Id = 1, Code = "ONGPK", Name = "ỐNG & PHỤ KIỆN", Level = 1, ParentId = null, IsActive = true },
            new MaterialCategory { Id = 2, Code = "ONGPVC", Name = "ỐNG NHỰA PVC", Level = 2, ParentId = 1, IsActive = true },
            new MaterialCategory { Id = 3, Code = "KIMKHI", Name = "KIM KHÍ & THÉP", Level = 2, ParentId = 1, IsActive = true });

        _db.MaterialUnits.AddRange(
            new MaterialUnit { Id = 1, Name = "Cây", IsActive = true },
            new MaterialUnit { Id = 2, Name = "Tấm", IsActive = true },
            new MaterialUnit { Id = 3, Name = "Mét", IsActive = true });

        _db.Materials.AddRange(
            new Material
            {
                MaterialCode = ActiveCode,
                MaterialName = "Ống nhựa PVC D90 Class 2",
                CategoryId = 2,
                Unit = "Cây",
                Origin = "Việt Nam",
                TechnicalSpec = "D90x3.0x4000mm",
                IsActive = true,
                CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new Material
            {
                MaterialCode = InactiveCode,
                MaterialName = "Thép tấm SS400 1500x6000",
                CategoryId = 3,
                Unit = "Tấm",
                Origin = "Hàn Quốc",
                TechnicalSpec = "1500x4x6000mm",
                IsActive = false,
                CreatedAt = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            },
            new Material
            {
                MaterialCode = SoftDeletedCode,
                MaterialName = "Inox 304 đã xóa mềm",
                CategoryId = 3,
                Unit = "Mét",
                IsActive = false,
                DeletedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            });

        _db.SaveChanges();

        var excelService = new MaterialImportExcelService(_db);
        var renameService = new MaterialCodeRenameService(_db);
        _controller = new MaterialsController(_db, excelService, renameService: renameService);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task GetAll_DefaultParams_ReturnsOnlyActiveAndExcludesSoftDeleted()
    {
        var result = await _controller.GetAll(ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<PagedResult<MaterialDto>>(ok.Value);
        Assert.Single(page.Items);
        Assert.Equal(ActiveCode, page.Items[0].MaterialCode);
        Assert.True(page.Items[0].IsActive);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("ACTIVE")]
    [InlineData(" Active ")]
    public async Task GetAll_WithStatusActive_ReturnsOnlyActive(string status)
    {
        var result = await _controller.GetAll(status: status, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<PagedResult<MaterialDto>>(ok.Value);
        Assert.Single(page.Items);
        Assert.Equal(ActiveCode, page.Items[0].MaterialCode);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("INACTIVE")]
    public async Task GetAll_WithStatusInactive_ReturnsOnlyInactiveAndExcludesSoftDeleted(string status)
    {
        var result = await _controller.GetAll(status: status, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<PagedResult<MaterialDto>>(ok.Value);
        Assert.Single(page.Items);
        Assert.Equal(InactiveCode, page.Items[0].MaterialCode);
        Assert.False(page.Items[0].IsActive);
    }

    [Theory]
    [InlineData("all")]
    [InlineData("ALL")]
    public async Task GetAll_WithStatusAll_ReturnsActiveAndInactiveAndExcludesSoftDeleted(string status)
    {
        var result = await _controller.GetAll(status: status, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<PagedResult<MaterialDto>>(ok.Value);
        Assert.Equal(2, page.Items.Count);
        Assert.Contains(page.Items, m => m.MaterialCode == ActiveCode);
        Assert.Contains(page.Items, m => m.MaterialCode == InactiveCode);
        Assert.DoesNotContain(page.Items, m => m.MaterialCode == SoftDeletedCode);
    }

    [Fact]
    public async Task GetAll_WithIncludeInactiveTrueAndNoStatus_PreservesCompatibility()
    {
        var result = await _controller.GetAll(includeInactive: true, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<PagedResult<MaterialDto>>(ok.Value);
        Assert.Equal(2, page.Items.Count);
        Assert.Contains(page.Items, m => m.MaterialCode == ActiveCode);
        Assert.Contains(page.Items, m => m.MaterialCode == InactiveCode);
        Assert.DoesNotContain(page.Items, m => m.MaterialCode == SoftDeletedCode);
    }

    [Theory]
    [InlineData("archived")]
    [InlineData("deleted")]
    public async Task GetAll_WithInvalidStatus_ReturnsBadRequest(string status)
    {
        var result = await _controller.GetAll(status: status, ct: CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateByCodeQuery_RenamingMaterialCode_CascadesAcrossAllReferencingTablesAndPreservesState()
    {
        const string oldCode = InactiveCode;
        const string newCode = "TH-TAM-SS400-1500x4x6000";
        var originalCreatedAt = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);

        _db.Projects.Add(new Project
        {
            ProjectCode = "DA-NV-01",
            ProjectName = "Nhà máy cơ khí Nam Việt",
            PmId = Guid.NewGuid(),
            PmName = "Nguyễn Văn An",
            StartDate = new DateOnly(2026, 1, 10),
        });
        _db.Projects.Add(new Project
        {
            ProjectCode = "DA-NV-02",
            ProjectName = "Kho trung tâm Bình Dương",
            PmId = Guid.NewGuid(),
            PmName = "Trần Thị Bình",
            StartDate = new DateOnly(2026, 2, 1),
        });
        _db.WmsWarehouses.Add(new WmsWarehouse
        {
            Id = "WH-CENTRAL",
            Code = "KHO-TT",
            Name = "Kho trung tâm",
        });

        _db.ProjectInventories.Add(new ProjectInventory
        {
            ProjectCode = "DA-NV-01",
            MaterialCode = oldCode,
            ActualImportQty = 10m,
        });
        _db.WarehouseInventories.Add(new WarehouseInventory
        {
            WarehouseId = "WH-CENTRAL",
            MaterialCode = oldCode,
            Qty = 25m,
        });
        _db.WarehouseTransactions.Add(new WarehouseTransaction
        {
            TicketCode = "NK-2026-0001",
            TransactionType = TransactionType.Import,
            ProjectCode = "DA-NV-01",
            CreatedById = Guid.NewGuid(),
            CreatedByName = "Lê Hoàng Nam",
        });
        _db.WarehouseTransactionItems.Add(new WarehouseTransactionItem
        {
            TicketCode = "NK-2026-0001",
            MaterialCode = oldCode,
            Quantity = 10m,
            PlannedQuantity = 10m,
            UnitPrice = 1250000m,
        });
        _db.InventoryReservations.Add(new InventoryReservation
        {
            WarehouseId = "WH-CENTRAL",
            ProjectCode = "DA-NV-01",
            MaterialCode = oldCode,
            SourceTicketCode = "NK-2026-0001",
            ReservedQty = 5m,
        });
        _db.ProjectMaterialLoans.Add(new ProjectMaterialLoan
        {
            LoanCode = "MT-20260601-0001",
            BorrowingProjectCode = "DA-NV-01",
            LendingProjectCode = "DA-NV-02",
            MaterialCode = oldCode,
            LoanQty = 3m,
        });
        _db.Stocktakes.Add(new Stocktake
        {
            Id = "ST-001",
            VoucherCode = "KK-0001",
            VoucherDate = new DateOnly(2026, 6, 15),
            CreatedById = Guid.NewGuid(),
            CreatedByName = "Phạm Minh Tuấn",
            WarehouseId = "WH-CENTRAL",
            WarehouseName = "Kho trung tâm",
        });
        _db.StocktakeItems.Add(new StocktakeItem
        {
            StocktakeId = "ST-001",
            MaterialCode = oldCode,
            Sku = oldCode,
            Name = "Thép tấm SS400",
            SystemQty = 25m,
            ActualQty = 25m,
            Diff = 0m,
        });
        _db.PurchasePlans.Add(new PurchasePlan
        {
            Code = "KHMH-2026-001",
            ProjectCode = "DA-NV-01",
        });
        var matPlanLine = new PurchasePlanLine
        {
            PlanCode = "KHMH-2026-001",
            LineNo = 1,
            LineType = PurchasePlanLineTypes.Material,
            MaterialCode = oldCode,
            ProductName = "Thép tấm SS400",
            Unit = "Tấm",
            Quantity = 12m,
        };
        var svcPlanLine = new PurchasePlanLine
        {
            PlanCode = "KHMH-2026-001",
            LineNo = 2,
            LineType = PurchasePlanLineTypes.Service,
            MaterialCode = oldCode,
            ProductName = "Dịch vụ gia công cắt laser",
            Unit = "Lần",
            Quantity = 1m,
        };
        _db.PurchasePlanLines.AddRange(matPlanLine, svcPlanLine);
        _db.PurchaseOrders.Add(new PurchaseOrder
        {
            Code = "PO-2026-0001",
            ProjectCode = "DA-NV-01",
            PlanCode = "KHMH-2026-001",
            OrderDate = new DateOnly(2026, 6, 20),
        });
        _db.PurchaseOrderItems.Add(new PurchaseOrderItem
        {
            PoCode = "PO-2026-0001",
            MaterialCode = oldCode,
            Sku = oldCode,
            Name = "Thép tấm SS400",
            Unit = "Tấm",
            Ordered = 12m,
        });
        _db.Conversions.Add(new Conversion
        {
            Id = "KC-001",
            VoucherCode = "KC-0001",
            VoucherDate = new DateOnly(2026, 6, 22),
            FromWarehouseId = "WH-CENTRAL",
            FromWarehouseName = "Kho trung tâm",
            FromType = "central",
            ToWarehouseId = "DA-NV-01",
            ToWarehouseName = "Nhà máy cơ khí Nam Việt",
            ToType = "project",
            CreatedById = Guid.NewGuid(),
            CreatedByName = "Lê Hoàng Nam",
        });
        _db.ConversionItems.Add(new ConversionItem
        {
            ConversionId = "KC-001",
            MaterialCode = oldCode,
            Sku = oldCode,
            Name = "Thép tấm SS400",
            Qty = 2m,
            Unit = "Tấm",
            Reason = "Điều chuyển",
        });
        _db.ProductBarcodes.Add(new ProductBarcode
        {
            MaterialCode = oldCode,
            NumericSku = "8930000001234",
        });
        _db.ConsumableQuotas.Add(new ConsumableQuota
        {
            ProjectCode = "DA-NV-01",
            MaterialCode = oldCode,
            QuotaQty = 20m,
            IssuedQty = 5m,
        });
        _db.ProjectBoms.Add(new ProjectBom
        {
            ProjectCode = "DA-NV-01",
            MaterialCode = oldCode,
            Qty = 30m,
        });
        _db.PurchaseWarranties.Add(new PurchaseWarranty
        {
            Code = "BH-0001",
            PoCode = "PO-2026-0001",
            PoItemId = 1,
            MaterialCode = oldCode,
            MaterialName = "Thép tấm SS400",
            Unit = "Tấm",
            Quantity = 12m,
            WarrantyMonths = 12,
        });
        var estimate = new PurchaseEstimate
        {
            Code = "DT-0001",
            ProjectCode = "DA-NV-01",
            Title = "Dự toán kết cấu thép",
            CreatedById = Guid.NewGuid(),
            CreatedByName = "Nguyễn Văn An",
            UpdatedById = Guid.NewGuid(),
            UpdatedByName = "Nguyễn Văn An",
        };
        var estVersion = new PurchaseEstimateVersion
        {
            EstimateId = estimate.Id,
            VersionNumber = 1,
            UpdatedById = Guid.NewGuid(),
            UpdatedByName = "Nguyễn Văn An",
        };
        _db.PurchaseEstimates.Add(estimate);
        _db.PurchaseEstimateVersions.Add(estVersion);
        _db.PurchaseEstimateLines.Add(new PurchaseEstimateLine
        {
            VersionId = estVersion.Id,
            MaterialCode = oldCode,
            MaterialName = "Thép tấm SS400",
            Unit = "Tấm",
            OldQty = 10m,
            NewQty = 12m,
            EstimateUnitPrice = 1200000m,
        });
        _db.PurchaseRfqQuotes.Add(new PurchaseRfqQuote
        {
            MaterialCode = oldCode,
            MaterialName = "Thép tấm SS400",
            SupplierId = 1,
            SupplierName = "Công ty Thép Hòa Phát",
            QuoteDate = new DateOnly(2026, 6, 5),
            UnitPrice = 1220000m,
        });
        _db.PurchaseSellPrices.Add(new PurchaseSellPrice
        {
            MaterialCode = oldCode,
            MaterialName = "Thép tấm SS400",
            SellPrice = 1450000m,
            EffectiveDate = new DateOnly(2026, 6, 1),
            CreatedByName = "Nguyễn Văn An",
        });
        _db.PurchaseItemEstimatePrices.Add(new PurchaseItemEstimatePrice
        {
            MaterialCode = oldCode,
            MaterialName = "Thép tấm SS400",
            EstimatePrice = 1200000m,
            EffectiveDate = new DateOnly(2026, 6, 1),
            CreatedByName = "Nguyễn Văn An",
        });
        _db.PurchaseDefects.Add(new PurchaseDefect
        {
            TicketCode = "HL-0001",
            PlanCode = "KHMH-2026-001",
            ProjectCode = "DA-NV-01",
            CreatedById = Guid.NewGuid(),
            CreatedByName = "Lê Hoàng Nam",
        });
        _db.PurchaseDefectItems.Add(new PurchaseDefectItem
        {
            TicketCode = "HL-0001",
            MaterialCode = oldCode,
            Name = "Thép tấm SS400",
            Unit = "Tấm",
            Quantity = 1m,
        });
        _db.ActivityEvents.AddRange(
            new ActivityEvent
            {
                ActorName = "Nguyễn Văn An",
                Module = ActivityModules.Wms,
                Action = "material.updated",
                EntityType = "material",
                EntityCode = oldCode,
                Message = "Cập nhật vật tư",
                CategoryLabel = "Vật tư",
            },
            new ActivityEvent
            {
                ActorName = "Nguyễn Văn An",
                Module = ActivityModules.Purchasing,
                Action = ActivityActions.PoCreated,
                EntityType = "purchase_order",
                EntityCode = oldCode,
                Message = "Không đổi mã PO",
                CategoryLabel = ActivityCategories.Order,
            });
        await _db.SaveChangesAsync();

        var updateReq = new UpsertMaterialRequest(
            MaterialCode: $" {newCode} ",
            MaterialName: "Thép tấm SS400 1500x4x6000mm",
            CategoryId: 2,
            Unit: "Mét",
            TechnicalSpec: "1500x4x6000mm chuẩn ASTM",
            GeneralTechnicalSpec: "Thép tấm kết cấu cán nóng",
            QualityStandard: "ASTM A36",
            CeilingPrice: 1350000m,
            AbcClass: "a",
            IsConsumable: true,
            Origin: "Nhật Bản",
            ImageUrl: "/api/warehouse/materials/images/materials/ss400.png");

        var updateResult = await _controller.UpdateByCodeQuery(oldCode, updateReq, CancellationToken.None);
        Assert.IsType<NoContentResult>(updateResult);

        var oldLookup = await _controller.GetByCodeQuery(oldCode, CancellationToken.None);
        Assert.IsType<NotFoundResult>(oldLookup.Result);

        var newLookup = await _controller.GetByCodeQuery(newCode, CancellationToken.None);
        var okNew = Assert.IsType<OkObjectResult>(newLookup.Result);
        var dto = Assert.IsType<MaterialDto>(okNew.Value);
        Assert.Equal(newCode, dto.MaterialCode);
        Assert.Equal("Thép tấm SS400 1500x4x6000mm", dto.MaterialName);
        Assert.Equal(2, dto.CategoryId);
        Assert.Equal("Mét", dto.Unit);
        Assert.Equal("Nhật Bản", dto.Origin);
        Assert.Equal("/api/warehouse/materials/images/materials/ss400.png", dto.ImageUrl);
        Assert.Equal("1500x4x6000mm chuẩn ASTM", dto.TechnicalSpec);
        Assert.Equal("Thép tấm kết cấu cán nóng", dto.GeneralTechnicalSpec);
        Assert.Equal("ASTM A36", dto.QualityStandard);
        Assert.Equal(1350000m, dto.CeilingPrice);
        Assert.Equal("A", dto.AbcClass);
        Assert.True(dto.IsConsumable);
        Assert.False(dto.IsActive); // Preserved inactive state

        var persistedEntity = await _db.Materials.AsNoTracking().SingleAsync(m => m.MaterialCode == newCode);
        Assert.Equal(originalCreatedAt, persistedEntity.CreatedAt);

        Assert.Equal(newCode, (await _db.ProjectInventories.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.WarehouseInventories.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.WarehouseTransactionItems.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.InventoryReservations.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.ProjectMaterialLoans.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.StocktakeItems.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.PurchaseOrderItems.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.ConversionItems.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.ProductBarcodes.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.ConsumableQuotas.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.ProjectBoms.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.PurchaseWarranties.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.PurchaseEstimateLines.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.PurchaseRfqQuotes.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.PurchaseSellPrices.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.PurchaseItemEstimatePrices.SingleAsync()).MaterialCode);
        Assert.Equal(newCode, (await _db.PurchaseDefectItems.SingleAsync()).MaterialCode);

        var updatedPlanLines = await _db.PurchasePlanLines.OrderBy(l => l.LineNo).ToListAsync();
        Assert.Equal(newCode, updatedPlanLines[0].MaterialCode);
        Assert.Equal(oldCode, updatedPlanLines[1].MaterialCode); // Service line left untouched

        var matEvent = await _db.ActivityEvents.SingleAsync(a => a.EntityType == "material");
        var poEvent = await _db.ActivityEvents.SingleAsync(a => a.EntityType == "purchase_order");
        Assert.Equal(newCode, matEvent.EntityCode);
        Assert.Equal(oldCode, poEvent.EntityCode);
    }

    [Fact]
    public async Task UpdateByCodeQuery_RenameToExistingActiveCode_ReturnsConflict()
    {
        var req = new UpsertMaterialRequest(
            MaterialCode: ActiveCode,
            MaterialName: "Thép tấm trùng mã đang hoạt động",
            CategoryId: 3,
            Unit: "Tấm",
            TechnicalSpec: null,
            GeneralTechnicalSpec: null,
            QualityStandard: null);

        var result = await _controller.UpdateByCodeQuery(InactiveCode, req, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task UpdateByCodeQuery_RenameToSoftDeletedCode_ReturnsConflict()
    {
        var req = new UpsertMaterialRequest(
            MaterialCode: SoftDeletedCode.ToLowerInvariant(),
            MaterialName: "Thép tấm trùng mã đã xóa mềm",
            CategoryId: 3,
            Unit: "Tấm",
            TechnicalSpec: null,
            GeneralTechnicalSpec: null,
            QualityStandard: null);

        var result = await _controller.UpdateByCodeQuery(ActiveCode, req, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateByCodeQuery_EmptyOrWhitespaceNewCode_ReturnsBadRequest(string invalidCode)
    {
        var req = new UpsertMaterialRequest(
            MaterialCode: invalidCode,
            MaterialName: "Ống nhựa PVC D90 Class 2",
            CategoryId: 2,
            Unit: "Cây",
            TechnicalSpec: null,
            GeneralTechnicalSpec: null,
            QualityStandard: null);

        var result = await _controller.UpdateByCodeQuery(ActiveCode, req, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateByCodeQuery_NewCodeExceeding100Chars_ReturnsBadRequest()
    {
        var tooLongCode = new string('M', 101);
        var req = new UpsertMaterialRequest(
            MaterialCode: tooLongCode,
            MaterialName: "Ống nhựa PVC D90 Class 2",
            CategoryId: 2,
            Unit: "Cây",
            TechnicalSpec: null,
            GeneralTechnicalSpec: null,
            QualityStandard: null);

        var result = await _controller.UpdateByCodeQuery(ActiveCode, req, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateByCodeQuery_CaseOnlyRename_SucceedsCleanly()
    {
        const string lowerCode = "vt-keo-sil-300";
        const string upperCode = "VT-KEO-SIL-300";

        _db.Materials.Add(new Material
        {
            MaterialCode = lowerCode,
            MaterialName = "Keo silicone trung tính",
            CategoryId = 2,
            Unit = "Cây",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        _db.ProductBarcodes.Add(new ProductBarcode
        {
            MaterialCode = lowerCode,
            NumericSku = "8930000009999",
        });
        await _db.SaveChangesAsync();

        var req = new UpsertMaterialRequest(
            MaterialCode: upperCode,
            MaterialName: "Keo silicone trung tính 300ml",
            CategoryId: 2,
            Unit: "Cây",
            TechnicalSpec: "Tuýp 300ml",
            GeneralTechnicalSpec: null,
            QualityStandard: null);

        var result = await _controller.UpdateByCodeQuery(lowerCode, req, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.False(await _db.Materials.AnyAsync(m => m.MaterialCode == lowerCode));
        var renamed = await _db.Materials.SingleAsync(m => m.MaterialCode == upperCode);
        Assert.Equal("Keo silicone trung tính 300ml", renamed.MaterialName);
        Assert.Equal(upperCode, (await _db.ProductBarcodes.SingleAsync(b => b.NumericSku == "8930000009999")).MaterialCode);
    }

    [Fact]
    public async Task UpdateByCodeQuery_WhenRenameServiceThrowsDomainRuleException_ReturnsConflict()
    {
        var controller = new MaterialsController(
            _db,
            new MaterialImportExcelService(_db),
            renameService: new ThrowingConflictRenameService());

        var req = new UpsertMaterialRequest(
            MaterialCode: "ONG-PVC-D110-C2",
            MaterialName: "Ống nhựa PVC D110",
            CategoryId: 2,
            Unit: "Cây",
            TechnicalSpec: null,
            GeneralTechnicalSpec: null,
            QualityStandard: null);

        var result = await controller.UpdateByCodeQuery(ActiveCode, req, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("đã tồn tại", conflict.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingConflictRenameService : IMaterialCodeRenameService
    {
        public Task RenameAndUpdateAsync(Material existing, string newCode, MaterialUpdateFields fields, CancellationToken ct = default)
            => throw new DomainRuleException($"Mã vật tư '{newCode}' đã tồn tại trong hệ thống.");
    }
}
