using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> List(CancellationToken ct)
    {
        var materials = await db.Materials
            .Include(m => m.Category)
            .Where(m => m.DeletedAt == null)
            .ToListAsync(ct);
        var result = new List<ProductDto>();
        foreach (var m in materials)
        {
            var price = await WmsInventoryHelper.GetLatestPriceAsync(db, m.MaterialCode, ct);
            result.Add(WmsMapper.ToProduct(m, price));
        }
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetById(string id, CancellationToken ct)
    {
        var m = await db.Materials
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.MaterialCode == id && x.DeletedAt == null, ct);
        if (m is null) return NotFound();
        var price = await WmsInventoryHelper.GetLatestPriceAsync(db, m.MaterialCode, ct);
        return Ok(WmsMapper.ToProduct(m, price));
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductDto dto, CancellationToken ct)
    {
        if (await db.Materials.AnyAsync(m => m.MaterialCode == dto.Sku, ct))
            return Conflict($"SKU '{dto.Sku}' đã tồn tại.");

        var categoryId = await ResolveCategoryIdAsync(dto.Category, ct);
        var m = new Material
        {
            MaterialCode = dto.Sku,
            MaterialName = dto.Name,
            CategoryId = categoryId,
            Unit = dto.Unit
        };
        db.Materials.Add(m);
        if (dto.Price > 0)
        {
            var supplierId = await db.Suppliers.Select(s => s.Id).FirstOrDefaultAsync(ct);
            if (supplierId > 0)
            {
                db.PriceBooks.Add(new PriceBook
                {
                    MaterialCode = dto.Sku,
                    SupplierId = supplierId,
                    UnitPrice = dto.Price,
                    PriceDate = DateTime.UtcNow
                });
            }
        }
        await db.SaveChangesAsync(ct);
        await db.Entry(m).Reference(x => x.Category).LoadAsync(ct);
        return Ok(WmsMapper.ToProduct(m, dto.Price));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProductDto>> Update(string id, [FromBody] UpdateProductDto dto, CancellationToken ct)
    {
        var m = await db.Materials
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.MaterialCode == id && x.DeletedAt == null, ct);
        if (m is null) return NotFound();
        if (dto.Name is not null) m.MaterialName = dto.Name;
        if (dto.Category is not null) m.CategoryId = await ResolveCategoryIdAsync(dto.Category, ct);
        if (dto.Unit is not null) m.Unit = dto.Unit;
        m.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await db.Entry(m).Reference(x => x.Category).LoadAsync(ct);
        var price = await WmsInventoryHelper.GetLatestPriceAsync(db, m.MaterialCode, ct);
        return Ok(WmsMapper.ToProduct(m, price));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var m = await db.Materials.FirstOrDefaultAsync(x => x.MaterialCode == id && x.DeletedAt == null, ct);
        if (m is null) return NotFound();
        m.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<int> ResolveCategoryIdAsync(string? categoryName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return await GetUncategorizedIdAsync(ct);

        var byName = await db.MaterialCategories
            .Where(c => c.Name == categoryName)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);
        if (byName != 0) return byName;

        var uncategorized = await GetUncategorizedIdAsync(ct);
        var category = new MaterialCategory
        {
            Code = $"LEGACY-{Guid.NewGuid():N}"[..20],
            Name = categoryName,
            Level = 2,
            ParentId = uncategorized,
            SortOrder = 0
        };
        db.MaterialCategories.Add(category);
        await db.SaveChangesAsync(ct);
        return category.Id;
    }

    private async Task<int> GetUncategorizedIdAsync(CancellationToken ct) =>
        await db.MaterialCategories
            .Where(c => c.Code == MaterialCategoryCodes.Uncategorized)
            .Select(c => c.Id)
            .FirstAsync(ct);
}
