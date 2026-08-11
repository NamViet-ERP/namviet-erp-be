using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

internal static class MaterialCeilingSync
{
    public static async Task ApplyFromPlanAsync(WarehouseDbContext db, string planCode, CancellationToken ct)
    {
        var priced = await db.PurchasePlanLines.AsNoTracking()
            .Where(l => l.PlanCode == planCode && l.MaterialCode != null && l.BudgetUnitPrice > 0)
            .Select(l => new { l.MaterialCode, Price = l.BudgetUnitPrice!.Value })
            .ToListAsync(ct);
        if (priced.Count == 0) return;

        var byCode = priced
            .GroupBy(x => x.MaterialCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Price), StringComparer.OrdinalIgnoreCase);
        var codes = byCode.Keys.ToList();
        var materials = await db.Materials
            .Where(m => codes.Contains(m.MaterialCode))
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var material in materials)
        {
            if (!byCode.TryGetValue(material.MaterialCode, out var price)) continue;
            if (material.CeilingPrice is > 0 && material.CeilingPrice >= price) continue;
            material.CeilingPrice = price;
            material.UpdatedAt = now;
        }
    }
}
