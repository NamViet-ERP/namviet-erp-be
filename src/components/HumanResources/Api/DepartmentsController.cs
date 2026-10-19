using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers;

public record UpsertDepartmentRequest(string Code, string Name, int? ParentId, string? Block);
public record UpsertPositionRequest(string Name, int DepartmentId, string? Level);
public record AssignDepartmentMembersRequest(IReadOnlyList<int> EmployeeIds);

[ApiController]
[Route("api/hr/departments")]
[Authorize]
public class DepartmentsController(HrDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission("hrm.members.settings.view")]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetAll([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var query = db.Departments
            .Include(d => d.Children)
            .Where(d => d.ParentId == null);

        if (activeOnly) query = query.Where(d => d.IsActive);

        var items = await query
            .OrderBy(d => d.Code)
            .ToListAsync(ct);

        var ids = items.Select(d => d.Id)
            .Concat(items.SelectMany(d => d.Children.Where(c => c.DeletedAt == null).Select(c => c.Id)))
            .Distinct()
            .ToList();

        var employeeCounts = await db.Employees
            .AsNoTracking()
            .Where(e => e.DepartmentId != null && ids.Contains(e.DepartmentId.Value))
            .GroupBy(e => e.DepartmentId!.Value)
            .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DepartmentId, x => x.Count, ct);

        return Ok(items.Select(d => MapDto(d, employeeCounts)));
    }

    [HttpGet("{id:int}")]
    [RequirePermission("hrm.members.settings.view")]
    public async Task<ActionResult<DepartmentDto>> GetById(int id, CancellationToken ct)
    {
        var d = await db.Departments
            .Include(x => x.Children)
            .Include(x => x.Positions)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (d is null) return NotFound();

        var employeeCount = await db.Employees.CountAsync(e => e.DepartmentId == id, ct);
        var counts = new Dictionary<int, int> { [id] = employeeCount };
        return Ok(MapDto(d, counts));
    }

    [HttpGet("{id:int}/employees")]
    [RequirePermission("hrm.members.settings.view")]
    public async Task<ActionResult<IReadOnlyList<DepartmentMemberDto>>> GetEmployees(int id, CancellationToken ct)
    {
        var exists = await db.Departments.AnyAsync(d => d.Id == id && d.DeletedAt == null, ct);
        if (!exists) return NotFound();

        var employees = await db.Employees
            .AsNoTracking()
            .Include(e => e.Position)
            .Where(e => e.DepartmentId == id)
            .OrderBy(e => e.FullName)
            .Select(e => new DepartmentMemberDto(
                e.Id,
                e.Code,
                e.FullName,
                e.Email,
                e.Phone,
                e.Status.ToString(),
                e.Position != null ? e.Position.Name : null))
            .ToListAsync(ct);

        return Ok(employees);
    }

    [HttpPost]
    [RequirePermission("hrm.members.settings.update")]
    public async Task<IActionResult> Create([FromBody] UpsertDepartmentRequest request, CancellationToken ct)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            return BadRequest("Mã và tên phòng ban là bắt buộc.");

        if (await db.Departments.AnyAsync(d => d.Code == code && d.DeletedAt == null, ct))
            return Conflict($"Mã phòng ban '{code}' đã tồn tại.");

        var dept = new Department
        {
            Code = code,
            Name = name,
            ParentId = request.ParentId,
            Block = string.IsNullOrWhiteSpace(request.Block) ? null : request.Block.Trim(),
        };
        db.Departments.Add(dept);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = dept.Id }, new { dept.Id });
    }

    [HttpPut("{id:int}")]
    [RequirePermission("hrm.members.settings.update")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertDepartmentRequest request, CancellationToken ct)
    {
        var dept = await db.Departments.FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null, ct);
        if (dept is null) return NotFound();

        var code = request.Code.Trim();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            return BadRequest("Mã và tên phòng ban là bắt buộc.");

        if (await db.Departments.AnyAsync(d => d.Id != id && d.Code == code && d.DeletedAt == null, ct))
            return Conflict($"Mã phòng ban '{code}' đã tồn tại.");

        dept.Code = code;
        dept.Name = name;
        dept.ParentId = request.ParentId;
        dept.Block = string.IsNullOrWhiteSpace(request.Block) ? null : request.Block.Trim();
        dept.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Gán một hoặc nhiều nhân viên vào phòng ban (ghi đè phòng ban hiện tại của NV).</summary>
    [HttpPost("{id:int}/employees")]
    [RequirePermission("hrm.members.settings.update")]
    public async Task<IActionResult> AssignEmployees(
        int id,
        [FromBody] AssignDepartmentMembersRequest request,
        CancellationToken ct)
    {
        var dept = await db.Departments.FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null, ct);
        if (dept is null) return NotFound();
        if (!dept.IsActive)
            return BadRequest("Không thể gán nhân viên vào phòng ban đã ngừng hoạt động.");

        var ids = (request.EmployeeIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return BadRequest("Chọn ít nhất một nhân viên.");

        var employees = await db.Employees
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(ct);
        if (employees.Count != ids.Count)
            return BadRequest("Một số nhân viên không tồn tại hoặc đã bị xóa.");

        foreach (var employee in employees)
        {
            employee.DepartmentId = id;
            employee.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { assigned = employees.Count, departmentId = id });
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("hrm.members.settings.update")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var dept = await db.Departments.FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null, ct);
        if (dept is null) return NotFound();

        var employeeCount = await db.Employees.CountAsync(e => e.DepartmentId == id, ct);
        if (employeeCount > 0)
        {
            return Conflict(new
            {
                message = $"Không thể xóa phòng ban đang có {employeeCount} nhân viên. Hãy chuyển nhân viên sang phòng khác trước.",
                employeeCount,
            });
        }

        var childCount = await db.Departments.CountAsync(d => d.ParentId == id && d.DeletedAt == null, ct);
        if (childCount > 0)
        {
            return Conflict(new
            {
                message = $"Không thể xóa phòng ban đang có {childCount} phòng ban con.",
                childCount,
            });
        }

        var planCount = await db.RecruitmentPlans.CountAsync(p => p.DepartmentId == id, ct);
        if (planCount > 0)
        {
            return Conflict(new
            {
                message = $"Không thể xóa phòng ban đang được dùng bởi {planCount} kế hoạch tuyển dụng.",
                planCount,
            });
        }

        dept.DeletedAt = DateTime.UtcNow;
        dept.IsActive = false;
        dept.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static DepartmentDto MapDto(Department d, IReadOnlyDictionary<int, int> employeeCounts)
    {
        employeeCounts.TryGetValue(d.Id, out var count);
        var children = d.Children
            .Where(c => c.DeletedAt == null)
            .Select(c =>
            {
                employeeCounts.TryGetValue(c.Id, out var childCount);
                return new DepartmentChildDto(c.Id, c.Code, c.Name, c.IsActive, childCount);
            })
            .ToList();

        return new DepartmentDto(
            d.Id,
            d.Code,
            d.Name,
            d.ParentId,
            d.Block,
            d.IsActive,
            count,
            children);
    }
}

[ApiController]
[Route("api/hr/positions")]
[Authorize]
public class PositionsController(HrDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission("hrm.members.settings.view")]
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
    [RequirePermission("hrm.members.settings.update")]
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
    [RequirePermission("hrm.members.settings.update")]
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
    [RequirePermission("hrm.members.settings.update")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var pos = await db.Positions.FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, ct);
        if (pos is null) return NotFound();
        pos.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
