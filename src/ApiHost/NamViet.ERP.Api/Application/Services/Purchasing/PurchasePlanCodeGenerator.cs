using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchasePlanCodeGenerator
{
    Task<string> NextAsync(string kind = PurchasePlanKinds.Plan, CancellationToken ct = default);
}

public sealed class PurchasePlanCodeGenerator(WarehouseDbContext db) : IPurchasePlanCodeGenerator
{
    public async Task<string> NextAsync(string kind = PurchasePlanKinds.Plan, CancellationToken ct = default)
    {
        var prefix = string.Equals(kind, PurchasePlanKinds.Adhoc, StringComparison.OrdinalIgnoreCase)
            ? "MHPS-"
            : "KHMH-";

        var codes = await db.PurchasePlans
            .IgnoreQueryFilters()
            .Select(p => p.Code)
            .Where(c => c.StartsWith(prefix))
            .ToListAsync(ct);

        return SequentialCodeHelper.Format(prefix, SequentialCodeHelper.NextSequence(codes, prefix));
    }
}
