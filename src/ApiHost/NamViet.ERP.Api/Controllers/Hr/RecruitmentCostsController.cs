using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Application.Validators;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/recruitment/costs")]
[Authorize]
public class RecruitmentCostsController(
    HrDbContext db,
    IValidator<CreateRecruitmentCostRequest> createValidator,
    IValidator<UpdateRecruitmentCostRequest> updateValidator,
    IValidator<RecruitmentCostListQuery> listValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("hrm.recruitment.costs.view")]
    public async Task<ActionResult<List<RecruitmentCostDto>>> List(
        [FromQuery] string? source = null,
        [FromQuery] string? position = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        CancellationToken ct = default)
    {
        await listValidator.ValidateAndThrowAsync(
            new RecruitmentCostListQuery(source, position, year, month), ct);
        var query = db.RecruitmentCosts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(source))
        {
            var s = source.Trim();
            query = query.Where(c => c.Source == s);
        }

        if (!string.IsNullOrWhiteSpace(position))
        {
            var p = position.Trim();
            query = query.Where(c => c.PositionName != null && c.PositionName.Contains(p));
        }

        if (year.HasValue)
            query = query.Where(c => c.PaymentYear == year);

        if (month.HasValue)
            query = query.Where(c => c.PaymentMonth == month);

        var items = await query
            .OrderByDescending(c => c.PaymentYear)
            .ThenByDescending(c => c.PaymentMonth)
            .ThenByDescending(c => c.Id)
            .Select(c => ToDto(c))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("summary")]
    [RequirePermission("hrm.recruitment.costs.view")]
    public async Task<ActionResult<List<RecruitmentCostSummaryDto>>> Summary(CancellationToken ct)
    {
        var costs = await db.RecruitmentCosts.AsNoTracking()
            .GroupBy(c => c.Source)
            .Select(g => new { Source = g.Key, Total = g.Sum(c => c.Amount) })
            .ToListAsync(ct);
        var hiredStatuses = new[]
        {
            CandidateStatus.Accepted,
            CandidateStatus.Probation,
            CandidateStatus.Official,
        };
        var hiredBySource = await db.Candidates.AsNoTracking()
            .Where(c => c.DeletedAt == null && hiredStatuses.Contains(c.Status))
            .GroupBy(c => c.Source)
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var hiredMap = hiredBySource.ToDictionary(
            x => MapSourceKey(x.Source),
            x => x.Count);

        var summaries = costs
            .Select(cost =>
            {
                var source = cost.Source.Trim();
                var hires = hiredMap.GetValueOrDefault(source, 0);
                decimal? perHire = hires > 0 ? Math.Round(cost.Total / hires, 0) : null;
                return new RecruitmentCostSummaryDto(source, cost.Total, hires, perHire);
            })
            .OrderByDescending(s => s.TotalAmount)
            .ToList();

        return Ok(summaries);
    }

    [HttpPost]
    [RequirePermission("hrm.recruitment.costs.create")]
    public async Task<ActionResult<RecruitmentCostDto>> Create(
        [FromBody] CreateRecruitmentCostRequest request,
        CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        var entity = MapToEntity(new RecruitmentCost(), request);
        db.RecruitmentCosts.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(entity));
    }

    [HttpPut("{id}")]
    [RequirePermission("hrm.recruitment.costs.update")]
    public async Task<ActionResult<RecruitmentCostDto>> Update(
        string id,
        [FromBody] UpdateRecruitmentCostRequest request,
        CancellationToken ct)
    {
        if (!int.TryParse(id, out var costId)) return NotFound();
        await updateValidator.ValidateAndThrowAsync(request, ct);

        var entity = await db.RecruitmentCosts.FirstOrDefaultAsync(c => c.Id == costId, ct);
        if (entity is null) return NotFound();

        ApplyCostUpdate(entity, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(entity));
    }

    [HttpDelete("{id}")]
    [RequirePermission("hrm.recruitment.costs.delete")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var costId)) return NotFound();

        var entity = await db.RecruitmentCosts.FirstOrDefaultAsync(c => c.Id == costId, ct);
        if (entity is null) return NotFound();

        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static RecruitmentCost MapToEntity(RecruitmentCost entity, CreateRecruitmentCostRequest request)
    {
        entity.Source = request.Source.Trim();
        entity.Amount = request.Amount;
        entity.Period = request.Period.Trim();
        entity.PositionName = request.PositionName?.Trim();
        entity.DurationDays = request.DurationDays;
        entity.PaymentYear = request.PaymentYear;
        entity.PaymentMonth = request.PaymentMonth;
        entity.Note = request.Note?.Trim();
        return entity;
    }

    private static void ApplyCostUpdate(RecruitmentCost entity, UpdateRecruitmentCostRequest request)
    {
        entity.Source = request.Source.Trim();
        entity.Amount = request.Amount;
        entity.Period = request.Period.Trim();
        entity.PositionName = request.PositionName?.Trim();
        entity.DurationDays = request.DurationDays;
        entity.PaymentYear = request.PaymentYear;
        entity.PaymentMonth = request.PaymentMonth;
        entity.Note = request.Note?.Trim();
    }

    private static RecruitmentCostDto ToDto(RecruitmentCost entity) =>
        new(
            entity.Id.ToString(),
            entity.Source,
            entity.Amount,
            entity.Period,
            entity.PositionName,
            entity.DurationDays,
            entity.PaymentYear,
            entity.PaymentMonth,
            entity.Note);

    private static string MapSourceKey(RecruitmentSource source) => source switch
    {
        RecruitmentSource.Facebook => "Facebook",
        RecruitmentSource.Zalo => "Zalo",
        RecruitmentSource.JobPortal24h => "24h - ứng tuyển",
        RecruitmentSource.TopCV => "TopCV",
        RecruitmentSource.Glints => "Glints",
        RecruitmentSource.Internal => "Nội bộ",
        RecruitmentSource.Referral => "Giới thiệu",
        _ => "Khác",
    };
}
