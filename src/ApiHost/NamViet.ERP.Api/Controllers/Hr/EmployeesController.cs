using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Application.Mappers.Hr;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController(HrDbContext db, IAccountStatusProvider accountStatus) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EmployeeDto>>> List(
        [FromQuery] bool activeOnly = false, CancellationToken ct = default)
    {
        var query = db.Employees
            .Include(e => e.Department).Include(e => e.Position)
            .Where(e => e.DeletedAt == null);

        if (activeOnly)
            query = query.Where(e => e.Status == EmployeeStatus.Active || e.Status == EmployeeStatus.OnLeave);

        var items = await query.OrderByDescending(e => e.CreatedAt).ToListAsync(ct);

        // Batch-enrich linked account status for employees with UserId
        var withUserId = items.Where(e => e.UserId.HasValue).ToList();
        Dictionary<Guid, NamViet.ERP.SharedKernel.Interfaces.AccountLinkStatus> statuses = [];
        if (withUserId.Count > 0)
        {
            statuses = await accountStatus.GetStatusByUserIdsAsync(
                withUserId.Select(e => e.UserId!.Value), ct);
        }

        var dtos = items.Select(e =>
        {
            var dto = HrEmployeeMapper.ToDto(e);
            if (e.UserId.HasValue && statuses.TryGetValue(e.UserId.Value, out var s))
                dto = dto with { LinkedAccountStatus = s.ToString().ToLowerInvariant() };
            return dto;
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<EmployeeDto>> GetById(string id, CancellationToken ct)
    {
        var e = await FindAsync(id, ct);
        if (e is null) return NotFound();
        var dto = HrEmployeeMapper.ToDto(e);

        // Enrich with linked account status from Identity module
        if (e.UserId.HasValue)
        {
            var status = await accountStatus.GetStatusByUserIdAsync(e.UserId.Value, ct);
            dto = dto with { LinkedAccountStatus = status.ToString().ToLowerInvariant() };
        }

        return Ok(dto);
    }

    [HttpGet("check-code")]
    public async Task<ActionResult<object>> CheckCode(
        [FromQuery] string code, [FromQuery] string? excludeId, CancellationToken ct)
    {
        int? exclude = int.TryParse(excludeId, out var eid) ? eid : null;
        var taken = await HrEmployeeMapper.IsCodeTakenAsync(db, code, exclude, ct);
        return Ok(new { available = !taken });
    }

    [HttpGet("suggested-code")]
    public async Task<ActionResult<object>> SuggestedCode(
        [FromQuery] string? excludeId, CancellationToken ct)
    {
        int? exclude = int.TryParse(excludeId, out var eid) ? eid : null;
        var suggested = await HrEmployeeMapper.GetSuggestedCodeAsync(db, exclude, ct);
        return Ok(new { suggestedCode = suggested });
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create([FromBody] EmployeeDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest("Vui lòng nhập mã nhân viên.");

        if (await HrEmployeeMapper.IsCodeTakenAsync(db, dto.Code, null, ct))
            return BadRequest("Mã nhân viên đã tồn tại.");

        var e = new Employee { Code = dto.Code.Trim() };
        await HrEmployeeMapper.ApplyFromDtoAsync(e, dto, db, ct);
        await HrEmployeeMapper.EnsureDefaultDepartmentAsync(e, db, ct);
        db.Employees.Add(e);
        await db.SaveChangesAsync(ct);
        await db.Entry(e).Reference(x => x.Department).LoadAsync(ct);
        await db.Entry(e).Reference(x => x.Position).LoadAsync(ct);
        return Ok(HrEmployeeMapper.ToDto(e));
    }

    [HttpPost("onboard")]
    [AllowAnonymous]
    public async Task<ActionResult<EmployeeDto>> Onboard([FromBody] EmployeeDto dto, CancellationToken ct)
    {
        var onboardDto = dto with
        {
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "Đang làm" : dto.Status,
            Code = null
        };

        var e = new Employee { Code = HrEmployeeMapper.NewPendingCode() };
        await HrEmployeeMapper.ApplyFromDtoAsync(e, onboardDto, db, ct);
        await HrEmployeeMapper.EnsureDefaultDepartmentAsync(e, db, ct);
        db.Employees.Add(e);
        await db.SaveChangesAsync(ct);
        await db.Entry(e).Reference(x => x.Department).LoadAsync(ct);
        await db.Entry(e).Reference(x => x.Position).LoadAsync(ct);
        return Ok(HrEmployeeMapper.ToDto(e));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<EmployeeDto>> Update(string id, [FromBody] EmployeeDto dto, CancellationToken ct)
    {
        if (!int.TryParse(id, out var eid)) return NotFound();
        var e = await db.Employees
            .Include(x => x.Department).Include(x => x.Position)
            .FirstOrDefaultAsync(x => x.Id == eid && x.DeletedAt == null, ct);
        if (e is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Code) &&
            await HrEmployeeMapper.IsCodeTakenAsync(db, dto.Code, eid, ct))
            return BadRequest("Mã nhân viên đã tồn tại.");

        if (!string.IsNullOrWhiteSpace(dto.Code))
            e.Code = dto.Code.Trim();

        await HrEmployeeMapper.ApplyFromDtoAsync(e, dto, db, ct);
        e.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await db.Entry(e).Reference(x => x.Department).LoadAsync(ct);
        await db.Entry(e).Reference(x => x.Position).LoadAsync(ct);
        return Ok(HrEmployeeMapper.ToDto(e));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var eid)) return NotFound();
        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == eid, ct);
        if (e is null) return NotFound();
        e.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<Employee?> FindAsync(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var eid)) return null;
        return await db.Employees.Include(x => x.Department).Include(x => x.Position)
            .FirstOrDefaultAsync(x => x.Id == eid && x.DeletedAt == null, ct);
    }
}
