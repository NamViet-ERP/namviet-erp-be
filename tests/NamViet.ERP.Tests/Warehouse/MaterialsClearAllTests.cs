using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using Xunit;

namespace NamViet.ERP.Tests.Warehouse;

public class MaterialsClearAllTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly MaterialImportExcelService _excelService;

    public MaterialsClearAllTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);

        // Seed Category
        _db.MaterialCategories.Add(new MaterialCategory
        {
            Id = 1,
            Code = "CAT1",
            Name = "Danh mục 1",
            Level = 2,
            IsActive = true
        });

        // Seed Unit
        _db.MaterialUnits.Add(new MaterialUnit
        {
            Id = 1,
            Name = "Cái",
            IsActive = true
        });

        _db.SaveChanges();
        _excelService = new MaterialImportExcelService(_db);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private MaterialsController CreateController(string? role = null)
    {
        var controller = new MaterialsController(_db, _excelService);
        var httpContext = new DefaultHttpContext();
        if (role != null)
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }
        else
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    [Fact]
    public async Task ClearAll_WhenUserIsNotAdmin_ReturnsForbid()
    {
        // Arrange
        _db.Materials.Add(new Material
        {
            MaterialCode = "MAT-001",
            MaterialName = "Vật tư 1",
            CategoryId = 1,
            Unit = "Cái",
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var controller = CreateController("Staff");

        // Act
        var result = await controller.ClearAll(CancellationToken.None);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);

        // Verify data was NOT deleted
        var remainingCount = await _db.Materials.CountAsync(m => m.DeletedAt == null);
        Assert.Equal(1, remainingCount);
    }

    [Fact]
    public async Task ClearAll_WhenUnauthenticated_ReturnsForbid()
    {
        // Arrange
        _db.Materials.Add(new Material
        {
            MaterialCode = "MAT-002",
            MaterialName = "Vật tư 2",
            CategoryId = 1,
            Unit = "Cái",
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var controller = CreateController(null);

        // Act
        var result = await controller.ClearAll(CancellationToken.None);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
        var remainingCount = await _db.Materials.CountAsync(m => m.DeletedAt == null);
        Assert.Equal(1, remainingCount);
    }

    [Fact]
    public async Task ClearAll_WhenUserIsAdmin_ClearsUnusedMaterialsViaHardDelete()
    {
        // Arrange
        _db.Materials.AddRange(
            new Material
            {
                MaterialCode = "MAT-BOGUS-1",
                MaterialName = "Vật tư lỗi 1",
                CategoryId = 1,
                Unit = "Cái",
                IsActive = true
            },
            new Material
            {
                MaterialCode = "MAT-BOGUS-2",
                MaterialName = "Vật tư lỗi 2",
                CategoryId = 1,
                Unit = "Cái",
                IsActive = true
            }
        );

        _db.ProductBarcodes.Add(new ProductBarcode
        {
            Id = 101,
            MaterialCode = "MAT-BOGUS-1",
            NumericSku = "893000000001"
        });

        await _db.SaveChangesAsync();

        var controller = CreateController("Admin");

        // Act
        var result = await controller.ClearAll(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ClearAllMaterialsResultDto>(okResult.Value);

        Assert.Equal(2, dto.TotalProcessed);
        Assert.Equal(2, dto.HardDeleted);
        Assert.Equal(0, dto.SoftDeleted);

        // Active catalog must be 0
        var activeCount = await _db.Materials.CountAsync(m => m.DeletedAt == null);
        Assert.Equal(0, activeCount);

        // Barcode must also be removed
        var barcodeCount = await _db.ProductBarcodes.CountAsync(b => b.MaterialCode == "MAT-BOGUS-1");
        Assert.Equal(0, barcodeCount);
    }

    [Fact]
    public async Task ClearAll_WhenMaterialIsInUse_SoftDeletesInUseMaterial()
    {
        // Arrange
        _db.Materials.AddRange(
            new Material
            {
                MaterialCode = "MAT-UNUSED",
                MaterialName = "Vật tư chưa dùng",
                CategoryId = 1,
                Unit = "Cái",
                IsActive = true
            },
            new Material
            {
                MaterialCode = "MAT-IN-USE",
                MaterialName = "Vật tư đã giao dịch",
                CategoryId = 1,
                Unit = "Cái",
                IsActive = true
            }
        );

        _db.WarehouseTransactionItems.Add(new WarehouseTransactionItem
        {
            Id = 501,
            TicketCode = "TICKET-01",
            MaterialCode = "MAT-IN-USE",
            Quantity = 10,
            UnitSnapshot = "Cái"
        });

        await _db.SaveChangesAsync();

        var controller = CreateController("Admin");

        // Act
        var result = await controller.ClearAll(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ClearAllMaterialsResultDto>(okResult.Value);

        Assert.Equal(2, dto.TotalProcessed);
        Assert.Equal(1, dto.HardDeleted);
        Assert.Equal(1, dto.SoftDeleted);

        // Active materials count must be 0!
        var activeCount = await _db.Materials.CountAsync(m => m.DeletedAt == null);
        Assert.Equal(0, activeCount);

        // Hard deleted is gone from DB
        var unusedInDb = await _db.Materials.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.MaterialCode == "MAT-UNUSED");
        Assert.Null(unusedInDb);

        // Soft deleted remains in DB for historical integrity
        var inUseInDb = await _db.Materials.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.MaterialCode == "MAT-IN-USE");
        Assert.NotNull(inUseInDb);
        Assert.NotNull(inUseInDb.DeletedAt);
        Assert.False(inUseInDb.IsActive);
    }

    [Fact]
    public async Task ClearAll_WhenCatalogEmpty_ReturnsZeroProcessed()
    {
        // Arrange
        var controller = CreateController("Admin");

        // Act
        var result = await controller.ClearAll(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ClearAllMaterialsResultDto>(okResult.Value);

        Assert.Equal(0, dto.TotalProcessed);
        Assert.Equal(0, dto.HardDeleted);
        Assert.Equal(0, dto.SoftDeleted);
    }
}
