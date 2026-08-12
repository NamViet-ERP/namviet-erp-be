using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/purchasing/price-book")]
[Authorize]
public class PurchasePriceBookController(IPurchasePriceBookService priceBook) : ControllerBase
{
    [HttpGet("items")]
    [RequirePermission("purchasing.pricebook.view")]
    public async Task<ActionResult<IReadOnlyList<PriceBookItemDto>>> Items(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct) =>
        Ok(await priceBook.ListItemsAsync(search, categoryId, fromDate, toDate, ct));

    [HttpGet("items/{code}")]
    [RequirePermission("purchasing.pricebook.view")]
    public async Task<ActionResult<PriceBookItemDetailDto>> Item(string code, CancellationToken ct)
    {
        var row = await priceBook.GetItemAsync(code, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("matrix")]
    [RequirePermission("purchasing.pricebook.view")]
    public async Task<ActionResult<PriceBookMatrixDto>> Matrix(
        [FromQuery] int categoryId, CancellationToken ct)
    {
        try
        {
            return Ok(await priceBook.MatrixAsync(categoryId, ct));
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpGet("categories")]
    [RequirePermission("purchasing.pricebook.view")]
    public async Task<ActionResult<IReadOnlyList<PriceBookCategoryRowDto>>> Categories(CancellationToken ct) =>
        Ok(await priceBook.CategoriesAsync(ct));

    [HttpGet("margins")]
    [RequirePermission("purchasing.pricebook.view")]
    public async Task<ActionResult<IReadOnlyList<PriceBookMarginRowDto>>> Margins(CancellationToken ct) =>
        Ok(await priceBook.MarginsAsync(ct));

    [HttpPost("sell-prices")]
    [RequirePermission("purchasing.pricebook.update")]
    public async Task<ActionResult<PurchaseSellPriceDto>> UpsertSellPrice(
        [FromBody] UpsertPurchaseSellPriceRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await priceBook.UpsertSellPriceAsync(request, ct));
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpGet("estimates")]
    [RequirePermission("purchasing.pricebook.view")]
    public async Task<ActionResult<IReadOnlyList<PriceBookEstimateRowDto>>> Estimates(CancellationToken ct) =>
        Ok(await priceBook.EstimatesAsync(ct));

    [HttpPost("estimate-prices")]
    [RequirePermission("purchasing.pricebook.update")]
    public async Task<ActionResult<PurchaseItemEstimatePriceDto>> UpsertEstimatePrice(
        [FromBody] UpsertPurchaseItemEstimatePriceRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await priceBook.UpsertEstimatePriceAsync(request, ct));
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
