using NamViet.ERP.Modules.Warehouse.Domain.Entities;

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
            var start = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(t => t.CreatedAt >= start);
        }

        if (dateTo is { } to)
        {
            var endExclusive = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(t => t.CreatedAt < endExclusive);
        }

        return query;
    }
}
