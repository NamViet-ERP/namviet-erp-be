using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
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
        [FromQuery] string? search = null, [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        var query = db.Materials.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.MaterialName.Contains(search) || m.MaterialCode.Contains(search));
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(m => m.MaterialCategory == category);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(m => m.MaterialCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new MaterialDto(m.MaterialCode, m.MaterialName, m.MaterialCategory, m.Unit, m.TechnicalSpec, m.QualityStandard))
            .ToListAsync(ct);

        return Ok(PagedResult<MaterialDto>.Create(items, total, page, pageSize));
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetById(string code, CancellationToken ct)
    {
        var m = await db.Materials.FirstOrDefaultAsync(x => x.MaterialCode == code, ct);
        if (m is null) return NotFound();
        return Ok(new MaterialDto(m.MaterialCode, m.MaterialName, m.MaterialCategory, m.Unit, m.TechnicalSpec, m.QualityStandard));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Procurement")]
    public async Task<IActionResult> Create([FromBody] UpsertMaterialRequest request, CancellationToken ct)
    {
        if (await db.Materials.AnyAsync(m => m.MaterialCode == request.MaterialCode, ct))
            return Conflict($"Mã vật tư '{request.MaterialCode}' đã tồn tại.");

        var material = new Material
        {
            MaterialCode = request.MaterialCode,
            MaterialName = request.MaterialName,
            MaterialCategory = request.MaterialCategory,
            Unit = request.Unit,
            TechnicalSpec = request.TechnicalSpec,
            QualityStandard = request.QualityStandard
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { code = material.MaterialCode }, null);
    }

    [HttpPut("{code}")]
    [Authorize(Roles = "Admin,Procurement")]
    public async Task<IActionResult> Update(string code, [FromBody] UpsertMaterialRequest request, CancellationToken ct)
    {
        var m = await db.Materials.FirstOrDefaultAsync(x => x.MaterialCode == code, ct);
        if (m is null) return NotFound();
        m.MaterialName = request.MaterialName;
        m.MaterialCategory = request.MaterialCategory;
        m.Unit = request.Unit;
        m.TechnicalSpec = request.TechnicalSpec;
        m.QualityStandard = request.QualityStandard;
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
        m.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
