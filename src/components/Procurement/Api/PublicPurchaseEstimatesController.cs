using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.RateLimiting;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[AllowAnonymous]
[Route("api/public/purchase-estimates")]
public class PublicPurchaseEstimatesController(IPurchaseEstimateService estimates) : ControllerBase
{
    [HttpGet("{token}")]
    [EnableRateLimiting(RateLimitPolicies.PublicRead)]
    public async Task<ActionResult<PublicPurchaseEstimateDto>> Get(string token, CancellationToken ct)
    {
        if (!TryParseToken(token, out var id)) return NotFound();
        try
        {
            var row = await estimates.GetPublicAsync(id, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{token}/submit")]
    [EnableRateLimiting(RateLimitPolicies.PublicWrite)]
    public async Task<ActionResult<PublicPurchaseEstimateDto>> Submit(
        string token, [FromBody] SubmitPublicPurchaseEstimateRequest request, CancellationToken ct)
    {
        if (!TryParseToken(token, out var id)) return NotFound();
        try
        {
            return Ok(await estimates.SubmitPublicAsync(id, request, ct));
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    private static bool TryParseToken(string token, out Guid id) =>
        Guid.TryParseExact(token, "N", out id) || Guid.TryParse(token, out id);
}
