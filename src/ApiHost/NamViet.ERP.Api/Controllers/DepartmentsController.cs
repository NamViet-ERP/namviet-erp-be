using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers;

public record UpsertDepartmentRequest(string Code, string Name, int? ParentId, string? Block);
public record UpsertPositionRequest(string Name, int DepartmentId, string? Level);

[ApiController]
[Route("api/hr/departments")]
[Authorize]
public class DepartmentsController(HrDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var query = db.Departments
            .Include(d => d.Children)
            .Where(d => d.ParentId == null);

        if (activeOnly) query = query.Where(d => d.IsActive);

        var items = await query
            .OrderBy(d => d.Code)
            .ToListAsync(ct);

        return Ok(items.Select(MapDto));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var d = await db.Departments
            .Include(x => x.Children)
            .Include(x => x.Positions)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (d is null) return NotFound();
        return Ok(MapDto(d));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Create([FromBody] UpsertDepartmentRequest request, CancellationToken ct)
    {
        if (await db.Departments.AnyAsync(d => d.Code == request.Code && d.DeletedAt == null, ct))
            return Conflict($"Mã phòng ban '{request.Code}' đã tồn tại.");

        var dept = new Department
        {
            Code = request.Code,
            Name = request.Name,
            ParentId = request.ParentId,
            Block = request.Block
        };
        db.Departments.Add(dept);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = dept.Id }, new { dept.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertDepartmentRequest request, CancellationToken ct)
    {
        var dept = await db.Departments.FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null, ct);
        if (dept is null) return NotFound();
        dept.Code = request.Code;
        dept.Name = request.Name;
        dept.ParentId = request.ParentId;
        dept.Block = request.Block;
        dept.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var dept = await db.Departments.FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null, ct);
        if (dept is null) return NotFound();
        dept.DeletedAt = DateTime.UtcNow;
        dept.IsActive = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static object MapDto(Department d) => new
    {
        d.Id, d.Code, d.Name, d.ParentId, d.Block, d.IsActive,
        Children = d.Children.Where(c => c.DeletedAt == null)
                             .Select(c => new { c.Id, c.Code, c.Name, c.IsActive })
    };
}

[ApiController]
[Route("api/hr/positions")]
[Authorize]
public class PositionsController(HrDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? departmentId = null, CancellationToken ct = default)
    {
        var query = db.Positions
            .Include(p => p.Department)
            .Where(p => p.DeletedAt == null)
            .AsQueryable();

        if (departmentId.HasValue)
            query = query.Where(p => p.DepartmentId == departmentId.Value);

        var items = await query
            .OrderBy(p => p.DepartmentId).ThenBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.DepartmentId, DepartmentName = p.Department.Name, p.Level })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Create([FromBody] UpsertPositionRequest request, CancellationToken ct)
    {
        var pos = new Position
        {
            Name = request.Name,
            DepartmentId = request.DepartmentId,
            Level = request.Level
        };
        db.Positions.Add(pos);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetAll), new { departmentId = pos.DepartmentId }, new { pos.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertPositionRequest request, CancellationToken ct)
    {
        var pos = await db.Positions.FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, ct);
        if (pos is null) return NotFound();
        pos.Name = request.Name;
        pos.DepartmentId = request.DepartmentId;
        pos.Level = request.Level;
        pos.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var pos = await db.Positions.FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, ct);
        if (pos is null) return NotFound();
        pos.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
