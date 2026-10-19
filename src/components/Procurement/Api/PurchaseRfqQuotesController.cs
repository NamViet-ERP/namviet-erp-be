using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/purchasing/rfq-quotes")]
[Authorize]
public class PurchaseRfqQuotesController(IPurchaseRfqService rfq) : ControllerBase
{
    [HttpGet]
    [RequirePermission("purchasing.rfq.view")]
    public async Task<ActionResult<PurchaseRfqQuotePagedResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? supplierId = null,
        [FromQuery] string? status = null,
        [FromQuery] int? year = null,
        [FromQuery] int? quarter = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default) =>
        Ok(await rfq.ListAsync(page, pageSize, supplierId, status, year, quarter, search, ct));

    [HttpGet("averages")]
    [RequirePermission("purchasing.rfq.view")]
    public async Task<ActionResult<IReadOnlyList<PurchaseRfqAverageDto>>> Averages(
        [FromQuery] string groupBy = "quarter",
        [FromQuery] int? year = null,
        [FromQuery] int? quarter = null,
        [FromQuery] int? supplierId = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default) =>
        Ok(await rfq.AveragesAsync(groupBy, year, quarter, supplierId, status, ct));

    [HttpGet("win-rates")]
    [RequirePermission("purchasing.rfq.view")]
    public async Task<ActionResult<IReadOnlyList<PurchaseRfqWinRateDto>>> WinRates(
        [FromQuery] int? supplierId = null,
        [FromQuery] string? status = null,
        [FromQuery] int? year = null,
        [FromQuery] int? quarter = null,
        CancellationToken ct = default) =>
        Ok(await rfq.WinRatesAsync(supplierId, status, year, quarter, ct));

    [HttpGet("{id:guid}")]
    [RequirePermission("purchasing.rfq.view")]
    public async Task<ActionResult<PurchaseRfqQuoteDto>> GetById(Guid id, CancellationToken ct)
    {
        var row = await rfq.GetByIdAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [RequirePermission("purchasing.rfq.create")]
    public async Task<ActionResult<PurchaseRfqQuoteDto>> Create(
        [FromBody] UpsertPurchaseRfqQuoteRequest request, CancellationToken ct)
    {
        try
        {
            var created = await rfq.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("purchasing.rfq.update")]
    public async Task<ActionResult<PurchaseRfqQuoteDto>> Update(
        Guid id, [FromBody] UpsertPurchaseRfqQuoteRequest request, CancellationToken ct)
    {
        try
        {
            var row = await rfq.UpdateAsync(id, request, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
