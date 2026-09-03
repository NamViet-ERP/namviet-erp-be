using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/hr/branches")]
[Authorize]
public class BranchesController(HrDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> GetAll(
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var query = db.Branches.AsQueryable();
        if (activeOnly) query = query.Where(b => b.IsActive);

        var items = await query
            .OrderBy(b => b.Code)
            .ToListAsync(ct);

        if (items.Count == 0)
            return Ok(Array.Empty<BranchDto>());

        var names = items.Select(b => b.Name).ToList();
        var employeeCounts = await db.Employees
            .AsNoTracking()
            .Where(e => e.Branch != null && names.Contains(e.Branch))
            .GroupBy(e => e.Branch!)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Name, x => x.Count, ct);

        return Ok(items.Select(b => MapDto(b, employeeCounts)).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BranchDto>> GetById(int id, CancellationToken ct)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == id && b.DeletedAt == null, ct);
        if (branch is null) return NotFound();

        var employeeCount = await db.Employees.CountAsync(e => e.Branch == branch.Name, ct);
        var counts = new Dictionary<string, int> { [branch.Name] = employeeCount };
        return Ok(MapDto(branch, counts));
    }

    [HttpPost]
    [RequirePermission("hrm.members.settings.update")]
    public async Task<IActionResult> Create([FromBody] UpsertBranchRequest request, CancellationToken ct)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            return BadRequest("Mã và tên chi nhánh là bắt buộc.");

        if (await db.Branches.AnyAsync(b => b.Code == code && b.DeletedAt == null, ct))
            return Conflict($"Mã chi nhánh '{code}' đã tồn tại.");
        if (await db.Branches.AnyAsync(b => b.Name == name && b.DeletedAt == null, ct))
            return Conflict($"Tên chi nhánh '{name}' đã tồn tại.");

        var branch = new Branch { Code = code, Name = name };
        db.Branches.Add(branch);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = branch.Id }, new { branch.Id });
    }

    [HttpPut("{id:int}")]
    [RequirePermission("hrm.members.settings.update")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertBranchRequest request, CancellationToken ct)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == id && b.DeletedAt == null, ct);
        if (branch is null) return NotFound();

        var code = request.Code.Trim();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            return BadRequest("Mã và tên chi nhánh là bắt buộc.");

        if (await db.Branches.AnyAsync(b => b.Id != id && b.Code == code && b.DeletedAt == null, ct))
            return Conflict($"Mã chi nhánh '{code}' đã tồn tại.");
        if (await db.Branches.AnyAsync(b => b.Id != id && b.Name == name && b.DeletedAt == null, ct))
            return Conflict($"Tên chi nhánh '{name}' đã tồn tại.");

        var oldName = branch.Name;
        branch.Code = code;
        branch.Name = name;
        branch.UpdatedAt = DateTime.UtcNow;

        if (!string.Equals(oldName, name, StringComparison.Ordinal))
        {
            var employees = await db.Employees.Where(e => e.Branch == oldName).ToListAsync(ct);
            foreach (var employee in employees)
            {
                employee.Branch = name;
                employee.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("hrm.members.settings.update")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == id && b.DeletedAt == null, ct);
        if (branch is null) return NotFound();

        var employeeCount = await db.Employees.CountAsync(e => e.Branch == branch.Name, ct);
        if (employeeCount > 0)
        {
            return Conflict(new
            {
                message = $"Không thể xóa chi nhánh đang có {employeeCount} nhân viên. Hãy chuyển nhân viên sang chi nhánh khác trước.",
                employeeCount,
            });
        }

        branch.DeletedAt = DateTime.UtcNow;
        branch.IsActive = false;
        branch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static BranchDto MapDto(Branch branch, IReadOnlyDictionary<string, int> employeeCounts)
    {
        employeeCounts.TryGetValue(branch.Name, out var count);
        return new BranchDto(branch.Id, branch.Code, branch.Name, branch.IsActive, count);
    }
}
