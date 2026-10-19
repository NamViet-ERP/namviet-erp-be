using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/recruitment/plans")]
[Authorize]
public class RecruitmentPlansController(
    HrDbContext db,
    IValidator<CreateRecruitmentPlanRequest> createValidator,
    IValidator<UpdateRecruitmentPlanRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("hrm.recruitment.plans.view")]
    public async Task<ActionResult<List<RecruitmentPlanDto>>> List(
        [FromQuery] string? department = null,
        [FromQuery] int? departmentId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? period = null,
        [FromQuery] string? openMonth = null,
        CancellationToken ct = default)
    {
        var query = db.RecruitmentPlans
            .AsNoTracking()
            .Include(p => p.Position)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = NormalizePlanStatus(status);
            query = query.Where(p => p.Status == normalized);
        }

        if (!string.IsNullOrWhiteSpace(period))
        {
            var p = period.Trim();
            query = query.Where(plan => plan.Period != null && plan.Period.Contains(p));
        }

        if (!string.IsNullOrWhiteSpace(openMonth) && TryParseYearMonth(openMonth, out var year, out var month))
        {
            query = query.Where(plan =>
                plan.OpenDate != null
                && plan.OpenDate.Value.Year == year
                && plan.OpenDate.Value.Month == month);
        }

        if (departmentId.HasValue)
            query = query.Where(plan => plan.DepartmentId == departmentId.Value);

        var plans = await query
            .OrderByDescending(p => p.Id)
            .ToListAsync(ct);

        var departmentIds = plans.Select(p => p.DepartmentId).Distinct().ToList();
        var departments = await db.Departments
            .AsNoTracking()
            .Where(d => departmentIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var planIds = plans.Select(p => p.Id).ToList();
        var hiredStatuses = new[]
        {
            CandidateStatus.Accepted,
            CandidateStatus.Probation,
            CandidateStatus.Official,
        };
        var hiredByPlan = await db.Candidates
            .AsNoTracking()
            .Where(c => c.RecruitmentPlanId != null
                && planIds.Contains(c.RecruitmentPlanId.Value)
                && hiredStatuses.Contains(c.Status))
            .GroupBy(c => c.RecruitmentPlanId!.Value)
            .Select(g => new { PlanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PlanId, x => x.Count, ct);

        var departmentFilter = department?.Trim();
        var items = plans
            .Select(plan =>
            {
                departments.TryGetValue(plan.DepartmentId, out var departmentName);
                return new { plan, departmentName = departmentName ?? "" };
            })
            .Where(x =>
                departmentId.HasValue
                || string.IsNullOrWhiteSpace(departmentFilter)
                || x.departmentName.Contains(departmentFilter, StringComparison.OrdinalIgnoreCase))
            .Select(x => ToDto(
                x.plan,
                x.departmentName,
                hiredByPlan.GetValueOrDefault(x.plan.Id)))
            .ToList();

        return Ok(items);
    }

    [HttpPost]
    [RequirePermission("hrm.recruitment.plans.create")]
    public async Task<ActionResult<RecruitmentPlanDto>> Create(
        [FromBody] CreateRecruitmentPlanRequest request,
        CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        var position = await ResolvePositionAsync(request.PositionName, ct);
        if (position is null) return BadRequest($"Không tìm thấy vị trí \"{request.PositionName}\".");

        var department = await ResolveDepartmentAsync(request.DepartmentId, request.DepartmentName, ct);
        if (department is null)
            return BadRequest("Vui lòng chọn phòng ban hợp lệ (departmentId hoặc departmentName).");

        var plan = new RecruitmentPlan
        {
            PositionId = position.Id,
            DepartmentId = department.Id,
            Headcount = request.Headcount,
            Reason = request.Reason?.Trim(),
            Period = request.Period?.Trim(),
            OpenDate = ParseDateOnly(request.OpenDate),
            DeadlineDate = ParseDateOnly(request.DeadlineDate),
            Status = request.Status.Trim(),
        };
        db.RecruitmentPlans.Add(plan);
        await db.SaveChangesAsync(ct);

        await db.Entry(plan).Reference(p => p.Position).LoadAsync(ct);
        return Ok(ToDto(plan, department.Name, 0));
    }

    [HttpPut("{id}")]
    [RequirePermission("hrm.recruitment.plans.update")]
    public async Task<ActionResult<RecruitmentPlanDto>> Update(
        string id,
        [FromBody] UpdateRecruitmentPlanRequest request,
        CancellationToken ct)
    {
        if (!int.TryParse(id, out var planId)) return NotFound();
        await updateValidator.ValidateAndThrowAsync(request, ct);

        var plan = await db.RecruitmentPlans
            .Include(p => p.Position)
            .FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return NotFound();

        var position = await ResolvePositionAsync(request.PositionName, ct);
        if (position is null) return BadRequest($"Không tìm thấy vị trí \"{request.PositionName}\".");

        var department = await ResolveDepartmentAsync(request.DepartmentId, request.DepartmentName, ct);
        if (department is null)
            return BadRequest("Vui lòng chọn phòng ban hợp lệ (departmentId hoặc departmentName).");

        plan.PositionId = position.Id;
        plan.DepartmentId = department.Id;
        plan.Headcount = request.Headcount;
        plan.Reason = request.Reason?.Trim();
        plan.Period = request.Period?.Trim();
        plan.OpenDate = ParseDateOnly(request.OpenDate);
        plan.DeadlineDate = ParseDateOnly(request.DeadlineDate);
        plan.Status = request.Status.Trim();
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(plan, department.Name, 0));
    }

    [HttpDelete("{id}")]
    [RequirePermission("hrm.recruitment.plans.delete")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var planId)) return NotFound();

        var plan = await db.RecruitmentPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return NotFound();

        plan.DeletedAt = DateTime.UtcNow;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<RecruitmentPosition?> ResolvePositionAsync(string name, CancellationToken ct)
    {
        var trimmed = name.Trim();
        return await db.RecruitmentPositions
            .FirstOrDefaultAsync(p => p.Name == trimmed && p.DeletedAt == null, ct);
    }

    private async Task<Department?> ResolveDepartmentAsync(
        int? departmentId,
        string? departmentName,
        CancellationToken ct)
    {
        if (departmentId is > 0)
        {
            return await db.Departments.FirstOrDefaultAsync(
                d => d.Id == departmentId.Value && d.DeletedAt == null && d.IsActive, ct);
        }

        if (string.IsNullOrWhiteSpace(departmentName))
            return null;

        var trimmed = departmentName.Trim();
        return await db.Departments
            .FirstOrDefaultAsync(d =>
                d.DeletedAt == null
                && d.IsActive
                && (d.Name == trimmed || d.Code == trimmed), ct);
    }

    private static string NormalizePlanStatus(string? status) =>
        status?.Trim() switch
        {
            "Active" or "Closed" or "Draft" => status.Trim(),
            _ => "Active",
        };

    private static DateOnly? ParseDateOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateOnly.TryParse(value.Trim(), out var iso)) return iso;
        var m = System.Text.RegularExpressions.Regex.Match(value.Trim(), @"^(\d{2})/(\d{2})/(\d{4})$");
        if (m.Success && DateOnly.TryParse($"{m.Groups[3]}-{m.Groups[2]}-{m.Groups[1]}", out var vi))
            return vi;
        return null;
    }

    private static string? FormatDateOnly(DateOnly? value) =>
        value?.ToString("dd/MM/yyyy");

    private static bool TryParseYearMonth(string value, out int year, out int month)
    {
        year = 0;
        month = 0;
        var trimmed = value.Trim();
        var slash = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(\d{1,2})/(\d{4})$");
        if (slash.Success)
        {
            month = int.Parse(slash.Groups[1].Value);
            year = int.Parse(slash.Groups[2].Value);
            return month is >= 1 and <= 12;
        }
        var dash = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(\d{4})-(\d{2})$");
        if (dash.Success)
        {
            year = int.Parse(dash.Groups[1].Value);
            month = int.Parse(dash.Groups[2].Value);
            return month is >= 1 and <= 12;
        }
        return false;
    }

    private static RecruitmentPlanDto ToDto(RecruitmentPlan plan, string departmentName, int hired)
    {
        var remaining = Math.Max(0, plan.Headcount - hired);
        return new RecruitmentPlanDto(
            plan.Id.ToString(),
            plan.Position?.Name ?? "",
            departmentName,
            plan.Headcount,
            hired,
            remaining,
            plan.Reason,
            plan.Period,
            FormatDateOnly(plan.OpenDate),
            FormatDateOnly(plan.DeadlineDate),
            plan.Status,
            plan.DepartmentId);
    }
}
