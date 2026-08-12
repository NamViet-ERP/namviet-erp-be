using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Application.Mappers.Hr;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Api.Infrastructure.RateLimiting;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers.Hr;

/// <summary>
/// FE-facing employee API (<c>/api/employees</c>) using <see cref="EmployeeDto"/> contract for the Members module.
/// </summary>
[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController(
    HrDbContext db,
    IAccountStatusProvider accountStatus,
    IValidator<EmployeeDto> onboardValidator,
    ICurrentUser currentUser,
    IEmployeeAccessService employeeAccess,
    IEmployeeOnboardingInviteService onboardingInvites) : ControllerBase
{
    [HttpGet]
    [RequirePermission("hrm.employees.view:all")]
    public async Task<IActionResult> List(
        [FromQuery] bool activeOnly = false,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken ct = default)
    {
        var query = db.Employees
            .Include(e => e.Department).Include(e => e.Position)
            .Where(e => e.DeletedAt == null);

        if (activeOnly)
            query = query.Where(e => e.Status == EmployeeStatus.Active || e.Status == EmployeeStatus.OnLeave);

        query = query.OrderByDescending(e => e.CreatedAt);

        if (page.HasValue || pageSize.HasValue)
        {
            var p = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 20, 1, 200);
            var total = await query.CountAsync(ct);
            var pageItems = await query.Skip((p - 1) * ps).Take(ps).ToListAsync(ct);
            var pageDtos = await EnrichDtosAsync(pageItems, ct);
            return Ok(PagedResult<EmployeeDto>.Create(pageDtos, total, p, ps));
        }

        var items = await query.ToListAsync(ct);
        return Ok(await EnrichDtosAsync(items, ct));
    }

    private async Task<List<EmployeeDto>> EnrichDtosAsync(List<Employee> items, CancellationToken ct)
    {
        var withUserId = items.Where(e => e.UserId.HasValue).ToList();
        Dictionary<Guid, AccountLinkStatus> statuses = [];
        if (withUserId.Count > 0)
        {
            statuses = await accountStatus.GetStatusByUserIdsAsync(
                withUserId.Select(e => e.UserId!.Value), ct);
        }

        return items.Select(e =>
        {
            var dto = HrEmployeeMapper.ToDto(e);
            if (e.UserId.HasValue && statuses.TryGetValue(e.UserId.Value, out var s))
                dto = dto with { LinkedAccountStatus = s.ToString().ToLowerInvariant() };
            return dto;
        }).ToList();
    }

    [HttpGet("me")]
    [RequireAnyPermission("hrm.employees.view:all", "hrm.employees.view:own")]
    public async Task<ActionResult<EmployeeDto>> GetMe(CancellationToken ct)
    {
        var ownId = await employeeAccess.ResolveLinkedEmployeeIdAsync(currentUser, ct);
        if (!ownId.HasValue)
            return NotFound("Tài khoản chưa liên kết hồ sơ nhân viên.");

        return await GetById(ownId.Value.ToString(), ct);
    }

    [HttpGet("{id}")]
    [RequireAnyPermission("hrm.employees.view:all", "hrm.employees.view:own")]
    public async Task<ActionResult<EmployeeDto>> GetById(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var eid)) return NotFound();
        if (!await employeeAccess.CanViewEmployeeAsync(currentUser, eid, ct))
            return NotFound();

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
    [RequirePermission("hrm.employees.create")]
    public async Task<ActionResult<object>> CheckCode(
        [FromQuery] string code, [FromQuery] string? excludeId, CancellationToken ct)
    {
        int? exclude = int.TryParse(excludeId, out var eid) ? eid : null;
        var taken = await HrEmployeeMapper.IsCodeTakenAsync(db, code, exclude, ct);
        return Ok(new { available = !taken });
    }

    [HttpGet("suggested-code")]
    [RequirePermission("hrm.employees.create")]
    public async Task<ActionResult<object>> SuggestedCode(
        [FromQuery] string? excludeId, CancellationToken ct)
    {
        int? exclude = int.TryParse(excludeId, out var eid) ? eid : null;
        var suggested = await HrEmployeeMapper.GetSuggestedCodeAsync(db, exclude, ct);
        return Ok(new { suggestedCode = suggested });
    }

    [HttpPost]
    [RequirePermission("hrm.employees.create")]
    public async Task<ActionResult<EmployeeDto>> Create([FromBody] EmployeeDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest("Vui lòng nhập mã nhân viên.");

        if (await HrEmployeeMapper.IsCodeTakenAsync(db, dto.Code, null, ct))
            return BadRequest("Mã nhân viên đã tồn tại.");

        var e = new Employee { Code = dto.Code.Trim() };
        try
        {
            await HrEmployeeMapper.ApplyFromDtoAsync(e, dto, db, ct);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        db.Employees.Add(e);
        await db.SaveChangesAsync(ct);
        await db.Entry(e).Reference(x => x.Department).LoadAsync(ct);
        await db.Entry(e).Reference(x => x.Position).LoadAsync(ct);
        return Ok(HrEmployeeMapper.ToDto(e));
    }

    [HttpPost("onboard")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicWrite)]
    public async Task<ActionResult<EmployeeDto>> Onboard(
        [FromQuery] string token,
        [FromBody] EmployeeDto dto,
        CancellationToken ct)
    {
        var invite = await onboardingInvites.FindValidAsync(token, ct);
        if (invite is null)
            return Unauthorized("Lời mời onboarding không hợp lệ, đã hết hạn hoặc đã được sử dụng.");
        if (!HrStorageKeyPolicy.AreOnboardingAttachmentsOwnedByInvite(dto, invite.Id))
            return BadRequest("Tệp onboarding không thuộc lời mời hiện tại.");

        await onboardValidator.ValidateAndThrowAsync(dto, ct);

        var onboardDto = dto with
        {
            Code = null,
            Status = "Đang làm",
            OfficialDate = null,
            ResignDate = null,
            Segment = null,
            Branch = null,
            Department = null,
            DepartmentId = null,
            Position = null,
            LinkedAccountStatus = null,
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var e = new Employee { Code = HrEmployeeMapper.NewPendingCode() };
        try
        {
            await HrEmployeeMapper.ApplyFromDtoAsync(e, onboardDto, db, ct);
        }
        catch (ArgumentException ex)
        {
            await transaction.RollbackAsync(ct);
            return BadRequest(ex.Message);
        }
        // Onboard công khai: chưa phân phòng — HR gán sau.
        e.DepartmentId = null;
        db.Employees.Add(e);
        await db.SaveChangesAsync(ct);

        var consumedAt = DateTime.UtcNow;
        var consumed = await db.EmployeeOnboardingInvites
            .Where(i => i.Id == invite.Id
                && i.ConsumedAt == null
                && i.RevokedAt == null
                && i.ExpiresAt > consumedAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(i => i.ConsumedAt, consumedAt)
                .SetProperty(i => i.ConsumedEmployeeId, e.Id)
                .SetProperty(i => i.UpdatedAt, consumedAt), ct);
        if (consumed != 1)
        {
            await transaction.RollbackAsync(ct);
            return Conflict("Lời mời onboarding vừa được sử dụng hoặc thu hồi.");
        }
        await transaction.CommitAsync(ct);

        await db.Entry(e).Reference(x => x.Department).LoadAsync(ct);
        await db.Entry(e).Reference(x => x.Position).LoadAsync(ct);
        return Ok(HrEmployeeMapper.ToDto(e));
    }

    [HttpPut("{id}")]
    [RequirePermission("hrm.employees.update")]
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

        try
        {
            await HrEmployeeMapper.ApplyFromDtoAsync(e, dto, db, ct);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        e.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await db.Entry(e).Reference(x => x.Department).LoadAsync(ct);
        await db.Entry(e).Reference(x => x.Position).LoadAsync(ct);
        return Ok(HrEmployeeMapper.ToDto(e));
    }

    [HttpDelete("{id}")]
    [RequirePermission("hrm.employees.update")]
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
