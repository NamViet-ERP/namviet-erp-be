using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/recruitment/dashboard")]
[Authorize]
[RequirePermission("hrm.candidates.view:all")]
public class RecruitmentDashboardController(
    IRecruitmentAnalyticsService analytics,
    IValidator<RecruitmentTrendQuery> trendValidator) : ControllerBase
{
    [HttpGet("kpi")]
    public async Task<ActionResult<RecruitmentKpiDto>> Kpi(CancellationToken ct)
        => Ok(await analytics.GetKpiAsync(ct));

    [HttpGet("trend")]
    public async Task<ActionResult<IReadOnlyList<RecruitmentTrendDto>>> Trend(
        [FromQuery] int months = 6, CancellationToken ct = default)
    {
        await trendValidator.ValidateAndThrowAsync(new RecruitmentTrendQuery(months), ct);
        return Ok(await analytics.GetTrendAsync(months, ct));
    }

    [HttpGet("by-source")]
    public async Task<ActionResult<IReadOnlyList<RecruitmentSourceStatDto>>> BySource(CancellationToken ct)
        => Ok(await analytics.GetBySourceAsync(ct));

    [HttpGet("by-position")]
    public async Task<ActionResult<IReadOnlyList<RecruitmentPositionStatDto>>> ByPosition(CancellationToken ct)
        => Ok(await analytics.GetByPositionAsync(ct));

    [HttpGet("cost-by-source")]
    public async Task<ActionResult<IReadOnlyList<RecruitmentCostSummaryDto>>> CostBySource(CancellationToken ct)
        => Ok(await analytics.GetCostBySourceAsync(ct));
}
