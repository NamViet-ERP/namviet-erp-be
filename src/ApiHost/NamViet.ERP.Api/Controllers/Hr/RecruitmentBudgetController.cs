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
[Route("api/recruitment/budget")]
[Authorize]
public class RecruitmentBudgetController(
    HrDbContext db,
    IValidator<UpdateRecruitmentBudgetRequest> updateValidator,
    IValidator<RecruitmentYearQuery> yearValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("hrm.recruitment.costs.view")]
    public async Task<ActionResult<RecruitmentBudgetDto>> Get(
        [FromQuery] int? year = null,
        CancellationToken ct = default)
    {
        await yearValidator.ValidateAndThrowAsync(new RecruitmentYearQuery(year), ct);
        var targetYear = year ?? DateTime.UtcNow.Year;
        var budget = await db.RecruitmentBudgets
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Year == targetYear, ct);

        var used = await db.RecruitmentCosts
            .AsNoTracking()
            .Where(c => c.DeletedAt == null && c.PaymentYear == targetYear)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

        var allocated = budget?.TotalAllocated ?? 0m;
        return Ok(new RecruitmentBudgetDto(targetYear, allocated, used, Math.Max(0, allocated - used)));
    }

    [HttpPut]
    [RequirePermission("hrm.recruitment.costs.update")]
    public async Task<ActionResult<RecruitmentBudgetDto>> Upsert(
        [FromBody] UpdateRecruitmentBudgetRequest request,
        CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        var year = request.Year;
        var allocated = request.TotalAllocated;

        var entity = await db.RecruitmentBudgets
            .FirstOrDefaultAsync(b => b.Year == year, ct);

        if (entity is null)
        {
            entity = new RecruitmentBudget { Year = year, TotalAllocated = allocated };
            db.RecruitmentBudgets.Add(entity);
        }
        else
        {
            entity.TotalAllocated = allocated;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        var used = await db.RecruitmentCosts
            .AsNoTracking()
            .Where(c => c.DeletedAt == null && c.PaymentYear == year)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

        return Ok(new RecruitmentBudgetDto(year, allocated, used, Math.Max(0, allocated - used)));
    }
}
