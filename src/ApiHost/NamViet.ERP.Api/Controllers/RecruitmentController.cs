using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers;

public record UpsertRecruitmentPositionRequest(
    string Name, int? DepartmentId, string? JobDescription, bool IsActive = true);

public record CreateRecruitmentPlanRequest(
    int PositionId, int DepartmentId, int Headcount,
    string? Reason, string? Period);

[ApiController]
[Route("api/hr/recruitment")]
[Authorize]
public class RecruitmentController(HrDbContext db) : ControllerBase
{
    // ── Vị trí tuyển dụng ──────────────────────────────────────────────────────
    [HttpGet("positions")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPositions(
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var query = db.RecruitmentPositions
            .Include(p => p.Department)
            .Where(p => p.DeletedAt == null)
            .AsQueryable();

        if (activeOnly) query = query.Where(p => p.IsActive);

        var items = await query
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, p.DepartmentId,
                DepartmentName = p.Department != null ? p.Department.Name : null,
                p.JobDescription, p.IsActive
            })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("positions")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> CreatePosition([FromBody] UpsertRecruitmentPositionRequest request, CancellationToken ct)
    {
        var pos = new RecruitmentPosition
        {
            Name = request.Name,
            DepartmentId = request.DepartmentId,
            JobDescription = request.JobDescription,
            IsActive = request.IsActive
        };
        db.RecruitmentPositions.Add(pos);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetPositions), new { pos.Id });
    }

    [HttpPut("positions/{id:int}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> UpdatePosition(int id, [FromBody] UpsertRecruitmentPositionRequest request, CancellationToken ct)
    {
        var pos = await db.RecruitmentPositions
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, ct);
        if (pos is null) return NotFound();
        pos.Name = request.Name;
        pos.DepartmentId = request.DepartmentId;
        pos.JobDescription = request.JobDescription;
        pos.IsActive = request.IsActive;
        pos.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Kế hoạch tuyển dụng ───────────────────────────────────────────────────
    [HttpGet("plans")]
    [Authorize(Roles = "Admin,HR,BOD")]
    public async Task<IActionResult> GetPlans(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, [FromQuery] string? period = null,
        CancellationToken ct = default)
    {
        var query = db.RecruitmentPlans
            .Include(p => p.Position)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(period)) query = query.Where(p => p.Period == period);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                p.Id, p.PositionId, PositionName = p.Position.Name,
                p.DepartmentId, p.Headcount, p.Reason,
                p.Period, p.Status, p.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(PagedResult<object>.Create(items, total, page, pageSize));
    }

    [HttpPost("plans")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> CreatePlan([FromBody] CreateRecruitmentPlanRequest request, CancellationToken ct)
    {
        if (!await db.RecruitmentPositions.AnyAsync(p => p.Id == request.PositionId, ct))
            return BadRequest($"Vị trí tuyển dụng ID={request.PositionId} không tồn tại.");

        var plan = new RecruitmentPlan
        {
            PositionId = request.PositionId,
            DepartmentId = request.DepartmentId,
            Headcount = request.Headcount,
            Reason = request.Reason,
            Period = request.Period
        };
        db.RecruitmentPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetPlans), new { plan.Id });
    }

    [HttpPatch("plans/{id:int}/status")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> UpdatePlanStatus(int id, [FromBody] string status, CancellationToken ct)
    {
        var plan = await db.RecruitmentPlans.FindAsync([id], ct);
        if (plan is null) return NotFound();
        plan.Status = status;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Khen thưởng / kỷ luật ─────────────────────────────────────────────────
    [HttpGet("rewards")]
    public async Task<IActionResult> GetRewards(
        [FromQuery] int? employeeId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = db.Rewards.Include(r => r.Employee).AsQueryable();
        if (employeeId.HasValue) query = query.Where(r => r.EmployeeId == employeeId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.Date)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id, r.EmployeeId, EmployeeName = r.Employee.FullName,
                r.Type, r.Amount, r.Reason, r.Date, r.DecisionNo
            })
            .ToListAsync(ct);
        return Ok(PagedResult<object>.Create(items, total, page, pageSize));
    }

    [HttpPost("rewards")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> CreateReward(
        [FromBody] CreateRewardRequest request, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == request.EmployeeId, ct))
            return BadRequest($"Nhân viên ID={request.EmployeeId} không tồn tại.");
        if (!DateOnly.TryParse(request.Date, out var d))
            return BadRequest("Ngày không hợp lệ.");
        var reward = new Reward
        {
            EmployeeId = request.EmployeeId,
            Type = request.Type,
            Amount = request.Amount,
            Reason = request.Reason,
            Date = d,
            DecisionNo = request.DecisionNo
        };
        db.Rewards.Add(reward);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetRewards), new { reward.Id });
    }
}

public record CreateRewardRequest(
    int EmployeeId, string Type, decimal Amount,
    string? Reason, string Date, string? DecisionNo);
