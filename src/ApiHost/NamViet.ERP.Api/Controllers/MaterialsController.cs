using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Application.Helpers;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/materials")]
[Authorize]
public class MaterialsController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? danhMuc = null,
        [FromQuery] string? nhomVatTu = null,
        [FromQuery] string? loaiVatTu = null,
        [FromQuery] string? phanLoai = null,
        [FromQuery] string? kieu = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var allCategories = await db.MaterialCategories.AsNoTracking().ToListAsync(ct);
        var categoryById = allCategories.ToDictionary(c => c.Id);

        var query = db.Materials.AsNoTracking().Where(m => m.DeletedAt == null);
        if (!includeInactive)
            query = query.Where(m => m.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.MaterialName.Contains(search) || m.MaterialCode.Contains(search));

        var matchingIds = MaterialCategoryHelper.CollectCategoryIdsMatchingPathFilter(
            allCategories, danhMuc, nhomVatTu, loaiVatTu, phanLoai, kieu);
        var hasPathFilter = !string.IsNullOrWhiteSpace(danhMuc) || !string.IsNullOrWhiteSpace(nhomVatTu) ||
            !string.IsNullOrWhiteSpace(loaiVatTu) || !string.IsNullOrWhiteSpace(phanLoai) ||
            !string.IsNullOrWhiteSpace(kieu);
        if (hasPathFilter)
        {
            if (matchingIds.Count == 0)
                return Ok(PagedResult<MaterialDto>.Create([], 0, page, pageSize));
            query = query.Where(m => matchingIds.Contains(m.CategoryId));
        }

        if (categoryId.HasValue)
        {
            var subtree = MaterialCategoryHelper.CollectSelfAndDescendantIds(categoryId.Value, allCategories);
            query = query.Where(m => subtree.Contains(m.CategoryId));
        }

        var total = await query.CountAsync(ct);
        var materials = await query
            .OrderBy(m => m.MaterialCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var items = materials.Select(m => ToDto(m, categoryById)).ToList();
        return Ok(PagedResult<MaterialDto>.Create(items, total, page, pageSize));
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetById(string code, CancellationToken ct)
    {
        var m = await db.Materials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaterialCode == code && x.DeletedAt == null, ct);
        if (m is null) return NotFound();

        var allCategories = await db.MaterialCategories.AsNoTracking().ToListAsync(ct);
        return Ok(ToDto(m, allCategories.ToDictionary(c => c.Id)));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Procurement,Warehouse_Keeper,Storekeeper")]
    public async Task<IActionResult> Create([FromBody] UpsertMaterialRequest request, CancellationToken ct)
    {
        if (await db.Materials.IgnoreQueryFilters()
                .AnyAsync(m => m.MaterialCode == request.MaterialCode && m.DeletedAt == null, ct))
            return Conflict($"Mã vật tư '{request.MaterialCode}' đã tồn tại.");
        if (!await db.MaterialCategories.AnyAsync(c => c.Id == request.CategoryId, ct))
            return BadRequest("Danh mục vật tư không tồn tại.");

        var leaf = await db.MaterialCategories.AsNoTracking().FirstAsync(c => c.Id == request.CategoryId, ct);
        if (leaf.Level < 3)
            return BadRequest("Phải chọn tối thiểu đến cấp Loại vật tư (cấp 3).");

        var material = new Material
        {
            MaterialCode = request.MaterialCode.Trim(),
            MaterialName = request.MaterialName.Trim(),
            CategoryId = request.CategoryId,
            Unit = request.Unit.Trim(),
            TechnicalSpec = request.TechnicalSpec,
            QualityStandard = request.QualityStandard,
            IsConsumable = request.IsConsumable,
            IsActive = true
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync(ct);

        var allCategories = await db.MaterialCategories.AsNoTracking().ToListAsync(ct);
        return CreatedAtAction(nameof(GetById), new { code = material.MaterialCode },
            ToDto(material, allCategories.ToDictionary(c => c.Id)));
    }

    [HttpPut("{code}")]
    [Authorize(Roles = "Admin,Procurement")]
    public async Task<IActionResult> Update(string code, [FromBody] UpsertMaterialRequest request, CancellationToken ct)
    {
        var m = await db.Materials.FirstOrDefaultAsync(x => x.MaterialCode == code, ct);
        if (m is null) return NotFound();
        if (!await db.MaterialCategories.AnyAsync(c => c.Id == request.CategoryId, ct))
            return BadRequest("Danh mục vật tư không tồn tại.");

        var leaf = await db.MaterialCategories.AsNoTracking().FirstAsync(c => c.Id == request.CategoryId, ct);
        if (leaf.Level < 3)
            return BadRequest("Phải chọn tối thiểu đến cấp Loại vật tư (cấp 3).");

        m.MaterialName = request.MaterialName.Trim();
        m.CategoryId = request.CategoryId;
        m.Unit = request.Unit.Trim();
        m.TechnicalSpec = request.TechnicalSpec;
        m.QualityStandard = request.QualityStandard;
        m.IsConsumable = request.IsConsumable;
        m.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{code}/discontinue")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Discontinue(string code, CancellationToken ct)
    {
        var m = await db.Materials.FirstOrDefaultAsync(x => x.MaterialCode == code, ct);
        if (m is null) return NotFound();
        if (!m.IsActive) return NoContent();

        m.IsActive = false;
        m.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{code}/reactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reactivate(string code, CancellationToken ct)
    {
        var m = await db.Materials.FirstOrDefaultAsync(x => x.MaterialCode == code, ct);
        if (m is null) return NotFound();
        if (m.IsActive) return NoContent();

        m.IsActive = true;
        m.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{code}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string code, CancellationToken ct)
    {
        var m = await db.Materials.FirstOrDefaultAsync(x => x.MaterialCode == code, ct);
        if (m is null) return NotFound();

        if (await MaterialUsageHelper.IsInUseAsync(db, code, ct))
        {
            return Conflict(new
            {
                message = "Vật tư đang được dùng trong dự án, kho hoặc phiếu. Hãy dùng «Ngừng sử dụng» để giữ lịch sử.",
                canDiscontinue = true
            });
        }

        m.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static MaterialDto ToDto(Material m, IReadOnlyDictionary<int, MaterialCategory> categoryById)
    {
        categoryById.TryGetValue(m.CategoryId, out var leaf);
        var path = MaterialCategoryHelper.BuildPath(leaf, categoryById);
        return new MaterialDto(
            m.MaterialCode, m.MaterialName, m.CategoryId, leaf?.Name,
            path,
            m.Unit, m.TechnicalSpec, m.QualityStandard, m.IsConsumable, m.IsActive);
    }
}
