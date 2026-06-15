using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public class SuppliersFeController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SupplierFeDto>>> List([FromQuery] string? category, CancellationToken ct)
    {
        var suppliers = await db.Suppliers.Where(s => s.DeletedAt == null).ToListAsync(ct);
        var supplierIds = suppliers.Select(s => s.Id).ToList();
        var avgPrices = await db.PriceBooks
            .Where(p => supplierIds.Contains(p.SupplierId))
            .GroupBy(p => p.SupplierId)
            .Select(g => new { SupplierId = g.Key, Avg = g.Average(x => x.UnitPrice) })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Avg, ct);

        var result = suppliers.Select(s =>
        {
            var avgPrice = avgPrices.GetValueOrDefault(s.Id);
            return new SupplierFeDto(s.SupplierName, avgPrice, s.MaxDebtDays, 7,
                s.Rating, s.Rating == "A" ? "Tốt nhất" : "Khuyến nghị");
        }).ToList();
        return Ok(result);
    }

    [HttpGet("price-history")]
    public async Task<ActionResult<List<PriceHistoryEntryDto>>> PriceHistory(
        [FromQuery] string category = "steel", [FromQuery] int months = 6, CancellationToken ct = default)
    {
        // TODO: filter PriceBooks by month — stub tiers for dev UI
        var entries = new List<PriceHistoryEntryDto>();
        for (var i = months; i >= 1; i--)
        {
            var month = DateTime.UtcNow.AddMonths(-i).ToString("MM/yyyy");
            var avg = await db.PriceBooks.Select(p => p.UnitPrice).DefaultIfEmpty(0).AverageAsync(ct);
            entries.Add(new PriceHistoryEntryDto(month, avg, avg * 0.8m, avg * 0.6m));
        }
        return Ok(entries);
    }

    [HttpGet("ranked")]
    public Task<ActionResult<List<SupplierFeDto>>> Ranked(CancellationToken ct) => List(null, ct);
}
