using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Controllers.Wms;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Tests.Warehouse;

public class MaterialOriginsControllerTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly MaterialOriginsController _controller;

    public MaterialOriginsControllerTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);
        _controller = new MaterialOriginsController(_db);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task List_ReturnsEmptyList_WhenDatabaseHasNoOrigins()
    {
        var result = await _controller.List(activeOnly: false, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<MaterialOriginDto>>(ok.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task List_FiltersActiveOnly_Correctly()
    {
        _db.MaterialOrigins.AddRange(
            new MaterialOrigin { Name = "Việt Nam", SortOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow },
            new MaterialOrigin { Name = "Cũ ngừng dùng", SortOrder = 2, IsActive = false, CreatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var activeResult = await _controller.List(activeOnly: true, CancellationToken.None);
        var okActive = Assert.IsType<OkObjectResult>(activeResult.Result);
        var activeList = Assert.IsType<List<MaterialOriginDto>>(okActive.Value);
        Assert.Single(activeList);
        Assert.Equal("Việt Nam", activeList[0].Name);

        var allResult = await _controller.List(activeOnly: false, CancellationToken.None);
        var okAll = Assert.IsType<OkObjectResult>(allResult.Result);
        var allList = Assert.IsType<List<MaterialOriginDto>>(okAll.Value);
        Assert.Equal(2, allList.Count);
    }

    [Fact]
    public async Task Create_RejectsEmptyName()
    {
        var result = await _controller.Create(new UpsertMaterialOriginRequest("   "), CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Vui lòng nhập tên xuất xứ.", bad.Value);
    }

    [Fact]
    public async Task Create_RejectsDuplicateName_CaseInsensitive()
    {
        _db.MaterialOrigins.Add(new MaterialOrigin
        {
            Name = "Nhật Bản",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _controller.Create(new UpsertMaterialOriginRequest("nhật bản"), CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_Succeeds_AndPersistsInDatabase()
    {
        var result = await _controller.Create(new UpsertMaterialOriginRequest("  Hàn Quốc  ", 1, true), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<MaterialOriginDto>(ok.Value);
        Assert.Equal("Hàn Quốc", dto.Name);
        Assert.True(dto.IsActive);

        var inDb = await _db.MaterialOrigins.FirstOrDefaultAsync(o => o.Name == "Hàn Quốc");
        Assert.NotNull(inDb);
        Assert.Equal("Hàn Quốc", inDb.Name);
    }

    [Fact]
    public async Task Update_CascadesOriginNameToMaterials_WhenNameChanges()
    {
        var origin = new MaterialOrigin
        {
            Name = "Đức",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.MaterialOrigins.Add(origin);

        var mat = new Material
        {
            MaterialCode = "MAT-GER-01",
            MaterialName = "Ống thép nhập Đức",
            CategoryId = 1,
            Unit = "Cây",
            Origin = "Đức",
            CreatedAt = DateTime.UtcNow
        };
        _db.Materials.Add(mat);
        await _db.SaveChangesAsync();

        var updateResult = await _controller.Update(
            origin.Id,
            new UpsertMaterialOriginRequest("Cộng Hòa Liên Bang Đức", 1, true),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(updateResult);

        var updatedMat = await _db.Materials.FirstAsync(m => m.MaterialCode == "MAT-GER-01");
        Assert.Equal("Cộng Hòa Liên Bang Đức", updatedMat.Origin);
    }

    [Fact]
    public async Task Delete_RejectsWhenInUseByMaterial()
    {
        var origin = new MaterialOrigin
        {
            Name = "Mỹ",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.MaterialOrigins.Add(origin);

        var mat = new Material
        {
            MaterialCode = "MAT-USA-01",
            MaterialName = "Van áp lực",
            CategoryId = 1,
            Unit = "Cái",
            Origin = "Mỹ",
            CreatedAt = DateTime.UtcNow
        };
        _db.Materials.Add(mat);
        await _db.SaveChangesAsync();

        var deleteResult = await _controller.Delete(origin.Id, CancellationToken.None);
        var conflict = Assert.IsType<ConflictObjectResult>(deleteResult);
        Assert.NotNull(conflict.Value);
    }

    [Fact]
    public async Task Delete_Succeeds_WhenNotInUse()
    {
        var origin = new MaterialOrigin
        {
            Name = "Thụy Điển",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.MaterialOrigins.Add(origin);
        await _db.SaveChangesAsync();

        var deleteResult = await _controller.Delete(origin.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(deleteResult);

        var inDb = await _db.MaterialOrigins.FirstOrDefaultAsync(o => o.Id == origin.Id);
        Assert.Null(inDb);
    }
}
