using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchasePlanCodeGenerator
{
    Task<string> NextAsync(CancellationToken ct = default);
}

public sealed class PurchasePlanCodeGenerator(WarehouseDbContext db) : IPurchasePlanCodeGenerator
{
    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        var codes = await db.PurchasePlans
            .IgnoreQueryFilters()
            .Select(p => p.Code)
            .Where(c => c.StartsWith("KHMH-"))
            .ToListAsync(ct);

        var max = 0;
        foreach (var code in codes)
        {
            if (code.Length > 5 && int.TryParse(code.AsSpan(5), out var n) && n > max)
                max = n;
        }

        return $"KHMH-{max + 1:D4}";
    }
}
