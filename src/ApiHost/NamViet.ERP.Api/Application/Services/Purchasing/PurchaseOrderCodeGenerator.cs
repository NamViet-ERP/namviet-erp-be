using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchaseOrderCodeGenerator
{
    Task<string> NextAsync(CancellationToken ct = default);
}

public sealed class PurchaseOrderCodeGenerator(WarehouseDbContext db) : IPurchaseOrderCodeGenerator
{
    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        const string prefix = "PO-";
        var codes = await db.PurchaseOrders
            .IgnoreQueryFilters()
            .Select(p => p.Code)
            .Where(c => c.StartsWith(prefix))
            .ToListAsync(ct);

        return SequentialCodeHelper.Format(prefix, SequentialCodeHelper.NextSequence(codes, prefix));
    }
}
