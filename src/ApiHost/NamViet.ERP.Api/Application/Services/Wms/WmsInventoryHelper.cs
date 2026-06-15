using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Wms;

public static class WmsInventoryHelper
{
    public static async Task UpdateWarehouseInventoryAsync(
        WarehouseDbContext db, string warehouseId, string materialCode,
        decimal delta, CancellationToken ct)
    {
        var inv = await db.WarehouseInventories
            .FirstOrDefaultAsync(i => i.WarehouseId == warehouseId && i.MaterialCode == materialCode, ct);
        if (inv is null)
        {
            inv = new WarehouseInventory { WarehouseId = warehouseId, MaterialCode = materialCode, Qty = 0 };
            db.WarehouseInventories.Add(inv);
        }
        inv.Qty += delta;
        inv.UpdatedAt = DateTime.UtcNow;
    }

    public static async Task ApplyApprovedImportAsync(
        WarehouseDbContext db, WarehouseTransaction transaction, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(transaction.WarehouseId)) return;
        foreach (var item in transaction.Items)
            await UpdateWarehouseInventoryAsync(
                db, transaction.WarehouseId, item.MaterialCode, item.Quantity, ct);
    }

    public static async Task ApplyApprovedExportAsync(
        WarehouseDbContext db, WarehouseTransaction transaction, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(transaction.WarehouseId)) return;
        foreach (var item in transaction.Items)
            await UpdateWarehouseInventoryAsync(
                db, transaction.WarehouseId, item.MaterialCode, -item.Quantity, ct);
    }

    public static async Task ApplyApprovedTransferAsync(
        WarehouseDbContext db, WarehouseTransaction transaction, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(transaction.FromWarehouseId) || string.IsNullOrEmpty(transaction.ToWarehouseId))
            return;
        foreach (var item in transaction.Items)
        {
            await UpdateWarehouseInventoryAsync(
                db, transaction.FromWarehouseId, item.MaterialCode, -item.Quantity, ct);
            await UpdateWarehouseInventoryAsync(
                db, transaction.ToWarehouseId, item.MaterialCode, item.Quantity, ct);
        }
    }

    public static bool IsNewlyApproved(ApprovalStatus previous, ApprovalStatus next) =>
        previous != ApprovalStatus.Approved && next == ApprovalStatus.Approved;

    public static async Task<(int totalStock, decimal totalValue)> GetWarehouseTotalsAsync(
        WarehouseDbContext db, string warehouseId, CancellationToken ct)
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
        var totalValue = items.Sum(i => i.Qty * (prices.GetValueOrDefault(i.MaterialCode)));
        return (totalStock, totalValue);
    }

    public static async Task<decimal> GetLatestPriceAsync(WarehouseDbContext db, string materialCode, CancellationToken ct)
    {
        var price = await db.PriceBooks
            .Where(p => p.MaterialCode == materialCode)
            .OrderByDescending(p => p.PriceDate)
            .Select(p => p.UnitPrice)
            .FirstOrDefaultAsync(ct);
        return price;
    }
}
