using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Wms;

public sealed class WmsCatalogQueryService(WarehouseDbContext db) : IWmsCatalogQueryService
{
    public async Task<decimal> GetLatestPriceAsync(string materialCode, CancellationToken ct = default) =>
        await db.PriceBooks
            .Where(p => p.MaterialCode == materialCode)
            .OrderByDescending(p => p.PriceDate)
            .Select(p => p.UnitPrice)
            .FirstOrDefaultAsync(ct);

    public async Task<(int totalStock, decimal totalValue)> GetWarehouseTotalsAsync(
        string warehouseId, CancellationToken ct = default)
    {
        var items = await db.WarehouseInventories
            .Where(i => i.WarehouseId == warehouseId)
            .Include(i => i.Material)
            .ToListAsync(ct);
        var materialCodes = items.Select(i => i.MaterialCode).ToList();
        var prices = await db.PriceBooks
            .Where(p => materialCodes.Contains(p.MaterialCode))
            .GroupBy(p => p.MaterialCode)
            .Select(g => new { MaterialCode = g.Key, Price = g.OrderByDescending(x => x.PriceDate).First().UnitPrice })
            .ToDictionaryAsync(x => x.MaterialCode, x => x.Price, ct);

        var totalStock = (int)items.Sum(i => i.Qty);
        var totalValue = items.Sum(i => i.Qty * prices.GetValueOrDefault(i.MaterialCode));
        return (totalStock, totalValue);
    }
}
