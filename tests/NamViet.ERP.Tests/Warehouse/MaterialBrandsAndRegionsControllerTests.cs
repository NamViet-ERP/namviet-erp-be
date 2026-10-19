using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Controllers.Wms;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Tests.Warehouse;

public class MaterialBrandsAndRegionsControllerTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly MaterialRegionsController _regionsController;
    private readonly MaterialBrandsController _brandsController;
    private readonly MaterialOriginsController _originsController;

    public MaterialBrandsAndRegionsControllerTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);
        _regionsController = new MaterialRegionsController(_db);
        _brandsController = new MaterialBrandsController(_db);
        _originsController = new MaterialOriginsController(_db);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task Regions_Create_List_Update_Delete_WorksCorrectly()
    {
        // 1. Create
        var createResult = await _regionsController.Create(
            new UpsertMaterialRegionRequest("G7", "Khối các nước công nghiệp G7", 1, true),
            CancellationToken.None);
        var okCreate = Assert.IsType<OkObjectResult>(createResult.Result);
        var regionDto = Assert.IsType<MaterialRegionDto>(okCreate.Value);
        Assert.Equal("G7", regionDto.Name);
        Assert.Equal("Khối các nước công nghiệp G7", regionDto.Description);

        // 2. List
        var listResult = await _regionsController.List(false, CancellationToken.None);
        var okList = Assert.IsType<OkObjectResult>(listResult.Result);
        var list = Assert.IsType<List<MaterialRegionDto>>(okList.Value);
        Assert.Single(list);

        // 3. Update
        var updateResult = await _regionsController.Update(
            regionDto.Id,
            new UpsertMaterialRegionRequest("G7+", "Nhóm G7 mở rộng", 1, true),
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(updateResult);

        // 4. Delete
        var deleteResult = await _regionsController.Delete(regionDto.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(deleteResult);
    }

    [Fact]
    public async Task Brands_Create_List_Update_Delete_WorksCorrectly()
    {
        // 1. Create
        var createResult = await _brandsController.Create(
            new UpsertMaterialBrandRequest("Hòa Phát", "Việt Nam", "Thương hiệu thép hàng đầu VN", 1, true),
            CancellationToken.None);
        var okCreate = Assert.IsType<OkObjectResult>(createResult.Result);
        var brandDto = Assert.IsType<MaterialBrandDto>(okCreate.Value);
        Assert.Equal("Hòa Phát", brandDto.Name);
        Assert.Equal("Việt Nam", brandDto.Country);

        // 2. List
        var listResult = await _brandsController.List(false, CancellationToken.None);
        var okList = Assert.IsType<OkObjectResult>(listResult.Result);
        var list = Assert.IsType<List<MaterialBrandDto>>(okList.Value);
        Assert.Single(list);

        // 3. Update
        var updateResult = await _brandsController.Update(
            brandDto.Id,
            new UpsertMaterialBrandRequest("Tập đoàn Hòa Phát", "Việt Nam", "Thép Hòa Phát", 1, true),
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(updateResult);

        // 4. Delete
        var deleteResult = await _brandsController.Delete(brandDto.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(deleteResult);
    }

    [Fact]
    public async Task Origins_Supports_Region_Association()
    {
        var createResult = await _originsController.Create(
            new UpsertMaterialOriginRequest("Đức", "G7", 1, true),
            CancellationToken.None);
        var okCreate = Assert.IsType<OkObjectResult>(createResult.Result);
        var originDto = Assert.IsType<MaterialOriginDto>(okCreate.Value);
        Assert.Equal("Đức", originDto.Name);
        Assert.Equal("G7", originDto.Region);

        var listResult = await _originsController.List(false, CancellationToken.None);
        var okList = Assert.IsType<OkObjectResult>(listResult.Result);
        var list = Assert.IsType<List<MaterialOriginDto>>(okList.Value);
        Assert.Single(list);
        Assert.Equal("G7", list[0].Region);
    }
}
