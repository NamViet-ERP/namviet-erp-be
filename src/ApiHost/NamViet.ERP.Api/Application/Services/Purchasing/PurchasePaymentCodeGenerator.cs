using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchasePaymentCodeGenerator
{
    Task<string> NextAsync(CancellationToken ct = default);
}

public sealed class PurchasePaymentCodeGenerator(WarehouseDbContext db) : IPurchasePaymentCodeGenerator
{
    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        const string prefix = "DNTT-";
        var codes = await db.PurchasePayments
            .IgnoreQueryFilters()
            .Select(p => p.Code)
            .Where(c => c.StartsWith(prefix))
            .ToListAsync(ct);

        return SequentialCodeHelper.Format(prefix, SequentialCodeHelper.NextSequence(codes, prefix));
    }
}
