using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
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
        var materials = await db.Materials.Where(m => m.DeletedAt == null).ToListAsync(ct);
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
        var m = await db.Materials.FirstOrDefaultAsync(x => x.MaterialCode == id && x.DeletedAt == null, ct);
        if (m is null) return NotFound();
        var price = await WmsInventoryHelper.GetLatestPriceAsync(db, m.MaterialCode, ct);
        return Ok(WmsMapper.ToProduct(m, price));
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductDto dto, CancellationToken ct)
    {
        if (await db.Materials.AnyAsync(m => m.MaterialCode == dto.Sku, ct))
            return Conflict($"SKU '{dto.Sku}' đã tồn tại.");
        var m = new Material
        {
            MaterialCode = dto.Sku,
            MaterialName = dto.Name,
            MaterialCategory = dto.Category,
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
        return Ok(WmsMapper.ToProduct(m, dto.Price));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProductDto>> Update(string id, [FromBody] UpdateProductDto dto, CancellationToken ct)
    {
        var m = await db.Materials.FirstOrDefaultAsync(x => x.MaterialCode == id && x.DeletedAt == null, ct);
        if (m is null) return NotFound();
        if (dto.Name is not null) m.MaterialName = dto.Name;
        if (dto.Category is not null) m.MaterialCategory = dto.Category;
        if (dto.Unit is not null) m.Unit = dto.Unit;
        m.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
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
}
