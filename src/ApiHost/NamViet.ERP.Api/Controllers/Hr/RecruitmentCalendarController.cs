using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/recruitment/calendar")]
[Authorize]
[RequirePermission("hrm.candidates.view:all")]
public class RecruitmentCalendarController(
    IRecruitmentAnalyticsService analytics,
    IValidator<RecruitmentDateRangeQuery> rangeValidator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecruitmentCalendarEventDto>>> List(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        await rangeValidator.ValidateAndThrowAsync(new RecruitmentDateRangeQuery(from, to), ct);
        return Ok(await analytics.GetCalendarAsync(from, to, ct));
    }
}
