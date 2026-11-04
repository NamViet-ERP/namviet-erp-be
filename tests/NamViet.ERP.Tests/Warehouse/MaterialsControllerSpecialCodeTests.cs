using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Tests.Warehouse;

public class MaterialsControllerSpecialCodeTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly MaterialsController _controller;
    private const string SpecialCode = "BÔ-ONG-120 kg/m3-Rockwool-Ø114x50x1000";

    public MaterialsControllerSpecialCodeTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);

        // Seed Category Level 1 & Level 2
        var rootCat = new MaterialCategory
        {
            Id = 1,
            Code = "BAOON",
            Name = "BẢO ÔN",
            Level = 1,
            ParentId = null,
            IsActive = true
        };
        var subCat = new MaterialCategory
        {
            Id = 2,
            Code = "ONGBAOON",
            Name = "ỐNG BẢO ÔN",
            Level = 2,
            ParentId = 1,
            IsActive = true
        };
        _db.MaterialCategories.AddRange(rootCat, subCat);

        // Seed Unit
        _db.MaterialUnits.Add(new MaterialUnit
        {
            Id = 1,
            Name = "Ống",
            IsActive = true
        });

        // Seed Material with special characters: slashes, symbols, spaces, uppercase
        _db.Materials.Add(new Material
        {
            MaterialCode = SpecialCode,
            MaterialName = "Ống bảo ôn Rockwool tỉ trọng 120 kg/m3 phi 114 dày 50",
            CategoryId = 2,
            Unit = "Ống",
            Origin = "Việt Nam",
            TechnicalSpec = "D114 x T50 x L1000, 120kg/m3",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        _db.SaveChanges();

        var excelService = new MaterialImportExcelService(_db);
        _controller = new MaterialsController(_db, excelService);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task GetByCodeQuery_WithSlashAndSpecialChars_ReturnsMaterial()
    {
        var result = await _controller.GetByCodeQuery(SpecialCode, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<MaterialDto>(okResult.Value);
        Assert.Equal(SpecialCode, dto.MaterialCode);
        Assert.Equal("Ống", dto.Unit);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task GetByCodeQuery_WithUrlEncodedSpecialCode_ReturnsMaterial()
    {
        var encodedCode = Uri.EscapeDataString(SpecialCode);
        var result = await _controller.GetByCodeQuery(encodedCode, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<MaterialDto>(okResult.Value);
        Assert.Equal(SpecialCode, dto.MaterialCode);
    }

    [Fact]
    public async Task GetById_WithCatchAllRouteSpecialCode_ReturnsMaterial()
    {
        var result = await _controller.GetById(SpecialCode, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<MaterialDto>(okResult.Value);
        Assert.Equal(SpecialCode, dto.MaterialCode);
    }

    [Fact]
    public async Task UpdateByCodeQuery_WithSpecialCode_UpdatesSuccessfully()
    {
        var updateReq = new UpsertMaterialRequest(
            MaterialCode: SpecialCode,
            MaterialName: "Tên mới cập nhật",
            CategoryId: 2,
            Unit: "Ống",
            Origin: "Malaysia",
            ImageUrl: null,
            TechnicalSpec: "Quy cách nâng cấp",
            GeneralTechnicalSpec: null,
            QualityStandard: null,
            CeilingPrice: 150000m,
            AbcClass: "A",
            IsConsumable: false
        );

        var updateResult = await _controller.UpdateByCodeQuery(SpecialCode, updateReq, CancellationToken.None);
        Assert.IsType<NoContentResult>(updateResult);

        var fetched = await _controller.GetByCodeQuery(SpecialCode, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(fetched.Result);
        var dto = Assert.IsType<MaterialDto>(okResult.Value);
        Assert.Equal("Tên mới cập nhật", dto.MaterialName);
        Assert.Equal(150000m, dto.CeilingPrice);
        Assert.Equal("A", dto.AbcClass);
    }

    [Fact]
    public async Task DiscontinueAndReactivate_ByQuery_TogglesActiveState()
    {
        // 1. Discontinue
        var discResult = await _controller.DiscontinueByQuery(SpecialCode, CancellationToken.None);
        Assert.IsType<NoContentResult>(discResult);

        var fetchedAfterDisc = await _controller.GetByCodeQuery(SpecialCode, CancellationToken.None);
        var okDisc = Assert.IsType<OkObjectResult>(fetchedAfterDisc.Result);
        var dtoDisc = Assert.IsType<MaterialDto>(okDisc.Value);
        Assert.False(dtoDisc.IsActive);

        // 2. Reactivate
        var reactResult = await _controller.ReactivateByQuery(SpecialCode, CancellationToken.None);
        Assert.IsType<NoContentResult>(reactResult);

        var fetchedAfterReact = await _controller.GetByCodeQuery(SpecialCode, CancellationToken.None);
        var okReact = Assert.IsType<OkObjectResult>(fetchedAfterReact.Result);
        var dtoReact = Assert.IsType<MaterialDto>(okReact.Value);
        Assert.True(dtoReact.IsActive);
    }

    [Fact]
    public async Task DeleteByCodeQuery_SoftDeletesMaterial()
    {
        var deleteResult = await _controller.DeleteByCodeQuery(SpecialCode, CancellationToken.None);
        Assert.IsType<NoContentResult>(deleteResult);

        var fetched = await _controller.GetByCodeQuery(SpecialCode, CancellationToken.None);
        Assert.IsType<NotFoundResult>(fetched.Result);
    }

    [Fact]
    public async Task CleanupFalseEquipmentSpecs_RemovesFalseEquipmentSpecsFromNonEquipment()
    {
        // 1. Add non-equipment material with false specs
        var falseSpecMaterial = new Material
        {
            MaterialCode = "BÔ-CUN-TEST-CLEANUP",
            MaterialName = "Cuộn bảo ôn test",
            CategoryId = 2,
            Unit = "Ống",
            GeneralTechnicalSpec = "600kW | 25 m³/h | 3600 Pa",
            IsActive = true
        };

        // 2. Add real equipment material
        var equipmentMaterial = new Material
        {
            MaterialCode = "ĐC-MOT-TEST",
            MaterialName = "Động cơ điện 160kW",
            CategoryId = 2,
            Unit = "Bộ",
            GeneralTechnicalSpec = "160kW | 71500 m³/h | Động cơ điện",
            IsActive = true
        };

        _db.Materials.AddRange(falseSpecMaterial, equipmentMaterial);
        await _db.SaveChangesAsync();

        // 3. Trigger cleanup
        var cleanupRes = await _controller.CleanupFalseEquipmentSpecs(CancellationToken.None);
        var okRes = Assert.IsType<OkObjectResult>(cleanupRes);

        // 4. Verify false spec was removed from BÔ-...
        var cleanedBaoOn = await _db.Materials.FindAsync("BÔ-CUN-TEST-CLEANUP");
        Assert.NotNull(cleanedBaoOn);
        Assert.Null(cleanedBaoOn.GeneralTechnicalSpec);

        // 5. Verify real equipment spec was preserved
        var preservedEquip = await _db.Materials.FindAsync("ĐC-MOT-TEST");
        Assert.NotNull(preservedEquip);
        Assert.Equal("160kW | 71500 m³/h | Động cơ điện", preservedEquip.GeneralTechnicalSpec);
    }
}
