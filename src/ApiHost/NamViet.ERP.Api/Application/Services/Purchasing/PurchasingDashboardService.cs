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

        var plansQ = db.PurchasePlans.AsNoTracking()
            .Where(p => p.CreatedAt >= rangeFrom && p.CreatedAt < rangeTo);
        var planCount = await plansQ.CountAsync(ct);
        var planAmount = await plansQ.SumAsync(p => (decimal?)p.PurchaseTotal, ct) ?? 0m;

        var ordersQ = db.PurchaseOrders.AsNoTracking()
            .Where(o => o.CreatedAt >= rangeFrom && o.CreatedAt < rangeTo);
        var orderCount = await ordersQ.CountAsync(ct);
        var orderAmount = await ordersQ.SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

        var importTickets = db.WarehouseTransactions.AsNoTracking()
            .Where(t => t.TransactionType == TransactionType.Import
                        && t.CreatedAt >= rangeFrom
                        && t.CreatedAt < rangeTo
                        && (t.PurchasePlanCode != null || (t.ReferenceCode != null && t.ReferenceCode.StartsWith("PO-"))));

        var importCount = await importTickets.CountAsync(ct);
        var importCodes = await importTickets.Select(t => t.TicketCode).ToListAsync(ct);
        var importAmount = importCodes.Count == 0
            ? 0m
            : await db.WarehouseTransactionItems.AsNoTracking()
                .Where(i => importCodes.Contains(i.TicketCode))
                .SumAsync(i => (decimal?)(i.Quantity * i.UnitPrice), ct) ?? 0m;

        return new PurchasingDashboardSummaryDto(
            new PurchasingKpiMetricDto(planCount, Math.Round(planAmount, 2)),
            new PurchasingKpiMetricDto(orderCount, Math.Round(orderAmount, 2)),
            new PurchasingKpiMetricDto(importCount, Math.Round(importAmount, 2)),
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

        decimal production = 0m;
        decimal stock = 0m;
        foreach (var row in rows)
        {
            if (row.ProjectType == ProjectType.Project)
                production += row.TotalAmount;
            else
                stock += row.TotalAmount;
        }

        var bars = new List<PurchasingDashboardChartBarDto>
        {
            new("production", "Lượng mua sản xuất", Math.Round(production, 2)),
            new("stock", "Lượng mua dự trữ", Math.Round(stock, 2)),
        };

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
}
