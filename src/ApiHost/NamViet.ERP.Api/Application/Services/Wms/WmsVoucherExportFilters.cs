namespace NamViet.ERP.Api.Application.Services.Wms;

internal static class WmsVoucherExportFilters
{
    public static List<T> FilterRows<T>(
        IReadOnlyList<T> rows,
        string? status,
        string? search,
        Func<T, string> statusSelector,
        params Func<T, string?>[] searchSelectors)
    {
        IEnumerable<T> query = rows;

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = status.Trim().ToLowerInvariant();
            query = query.Where(row => string.Equals(statusSelector(row), normalized, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim().ToLowerInvariant();
            query = query.Where(row =>
                searchSelectors.Any(selector =>
                {
                    var value = selector(row);
                    return !string.IsNullOrWhiteSpace(value)
                           && value.Contains(needle, StringComparison.OrdinalIgnoreCase);
                }));
        }

        return query.ToList();
    }
}
