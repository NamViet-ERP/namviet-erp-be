using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchasePaymentCodeGenerator
{
    Task<string> NextAsync(CancellationToken ct = default);
}

public sealed class PurchasePaymentCodeGenerator(WarehouseDbContext db) : IPurchasePaymentCodeGenerator
{
    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        var codes = await db.PurchasePayments
            .IgnoreQueryFilters()
            .Select(p => p.Code)
            .Where(c => c.StartsWith("DNTT-"))
            .ToListAsync(ct);

        var max = 0;
        foreach (var code in codes)
        {
            if (code.Length > 5 && int.TryParse(code.AsSpan(5), out var n) && n > max)
                max = n;
        }

        return $"DNTT-{max + 1:D4}";
    }
}
