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
[Route("api/material-units")]
[Authorize]
public class MaterialUnitsController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.material_units.view:all")]
    public async Task<ActionResult<List<MaterialUnitDto>>> List(
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        var query = db.MaterialUnits.AsNoTracking().AsQueryable();
        if (activeOnly)
            query = query.Where(u => u.IsActive);

        var items = await query
            .OrderBy(u => u.Name)
            .Select(u => new MaterialUnitDto(u.Id, u.Name, u.SortOrder, u.IsActive))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [RequirePermission("wms.material_units.create")]
    public async Task<ActionResult<MaterialUnitDto>> Create(
        [FromBody] UpsertMaterialUnitRequest request,
        CancellationToken ct)
    {
        var name = MaterialUnitHelper.NormalizeDisplayName(request.Name);
        if (string.IsNullOrEmpty(name))
            return BadRequest("Vui lòng nhập tên đơn vị tính.");

        if (await db.MaterialUnits.AnyAsync(u => u.Name == name, ct))
            return Conflict($"Đơn vị tính '{name}' đã tồn tại.");

        var sortOrder = request.SortOrder;
        if (sortOrder <= 0)
            sortOrder = await db.MaterialUnits.CountAsync(ct) + 1;

        var entity = new MaterialUnit
        {
            Name = name,
            SortOrder = sortOrder,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
        };
        db.MaterialUnits.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(new MaterialUnitDto(entity.Id, entity.Name, entity.SortOrder, entity.IsActive));
    }

    [HttpPut("{id:int}")]
    [RequirePermission("wms.material_units.update")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertMaterialUnitRequest request, CancellationToken ct)
    {
        var entity = await db.MaterialUnits.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (entity is null) return NotFound();

        var name = MaterialUnitHelper.NormalizeDisplayName(request.Name);
        if (string.IsNullOrEmpty(name))
            return BadRequest("Vui lòng nhập tên đơn vị tính.");

        if (await db.MaterialUnits.AnyAsync(u => u.Id != id && u.Name == name, ct))
            return Conflict($"Đơn vị tính '{name}' đã tồn tại.");

        var oldName = entity.Name;
        entity.Name = name;
        if (request.SortOrder > 0)
            entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        if (!string.Equals(oldName, name, StringComparison.Ordinal))
        {
            var materials = await db.Materials
                .Where(m => m.Unit == oldName)
                .ToListAsync(ct);
            foreach (var material in materials)
                material.Unit = name;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new MaterialUnitDto(entity.Id, entity.Name, entity.SortOrder, entity.IsActive));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("wms.material_units.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await db.MaterialUnits.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (entity is null) return NotFound();

        if (await db.Materials.AnyAsync(m => m.Unit == entity.Name && m.DeletedAt == null, ct))
            return BadRequest("Có vật tư đang dùng đơn vị tính này.");

        db.MaterialUnits.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
