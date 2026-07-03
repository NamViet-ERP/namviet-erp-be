using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet("tree")]
    [RequirePermission("wms.categories.view:all")]
    public async Task<ActionResult<List<MaterialCategoryDto>>> Tree(CancellationToken ct)
    {
        var categories = await db.MaterialCategories
            .AsNoTracking()
            .OrderBy(c => c.Level).ThenBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new MaterialCategoryDto(
                c.Id, c.Code, c.Name, c.Level, c.ParentId, c.SortOrder, c.IsActive))
            .ToListAsync(ct);
        return Ok(categories);
    }

    [HttpGet]
    [RequirePermission("wms.categories.view:all")]
    public async Task<ActionResult<List<MaterialCategoryDto>>> List(
        [FromQuery] int? parentId, [FromQuery] int? level, CancellationToken ct)
    {
        var query = db.MaterialCategories.AsNoTracking().AsQueryable();
        if (parentId.HasValue)
            query = query.Where(c => c.ParentId == parentId);
        if (level.HasValue)
            query = query.Where(c => c.Level == level);

        var items = await query
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new MaterialCategoryDto(
                c.Id, c.Code, c.Name, c.Level, c.ParentId, c.SortOrder, c.IsActive))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [RequirePermission("wms.categories.create")]
    public async Task<ActionResult<MaterialCategoryDto>> Create(
        [FromBody] UpsertMaterialCategoryRequest request, CancellationToken ct)
    {
        if (await db.MaterialCategories.AnyAsync(c => c.Code == request.Code, ct))
            return Conflict($"Mã danh mục '{request.Code}' đã tồn tại.");

        var entity = new MaterialCategory
        {
            Code = request.Code,
            Name = request.Name,
            Level = request.Level,
            ParentId = request.ParentId,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };
        db.MaterialCategories.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(new MaterialCategoryDto(
            entity.Id, entity.Code, entity.Name, entity.Level,
            entity.ParentId, entity.SortOrder, entity.IsActive));
    }

    [HttpPut("{id:int}")]
    [RequirePermission("wms.categories.update")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertMaterialCategoryRequest request, CancellationToken ct)
    {
        var entity = await db.MaterialCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return NotFound();
        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.Level = request.Level;
        entity.ParentId = request.ParentId;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("wms.categories.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await db.MaterialCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return NotFound();
        if (entity.Code == MaterialCategoryCodes.Uncategorized)
            return BadRequest("Không thể xóa danh mục hệ thống.");

        if (await db.MaterialCategories.AnyAsync(c => c.ParentId == id, ct))
            return BadRequest("Danh mục còn mục con — hãy xóa hoặc chuyển con trước.");

        if (await db.Materials.AnyAsync(m => m.CategoryId == id && m.DeletedAt == null, ct))
            return BadRequest("Có vật tư đang dùng danh mục này.");

        db.MaterialCategories.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
