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
[Route("api/material-origins")]
[Authorize]
public class MaterialOriginsController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    [RequireAnyPermission("wms.material_origins.view:all", "wms.materials.view:all")]
    public async Task<ActionResult<List<MaterialOriginDto>>> List(
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        var query = db.MaterialOrigins.AsNoTracking().AsQueryable();
        if (activeOnly)
            query = query.Where(o => o.IsActive);

        var items = await query
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Name)
            .Select(o => new MaterialOriginDto(o.Id, o.Name, o.Region, o.SortOrder, o.IsActive))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [RequirePermission("wms.material_origins.create")]
    public async Task<ActionResult<MaterialOriginDto>> Create(
        [FromBody] UpsertMaterialOriginRequest request,
        CancellationToken ct)
    {
        var name = MaterialOriginHelper.NormalizeDisplayName(request.Name);
        if (string.IsNullOrEmpty(name))
            return BadRequest("Vui lòng nhập tên xuất xứ.");

        if (await db.MaterialOrigins.AnyAsync(o => o.Name.ToLower() == name.ToLower(), ct))
            return Conflict($"Xuất xứ '{name}' đã tồn tại.");

        var sortOrder = request.SortOrder;
        if (sortOrder <= 0)
            sortOrder = await db.MaterialOrigins.CountAsync(ct) + 1;

        var entity = new MaterialOrigin
        {
            Name = name,
            Region = string.IsNullOrWhiteSpace(request.Region) ? null : request.Region.Trim(),
            SortOrder = sortOrder,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
        };
        db.MaterialOrigins.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(new MaterialOriginDto(entity.Id, entity.Name, entity.Region, entity.SortOrder, entity.IsActive));
    }

    [HttpPut("{id:int}")]
    [RequirePermission("wms.material_origins.update")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertMaterialOriginRequest request, CancellationToken ct)
    {
        var entity = await db.MaterialOrigins.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (entity is null) return NotFound();

        var name = MaterialOriginHelper.NormalizeDisplayName(request.Name);
        if (string.IsNullOrEmpty(name))
            return BadRequest("Vui lòng nhập tên xuất xứ.");

        if (await db.MaterialOrigins.AnyAsync(o => o.Id != id && o.Name.ToLower() == name.ToLower(), ct))
            return Conflict($"Xuất xứ '{name}' đã tồn tại.");

        var oldName = entity.Name;
        entity.Name = name;
        entity.Region = string.IsNullOrWhiteSpace(request.Region) ? null : request.Region.Trim();
        if (request.SortOrder > 0)
            entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        if (!string.Equals(oldName, name, StringComparison.OrdinalIgnoreCase))
        {
            var materials = await db.Materials
                .Where(m => m.Origin == oldName)
                .ToListAsync(ct);
            foreach (var material in materials)
                material.Origin = name;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new MaterialOriginDto(entity.Id, entity.Name, entity.Region, entity.SortOrder, entity.IsActive));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("wms.material_origins.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await db.MaterialOrigins.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (entity is null) return NotFound();

        var inUse = await db.Materials.AnyAsync(m => m.Origin == entity.Name, ct);
        if (inUse)
        {
            return Conflict(new
            {
                message = $"Xuất xứ '{entity.Name}' đang được sử dụng bởi vật tư. Hãy tắt kích hoạt thay vì xóa.",
                canDeactivate = true
            });
        }

        db.MaterialOrigins.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
