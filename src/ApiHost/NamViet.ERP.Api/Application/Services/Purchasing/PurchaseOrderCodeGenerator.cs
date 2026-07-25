using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchaseOrderCodeGenerator
{
    Task<string> NextAsync(CancellationToken ct = default);
}

public sealed class PurchaseOrderCodeGenerator(WarehouseDbContext db) : IPurchaseOrderCodeGenerator
{
    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        var codes = await db.PurchaseOrders
            .IgnoreQueryFilters()
            .Select(p => p.Code)
            .Where(c => c.StartsWith("PO-"))
            .ToListAsync(ct);

        var max = 0;
        foreach (var code in codes)
        {
            if (code.Length > 3 && int.TryParse(code.AsSpan(3), out var n) && n > max)
                max = n;
        }

        return $"PO-{max + 1:D4}";
    }
}
