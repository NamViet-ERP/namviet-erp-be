using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Application.Helpers;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/material-brands")]
[Authorize]
public class MaterialBrandsController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    [RequireAnyPermission("wms.material_origins.view:all", "wms.materials.view:all")]
    public async Task<ActionResult<List<MaterialBrandDto>>> List(
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        var query = db.MaterialBrands.AsNoTracking().AsQueryable();
        if (activeOnly)
            query = query.Where(b => b.IsActive);

        var items = await query
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.Name)
            .Select(b => new MaterialBrandDto(b.Id, b.Name, b.Country, b.Description, b.SortOrder, b.IsActive))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [RequirePermission("wms.material_origins.create")]
    public async Task<ActionResult<MaterialBrandDto>> Create(
        [FromBody] UpsertMaterialBrandRequest request,
        CancellationToken ct)
    {
        var name = MaterialOriginHelper.NormalizeDisplayName(request.Name);
        if (string.IsNullOrEmpty(name))
            return BadRequest("Vui lòng nhập tên nhãn hiệu/hãng.");

        if (await db.MaterialBrands.AnyAsync(b => b.Name.ToLower() == name.ToLower(), ct))
            return Conflict($"Nhãn hiệu '{name}' đã tồn tại.");

        var sortOrder = request.SortOrder;
        if (sortOrder <= 0)
            sortOrder = await db.MaterialBrands.CountAsync(ct) + 1;

        var entity = new MaterialBrand
        {
            Name = name,
            Country = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            SortOrder = sortOrder,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
        };
        db.MaterialBrands.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(new MaterialBrandDto(entity.Id, entity.Name, entity.Country, entity.Description, entity.SortOrder, entity.IsActive));
    }

    [HttpPut("{id:int}")]
    [RequirePermission("wms.material_origins.update")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertMaterialBrandRequest request, CancellationToken ct)
    {
        var entity = await db.MaterialBrands.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (entity is null) return NotFound();

        var name = MaterialOriginHelper.NormalizeDisplayName(request.Name);
        if (string.IsNullOrEmpty(name))
            return BadRequest("Vui lòng nhập tên nhãn hiệu/hãng.");

        if (await db.MaterialBrands.AnyAsync(b => b.Id != id && b.Name.ToLower() == name.ToLower(), ct))
            return Conflict($"Nhãn hiệu '{name}' đã tồn tại.");

        entity.Name = name;
        entity.Country = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (request.SortOrder > 0)
            entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(new MaterialBrandDto(entity.Id, entity.Name, entity.Country, entity.Description, entity.SortOrder, entity.IsActive));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("wms.material_origins.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await db.MaterialBrands.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (entity is null) return NotFound();

        // Check if any material origin string contains this brand name
        var inUse = await db.Materials.AnyAsync(m => m.Origin != null && m.Origin.Contains(entity.Name), ct);
        if (inUse)
        {
            return Conflict(new
            {
                message = $"Nhãn hiệu '{entity.Name}' đang được sử dụng trong xuất xứ vật tư. Hãy tắt kích hoạt thay vì xóa.",
                canDeactivate = true
            });
        }

        db.MaterialBrands.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
