using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchasingDashboardService
{
    Task<PurchasingDashboardSummaryDto> GetSummaryAsync(
        string? period, DateTime? from, DateTime? to, CancellationToken ct = default);

    Task<PurchasingDashboardChartDto> GetChartAsync(
        string? period, DateTime? from, DateTime? to, CancellationToken ct = default);

    Task<PurchasingActivityPagedResponse> GetActivityAsync(
        string? period,
        DateTime? from,
        DateTime? to,
        Guid? staffId,
        long? cursor,
        int limit,
        CancellationToken ct = default);

    Task<IReadOnlyList<PurchasingActivityActorDto>> GetActivityActorsAsync(
        string? period, DateTime? from, DateTime? to, CancellationToken ct = default);
}

public sealed class PurchasingDashboardService(
    WarehouseDbContext db,
    IdentityDbContext identity) : IPurchasingDashboardService
{
    public async Task<PurchasingDashboardSummaryDto> GetSummaryAsync(
        string? period, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var (rangeFrom, rangeTo, periodKey) = ResolveRange(period, from, to);

        var planGroups = await db.PurchasePlans.AsNoTracking()
            .Where(p => p.CreatedAt >= rangeFrom && p.CreatedAt < rangeTo)
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var planRows = planGroups
            .Select(g => new StatusCount(g.Status, g.Count))
            .ToList();

        var orderGroups = await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.CreatedAt >= rangeFrom && o.CreatedAt < rangeTo)
            .GroupBy(o => o.DeliveryStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var orderRows = orderGroups
            .Select(g => new StatusCount(g.Status, g.Count))
            .ToList();

        var importGroups = await db.WarehouseTransactions.AsNoTracking()
            .Where(t => t.TransactionType == TransactionType.Import
                        && t.CreatedAt >= rangeFrom
                        && t.CreatedAt < rangeTo
                        && (t.PurchasePlanCode != null || (t.ReferenceCode != null && t.ReferenceCode.StartsWith("PO-"))))
            .GroupBy(t => t.ApprovalStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var importRows = importGroups
            .Select(g => new StatusCount(g.Status.ToString(), g.Count))
            .ToList();

        return new PurchasingDashboardSummaryDto(
            BuildMetric(planRows, PlanStatusSpecs),
            BuildMetric(orderRows, OrderStatusSpecs),
            BuildMetric(importRows, ImportStatusSpecs),
            rangeFrom,
            rangeTo,
            periodKey,
            DateTime.UtcNow);
    }

    public async Task<PurchasingDashboardChartDto> GetChartAsync(
        string? period, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var (rangeFrom, rangeTo, periodKey) = ResolveRange(period, from, to);

        // Production = PO gắn dự án Project; Stock = GeneralWarehouse hoặc không có ProjectCode.
        var rows = await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.CreatedAt >= rangeFrom && o.CreatedAt < rangeTo)
            .Select(o => new
            {
                o.TotalAmount,
                ProjectType = o.Project != null ? (ProjectType?)o.Project.ProjectType : null,
            })
            .ToListAsync(ct);

        decimal productionAmount = 0m;
        decimal stockAmount = 0m;
        var productionCount = 0;
        var stockCount = 0;
        foreach (var row in rows)
        {
            if (row.ProjectType == ProjectType.Project)
            {
                productionAmount += row.TotalAmount;
                productionCount += 1;
            }
            else
            {
                stockAmount += row.TotalAmount;
                stockCount += 1;
            }
        }

        var bars = new List<PurchasingDashboardChartBarDto>();
        if (productionCount > 0)
            bars.Add(new("production", "Lượng mua sản xuất", productionCount, Math.Round(productionAmount, 2)));
        if (stockCount > 0)
            bars.Add(new("stock", "Lượng mua dự trữ", stockCount, Math.Round(stockAmount, 2)));

        return new PurchasingDashboardChartDto(bars, rangeFrom, rangeTo, periodKey, DateTime.UtcNow);
    }

    public async Task<PurchasingActivityPagedResponse> GetActivityAsync(
        string? period,
        DateTime? from,
        DateTime? to,
        Guid? staffId,
        long? cursor,
        int limit,
        CancellationToken ct = default)
    {
        var (rangeFrom, rangeTo, _) = ResolveRange(period, from, to);
        limit = Math.Clamp(limit, 1, 100);

        var q = db.ActivityEvents.AsNoTracking()
            .Where(e => e.Module == ActivityModules.Purchasing
                        && e.OccurredAt >= rangeFrom
                        && e.OccurredAt < rangeTo);

        if (staffId is Guid sid)
            q = q.Where(e => e.ActorUserId == sid);

        if (cursor is long c)
            q = q.Where(e => e.Id < c);

        var rows = await q
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = rows.Count > limit;
        if (hasMore)
            rows = rows.Take(limit).ToList();

        var items = rows.Select(e => new PurchasingActivityItemDto(
            e.Id,
            e.OccurredAt,
            e.ActorUserId,
            e.ActorName,
            e.Module,
            e.Action,
            e.EntityType,
            e.EntityCode,
            e.Message,
            e.CategoryLabel)).ToList();

        long? nextCursor = hasMore && items.Count > 0 ? items[^1].Id : null;
        return new PurchasingActivityPagedResponse(items, nextCursor, hasMore);
    }

    public async Task<IReadOnlyList<PurchasingActivityActorDto>> GetActivityActorsAsync(
        string? period, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        _ = (period, from, to);

        // ActorUserId on the feed is Identity.users.Id - list those logins.
        return await identity.Users.AsNoTracking()
            .Where(u => u.DeletedAt == null && u.IsActive)
            .OrderBy(u => u.Name)
            .ThenBy(u => u.EmployeeCode)
            .Select(u => new PurchasingActivityActorDto(u.Id, u.Name))
            .ToListAsync(ct);
    }

    /// <summary>
    /// period: empty=all, week, this_month, 1 (last month), this_year, last_year, custom (requires from/to).
    /// </summary>
    internal static (DateTime From, DateTime To, string Period) ResolveRange(
        string? period, DateTime? from, DateTime? to)
    {
        var now = DateTime.UtcNow;
        var key = (period ?? "all").Trim().ToLowerInvariant();
        if (key is "custom" or "giai_doan")
        {
            if (from is null || to is null)
                throw new ArgumentException("Kỳ 'custom' cần from và to.");
            var f = EnsureUtc(from.Value);
            var t = EnsureUtc(to.Value);
            if (t <= f)
                throw new ArgumentException("to phải sau from.");
            return (f, t, "custom");
        }

        return key switch
        {
            "" or "all" or "tu_truoc" => (DateTime.SpecifyKind(DateTime.MinValue.AddYears(1), DateTimeKind.Utc), now.AddDays(1), "all"),
            "week" or "this_week" => (StartOfWeekUtc(now), now.AddDays(1), "week"),
            "this_month" or "month" => (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), now.AddDays(1), "this_month"),
            "1" or "last_month" => LastMonthRange(now),
            "this_year" or "year" => (new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc), now.AddDays(1), "this_year"),
            "last_year" => (
                new DateTime(now.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                "last_year"),
            _ => (DateTime.SpecifyKind(DateTime.MinValue.AddYears(1), DateTimeKind.Utc), now.AddDays(1), "all"),
        };
    }

    private static (DateTime From, DateTime To, string Period) LastMonthRange(DateTime now)
    {
        var firstThis = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (firstThis.AddMonths(-1), firstThis, "last_month");
    }

    private static DateTime StartOfWeekUtc(DateTime now)
    {
        // Monday-start week (VN business week).
        var d = now.Date;
        var diff = ((int)d.DayOfWeek + 6) % 7;
        return DateTime.SpecifyKind(d.AddDays(-diff), DateTimeKind.Utc);
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private readonly record struct StatusCount(string Status, int Count);

    private readonly record struct StatusSpec(string[] Match, string Key, string Label);

    private static readonly StatusSpec[] PlanStatusSpecs =
    [
        new([PurchasePlanStatuses.Draft], "draft", "Nháp"),
        new([PurchasePlanStatuses.PendingL1, PurchasePlanStatuses.PendingL2], "pending", "Chờ duyệt"),
        new([PurchasePlanStatuses.Approved, PurchasePlanStatuses.ActiveLegacy], "approved", "Đã duyệt"),
        new([PurchasePlanStatuses.Rejected], "rejected", "Từ chối"),
        new([PurchasePlanStatuses.Closed], "closed", "Đã đóng"),
    ];

    private static readonly StatusSpec[] OrderStatusSpecs =
    [
        new([PurchaseOrderDeliveryStatuses.None], "none", "Chưa giao"),
        new([PurchaseOrderDeliveryStatuses.Partial], "partial", "Giao 1 phần"),
        new([PurchaseOrderDeliveryStatuses.Complete], "complete", "Đã giao đủ"),
    ];

    private static readonly StatusSpec[] ImportStatusSpecs =
    [
        new([nameof(ApprovalStatus.Draft)], "draft", "Nháp"),
        new([nameof(ApprovalStatus.Pending)], "pending", "Chờ duyệt"),
        new([nameof(ApprovalStatus.Approved)], "approved", "Đã duyệt"),
        new([nameof(ApprovalStatus.Rejected)], "rejected", "Từ chối"),
    ];

    private static PurchasingKpiMetricDto BuildMetric(IReadOnlyList<StatusCount> rows, StatusSpec[] specs)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var key = string.IsNullOrWhiteSpace(row.Status) ? string.Empty : row.Status.Trim();
            counts[key] = counts.GetValueOrDefault(key) + row.Count;
        }

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<PurchasingKpiLineDto>();
        var total = 0;
        foreach (var spec in specs)
        {
            var n = 0;
            foreach (var match in spec.Match)
            {
                n += counts.GetValueOrDefault(match);
                used.Add(match);
            }

            total += n;
            if (n > 0)
                lines.Add(new PurchasingKpiLineDto(spec.Key, spec.Label, n));
        }

        foreach (var (status, n) in counts)
        {
            if (n <= 0 || used.Contains(status))
                continue;
            total += n;
            lines.Add(new PurchasingKpiLineDto(status, status, n));
        }

        return new PurchasingKpiMetricDto(total, lines);
    }
}
