using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
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

    public async Task<decimal> GetLatestPriceForSupplierAsync(
        string materialCode, int supplierId, CancellationToken ct = default) =>
        await db.PriceBooks
            .Where(p => p.MaterialCode == materialCode && p.SupplierId == supplierId)
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

    public async Task<decimal> GetTotalInventoryValueAsync(CancellationToken ct = default)
    {
        var wacByMaterial = await GetWeightedAverageCostByMaterialAsync(ct);
        var qtyByMaterial = await GetCombinedStockQtyByMaterialAsync(ct);

        decimal totalValue = 0m;
        foreach (var (materialCode, qty) in qtyByMaterial)
        {
            if (qty <= 0) continue;
            var unitPrice = wacByMaterial.GetValueOrDefault(materialCode);
            if (unitPrice <= 0)
                unitPrice = await GetLatestPriceAsync(materialCode, ct);
            totalValue += qty * unitPrice;
        }

        return totalValue;
    }

    private async Task<Dictionary<string, decimal>> GetCombinedStockQtyByMaterialAsync(CancellationToken ct)
    {
        var projectRows = await db.ProjectInventories
            .AsNoTracking()
            .GroupBy(pi => pi.MaterialCode)
            .Select(g => new
            {
                MaterialCode = g.Key,
                Qty = g.Sum(x => x.ActualImportQty - x.ActualExportQty),
            })
            .ToListAsync(ct);

        var warehouseRows = await db.WarehouseInventories
            .AsNoTracking()
            .GroupBy(i => i.MaterialCode)
            .Select(g => new { MaterialCode = g.Key, Qty = g.Sum(x => x.Qty) })
            .ToListAsync(ct);

        var qtyByMaterial = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in projectRows)
            qtyByMaterial[row.MaterialCode] = qtyByMaterial.GetValueOrDefault(row.MaterialCode) + row.Qty;
        foreach (var row in warehouseRows)
            qtyByMaterial[row.MaterialCode] = qtyByMaterial.GetValueOrDefault(row.MaterialCode) + row.Qty;

        return qtyByMaterial;
    }

    private async Task<Dictionary<string, decimal>> GetWeightedAverageCostByMaterialAsync(CancellationToken ct) =>
        await db.WarehouseTransactionItems
            .AsNoTracking()
            .Where(i =>
                i.Transaction!.TransactionType == TransactionType.Import
                && i.Transaction.ApprovalStatus == ApprovalStatus.Approved
                && i.UnitPrice > 0
                && i.Quantity > 0)
            .GroupBy(i => i.MaterialCode)
            .Select(g => new
            {
                MaterialCode = g.Key,
                TotalQty = g.Sum(x => x.Quantity),
                TotalCost = g.Sum(x => x.Quantity * x.UnitPrice),
            })
            .ToDictionaryAsync(
                x => x.MaterialCode,
                x => x.TotalQty > 0 ? x.TotalCost / x.TotalQty : 0m,
                ct);
}
