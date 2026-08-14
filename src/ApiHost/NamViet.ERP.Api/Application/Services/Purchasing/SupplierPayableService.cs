using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface ISupplierPayableService
{
    Task<SupplierPayableReportDto> GetReportAsync(
        int? supplierId, string? status, string? search, CancellationToken ct = default);

    Task<SupplierPayableSnapshotDto?> GetSnapshotAsync(int supplierId, CancellationToken ct = default);
}

/// <summary>
/// Công nợ NCC = phần DNTT chưa trả hết (không lấy tổng giá trị PO).
/// DNTT đã hủy không tính. Null/0 CreditLimit = không giới hạn.
/// </summary>
public sealed class SupplierPayableService(WarehouseDbContext db) : ISupplierPayableService
{
    public async Task<SupplierPayableReportDto> GetReportAsync(
        int? supplierId, string? status, string? search, CancellationToken ct = default)
    {
        var built = await BuildAsync(ct);

        var supplierRows = built.SupplierRows
            .OrderByDescending(r => r.OverdueAmount)
            .ThenByDescending(r => r.Outstanding)
            .ThenBy(r => r.SupplierName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lines = FilterLines(built.Lines, supplierId, status, search);
        var upcoming = built.Upcoming
            .Where(u => !supplierId.HasValue || u.SupplierId == supplierId)
            .ToList();
        var upcoming7 = upcoming.Where(u => u.DaysUntilDue <= 7).Sum(u => u.Outstanding);

        return new SupplierPayableReportDto(
            built.Kpis,
            built.Aging,
            supplierRows,
            Math.Round(upcoming7, 2),
            Math.Round(upcoming.Sum(u => u.Outstanding), 2),
            upcoming,
            lines);
    }

    public async Task<SupplierPayableSnapshotDto?> GetSnapshotAsync(int supplierId, CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.AsNoTracking()
            .Where(s => s.Id == supplierId)
            .Select(s => new { s.Id, s.SupplierCode, s.SupplierName, s.CreditLimit, s.MaxDebtDays })
            .FirstOrDefaultAsync(ct);
        if (supplier is null) return null;

        var built = await BuildAsync(ct);
        var row = built.SupplierRows.FirstOrDefault(r => r.SupplierId == supplierId);
        var outstanding = row?.Outstanding ?? 0m;
        var creditLimit = NormalizeLimit(supplier.CreditLimit);
        return new SupplierPayableSnapshotDto(
            supplier.Id,
            supplier.SupplierCode,
            supplier.SupplierName,
            outstanding,
            row?.OverdueAmount ?? 0m,
            row?.UnpaidCount ?? 0,
            row?.OverdueCount ?? 0,
            creditLimit,
            IsOverLimit(outstanding, creditLimit),
            supplier.MaxDebtDays);
    }

    private async Task<BuiltReport> BuildAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(30);

        var payments = await db.PurchasePayments.AsNoTracking()
            .Where(p => p.Status != PurchasePaymentStatuses.Cancelled)
            .Select(p => new
            {
                p.Code,
                p.PoCode,
                p.StageNumber,
                p.SupplierId,
                p.SupplierNameSnapshot,
                p.Amount,
                p.DueDate,
                p.Status,
                p.PaidAt,
                p.PaidByName,
            })
            .ToListAsync(ct);

        var poCodes = payments.Select(p => p.PoCode).Distinct().ToList();
        var orders = poCodes.Count == 0
            ? []
            : await db.PurchaseOrders.AsNoTracking()
                .Where(o => poCodes.Contains(o.Code))
                .Select(o => new { o.Code, o.ProjectCode, o.OrderDate, o.SupplierPaymentTermsDays })
                .ToListAsync(ct);
        var orderMap = orders.ToDictionary(o => o.Code, o => o, StringComparer.OrdinalIgnoreCase);

        var projectCodes = orders
            .Select(o => o.ProjectCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();
        var projectMap = projectCodes.Count == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : await db.Projects.AsNoTracking()
                .Where(p => projectCodes.Contains(p.ProjectCode))
                .ToDictionaryAsync(p => p.ProjectCode, p => p.ProjectName, StringComparer.OrdinalIgnoreCase, ct);

        var supplierIds = payments
            .Where(p => p.SupplierId != null)
            .Select(p => p.SupplierId!.Value)
            .Distinct()
            .ToList();
        var suppliers = supplierIds.Count == 0
            ? []
            : await db.Suppliers.AsNoTracking()
                .Where(s => supplierIds.Contains(s.Id))
                .Select(s => new { s.Id, s.SupplierCode, s.SupplierName, s.CreditLimit })
                .ToListAsync(ct);
        var supplierMap = suppliers.ToDictionary(s => s.Id);

        var lines = new List<SupplierPayableLineDto>(payments.Count);
        foreach (var p in payments)
        {
            var paid = string.Equals(p.Status, PurchasePaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
                ? p.Amount
                : 0m;
            var outstanding = Math.Round(p.Amount - paid, 2);
            var daysOverdue = today.DayNumber - p.DueDate.DayNumber;
            var overdue = outstanding > 0 && daysOverdue > 0;
            var lineStatus = outstanding <= 0
                ? "paid"
                : overdue
                    ? "overdue"
                    : paid > 0
                        ? "partial"
                        : "unpaid";

            orderMap.TryGetValue(p.PoCode, out var order);
            var projectCode = order?.ProjectCode;
            string? projectName = null;
            if (!string.IsNullOrWhiteSpace(projectCode))
                projectMap.TryGetValue(projectCode, out projectName);

            string? supplierCode = null;
            string? supplierName = p.SupplierNameSnapshot;
            if (p.SupplierId is int sid && supplierMap.TryGetValue(sid, out var supplier))
            {
                supplierCode = supplier.SupplierCode;
                supplierName = supplier.SupplierName;
            }

            var termsDays = order?.SupplierPaymentTermsDays;
            var paymentTerm = termsDays is > 0 ? $"{termsDays} ngày" : null;

            lines.Add(new SupplierPayableLineDto(
                p.Code,
                p.PoCode,
                order?.OrderDate,
                projectCode,
                projectName,
                p.SupplierId,
                supplierCode,
                supplierName,
                p.StageNumber,
                Math.Round(p.Amount, 2),
                Math.Round(paid, 2),
                outstanding,
                p.DueDate,
                overdue ? daysOverdue : 0,
                lineStatus,
                p.Status,
                paymentTerm,
                string.Equals(p.Status, PurchasePaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
                    ? p.PaidAt
                    : null,
                string.Equals(p.Status, PurchasePaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
                    ? p.PaidByName
                    : null));
        }

        var supplierRows = lines
            .Where(l => l.SupplierId != null)
            .GroupBy(l => l.SupplierId!.Value)
            .Select(g =>
            {
                var first = g.First();
                var requested = g.Sum(x => x.Requested);
                var paid = g.Sum(x => x.Paid);
                var outstanding = g.Sum(x => x.Outstanding);
                var overdueAmount = g.Where(x => x.LineStatus == "overdue").Sum(x => x.Outstanding);
                var overdueCount = g.Count(x => x.LineStatus == "overdue");
                var unpaidCount = g.Count(x => x.Outstanding > 0);
                supplierMap.TryGetValue(g.Key, out var supplier);
                var creditLimit = NormalizeLimit(supplier?.CreditLimit);
                return new SupplierPayableSupplierRowDto(
                    g.Key,
                    supplier?.SupplierCode ?? first.SupplierCode ?? string.Empty,
                    supplier?.SupplierName ?? first.SupplierName ?? string.Empty,
                    g.Select(x => x.PoCode).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    g.Select(x => x.ProjectCode)
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    Math.Round(requested, 2),
                    Math.Round(paid, 2),
                    Math.Round(outstanding, 2),
                    creditLimit,
                    IsOverLimit(outstanding, creditLimit),
                    Math.Round(overdueAmount, 2),
                    overdueCount,
                    unpaidCount);
            })
            .ToList();

        var agingKeys = new (string Key, string Label, Func<int, bool> Match)[]
        {
            ("current", "Chưa đến hạn", d => d <= 0),
            ("d1_30", "Quá hạn 1-30 ngày", d => d is >= 1 and <= 30),
            ("d31_60", "Quá hạn 31-60 ngày", d => d is >= 31 and <= 60),
            ("d61_90", "Quá hạn 61-90 ngày", d => d is >= 61 and <= 90),
            ("over90", "Quá hạn trên 90 ngày", d => d > 90),
        };
        var openLines = lines.Where(l => l.Outstanding > 0).ToList();
        var aging = agingKeys.Select(b =>
        {
            var bucket = openLines.Where(l =>
            {
                var days = today.DayNumber - l.DueDate.DayNumber;
                return b.Match(days);
            }).ToList();
            return new SupplierPayableAgingBucketDto(
                b.Key,
                b.Label,
                Math.Round(bucket.Sum(x => x.Outstanding), 2),
                bucket.Count);
        }).ToList();

        var upcoming = openLines
            .Where(l => l.DueDate >= today && l.DueDate <= horizon)
            .OrderBy(l => l.DueDate)
            .ThenBy(l => l.SupplierName)
            .Select(l => new SupplierPayableUpcomingDto(
                l.PaymentCode,
                l.PoCode,
                l.ProjectCode,
                l.SupplierId,
                l.SupplierName,
                l.DueDate,
                l.Outstanding,
                l.DueDate.DayNumber - today.DayNumber))
            .ToList();

        var kpis = new SupplierPayableKpiDto(
            Math.Round(supplierRows.Sum(r => r.Outstanding), 2),
            Math.Round(supplierRows.Sum(r => r.OverdueAmount), 2),
            supplierRows.Count(r => r.OverdueCount > 0),
            supplierRows.Count(r => r.OverLimit));

        var sortedLines = lines
            .OrderBy(l => l.LineStatus == "overdue" ? 0 : l.LineStatus == "paid" ? 2 : 1)
            .ThenByDescending(l => l.DueDate)
            .ToList();

        return new BuiltReport(kpis, aging, supplierRows, upcoming, sortedLines);
    }

    private static List<SupplierPayableLineDto> FilterLines(
        IReadOnlyList<SupplierPayableLineDto> source,
        int? supplierId,
        string? status,
        string? search)
    {
        IEnumerable<SupplierPayableLineDto> lines = source;
        if (supplierId is int sid)
            lines = lines.Where(l => l.SupplierId == sid);

        var statusKey = status?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(statusKey) && statusKey != "all")
        {
            if (statusKey == "unpaid")
                lines = lines.Where(l => l.Outstanding > 0);
            else if (statusKey == "overdue")
                lines = lines.Where(l => l.LineStatus == "overdue");
            else if (statusKey == "paid")
                lines = lines.Where(l => l.LineStatus == "paid");
            else if (statusKey == "partial")
            {
                var mixed = source
                    .GroupBy(l => l.PoCode, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Any(x => x.Paid > 0) && g.Any(x => x.Outstanding > 0))
                    .Select(g => g.Key)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                lines = lines.Where(l => mixed.Contains(l.PoCode));
            }
        }

        var q = search?.Trim();
        if (!string.IsNullOrWhiteSpace(q))
        {
            lines = lines.Where(l =>
                (l.PoCode?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (l.PaymentCode?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (l.SupplierName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (l.SupplierCode?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (l.ProjectCode?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return lines.ToList();
    }

    private static decimal? NormalizeLimit(decimal? value)
        => value is > 0 ? Math.Round(value.Value, 2) : null;

    private static bool IsOverLimit(decimal outstanding, decimal? creditLimit)
        => creditLimit is > 0 && outstanding > creditLimit.Value;

    private sealed record BuiltReport(
        SupplierPayableKpiDto Kpis,
        IReadOnlyList<SupplierPayableAgingBucketDto> Aging,
        IReadOnlyList<SupplierPayableSupplierRowDto> SupplierRows,
        IReadOnlyList<SupplierPayableUpcomingDto> Upcoming,
        IReadOnlyList<SupplierPayableLineDto> Lines);
}
