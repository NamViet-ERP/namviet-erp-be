using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Api.Application;

namespace NamViet.ERP.Api.Application.Services.Wms;

internal static class WmsVoucherQueryFilters
{
    public static IQueryable<WarehouseTransaction> WhereCreatedBetween(
        this IQueryable<WarehouseTransaction> query,
        DateOnly? dateFrom,
        DateOnly? dateTo)
    {
        if (dateFrom is { } from)
        {
            var start = UtcDateRange.Start(from)!.Value;
            query = query.Where(t => t.CreatedAt >= start);
        }

        if (dateTo is { } to)
        {
            var endExclusive = UtcDateRange.ExclusiveEnd(to)!.Value;
            query = query.Where(t => t.CreatedAt < endExclusive);
        }

        return query;
    }
}
