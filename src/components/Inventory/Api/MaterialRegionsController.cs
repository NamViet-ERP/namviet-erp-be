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
[Route("api/material-regions")]
[Authorize]
public class MaterialRegionsController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    [RequireAnyPermission("wms.material_origins.view:all", "wms.materials.view:all")]
    public async Task<ActionResult<List<MaterialRegionDto>>> List(
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        var query = db.MaterialRegions.AsNoTracking().AsQueryable();
        if (activeOnly)
            query = query.Where(r => r.IsActive);

        var items = await query
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .Select(r => new MaterialRegionDto(r.Id, r.Name, r.Description, r.SortOrder, r.IsActive))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [RequirePermission("wms.material_origins.create")]
    public async Task<ActionResult<MaterialRegionDto>> Create(
        [FromBody] UpsertMaterialRegionRequest request,
        CancellationToken ct)
    {
        var name = MaterialOriginHelper.NormalizeDisplayName(request.Name);
        if (string.IsNullOrEmpty(name))
            return BadRequest("Vui lòng nhập tên nhóm nước.");

        if (await db.MaterialRegions.AnyAsync(r => r.Name.ToLower() == name.ToLower(), ct))
            return Conflict($"Nhóm nước '{name}' đã tồn tại.");

        var sortOrder = request.SortOrder;
        if (sortOrder <= 0)
            sortOrder = await db.MaterialRegions.CountAsync(ct) + 1;

        var entity = new MaterialRegion
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            SortOrder = sortOrder,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
        };
        db.MaterialRegions.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(new MaterialRegionDto(entity.Id, entity.Name, entity.Description, entity.SortOrder, entity.IsActive));
    }

    [HttpPut("{id:int}")]
    [RequirePermission("wms.material_origins.update")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertMaterialRegionRequest request, CancellationToken ct)
    {
        var entity = await db.MaterialRegions.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return NotFound();

        var name = MaterialOriginHelper.NormalizeDisplayName(request.Name);
        if (string.IsNullOrEmpty(name))
            return BadRequest("Vui lòng nhập tên nhóm nước.");

        if (await db.MaterialRegions.AnyAsync(r => r.Id != id && r.Name.ToLower() == name.ToLower(), ct))
            return Conflict($"Nhóm nước '{name}' đã tồn tại.");

        var oldName = entity.Name;
        entity.Name = name;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (request.SortOrder > 0)
            entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        if (!string.Equals(oldName, name, StringComparison.OrdinalIgnoreCase))
        {
            var origins = await db.MaterialOrigins
                .Where(o => o.Region == oldName)
                .ToListAsync(ct);
            foreach (var origin in origins)
                origin.Region = name;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new MaterialRegionDto(entity.Id, entity.Name, entity.Description, entity.SortOrder, entity.IsActive));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("wms.material_origins.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await db.MaterialRegions.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return NotFound();

        var inUse = await db.MaterialOrigins.AnyAsync(o => o.Region == entity.Name, ct);
        if (inUse)
        {
            return Conflict(new
            {
                message = $"Nhóm nước '{entity.Name}' đang được gắn cho một số quốc gia xuất xứ. Hãy tắt kích hoạt thay vì xóa.",
                canDeactivate = true
            });
        }

        db.MaterialRegions.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
