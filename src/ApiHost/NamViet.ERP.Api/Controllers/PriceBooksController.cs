using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/price-books")]
[Authorize]
public class PriceBooksController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? materialCode = null, [FromQuery] int? supplierId = null,
        CancellationToken ct = default)
    {
        var query = db.PriceBooks
            .Include(p => p.Material)
            .Include(p => p.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(materialCode))
            query = query.Where(p => p.MaterialCode == materialCode);
        if (supplierId.HasValue)
            query = query.Where(p => p.SupplierId == supplierId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.PriceDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new PriceBookDto(
                p.Id, p.MaterialCode, p.Material.MaterialName,
                p.SupplierId, p.Supplier.SupplierName,
                p.UnitPrice, p.PurchaseUrl, p.CatalogUrl, p.PriceDate))
            .ToListAsync(ct);

        return Ok(PagedResult<PriceBookDto>.Create(items, total, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var p = await db.PriceBooks
            .Include(x => x.Material)
            .Include(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return NotFound();
        return Ok(new PriceBookDto(
            p.Id, p.MaterialCode, p.Material.MaterialName,
            p.SupplierId, p.Supplier.SupplierName,
            p.UnitPrice, p.PurchaseUrl, p.CatalogUrl, p.PriceDate));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Procurement")]
    public async Task<IActionResult> Create([FromBody] CreatePriceBookRequest request, CancellationToken ct)
    {
        if (!await db.Materials.AnyAsync(m => m.MaterialCode == request.MaterialCode, ct))
            return BadRequest($"Vật tư '{request.MaterialCode}' không tồn tại.");
        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct))
            return BadRequest($"Nhà cung cấp ID={request.SupplierId} không tồn tại.");

        var entry = new PriceBook
        {
            MaterialCode = request.MaterialCode,
            SupplierId = request.SupplierId,
            UnitPrice = request.UnitPrice,
            PurchaseUrl = request.PurchaseUrl,
            CatalogUrl = request.CatalogUrl,
            PriceDate = DateTime.UtcNow
        };
        db.PriceBooks.Add(entry);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = entry.Id }, null);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Procurement")]
    public async Task<IActionResult> Update(int id, [FromBody] CreatePriceBookRequest request, CancellationToken ct)
    {
        var entry = await db.PriceBooks.FindAsync([id], ct);
        if (entry is null) return NotFound();
        entry.UnitPrice = request.UnitPrice;
        entry.PurchaseUrl = request.PurchaseUrl;
        entry.CatalogUrl = request.CatalogUrl;
        entry.PriceDate = DateTime.UtcNow;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Procurement")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entry = await db.PriceBooks.FindAsync([id], ct);
        if (entry is null) return NotFound();
        db.PriceBooks.Remove(entry);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
