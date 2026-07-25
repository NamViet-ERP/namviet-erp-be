using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/purchasing/dashboard")]
[Authorize]
[RequirePermission("purchasing.dashboard.view")]
public class PurchasingDashboardController(IPurchasingDashboardService dashboard) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<PurchasingDashboardSummaryDto>> Summary(
        [FromQuery] string? period,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        try
        {
            return Ok(await dashboard.GetSummaryAsync(period, from, to, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("chart")]
    public async Task<ActionResult<PurchasingDashboardChartDto>> Chart(
        [FromQuery] string? period,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        try
        {
            return Ok(await dashboard.GetChartAsync(period, from, to, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("activity")]
    public async Task<ActionResult<PurchasingActivityPagedResponse>> Activity(
        [FromQuery] string? period,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? staffId,
        [FromQuery] long? cursor,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await dashboard.GetActivityAsync(period, from, to, staffId, cursor, limit, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("activity/actors")]
    public async Task<ActionResult<IReadOnlyList<PurchasingActivityActorDto>>> ActivityActors(
        [FromQuery] string? period,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        try
        {
            return Ok(await dashboard.GetActivityActorsAsync(period, from, to, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
