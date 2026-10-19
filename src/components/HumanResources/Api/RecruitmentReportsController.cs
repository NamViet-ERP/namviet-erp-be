using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/recruitment/reports")]
[Authorize]
public class RecruitmentReportsController(
    IRecruitmentAnalyticsService analytics,
    IValidator<RecruitmentDateRangeQuery> rangeValidator) : ControllerBase
{
    [HttpGet("summary")]
    [RequirePermission("hrm.recruitment.reports.view")]
    public async Task<ActionResult<RecruitmentReportSummaryDto>> Summary(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken ct)
    {
        await ValidateRangeAsync(dateFrom, dateTo, ct);
        return Ok(await analytics.GetReportSummaryAsync(dateFrom, dateTo, ct));
    }

    [HttpGet("export")]
    [RequirePermission("hrm.recruitment.reports.export")]
    public async Task<IActionResult> Export(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken ct)
    {
        await ValidateRangeAsync(dateFrom, dateTo, ct);
        var summary = await analytics.GetReportSummaryAsync(dateFrom, dateTo, ct);
        var bytes = analytics.BuildReportWorkbook(summary);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "bao-cao-tuyen-dung.xlsx");
    }

    private Task ValidateRangeAsync(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        rangeValidator.ValidateAndThrowAsync(new RecruitmentDateRangeQuery(from, to), ct);
}
