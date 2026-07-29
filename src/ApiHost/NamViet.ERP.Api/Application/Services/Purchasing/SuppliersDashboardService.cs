using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface ISuppliersDashboardService
{
    Task<SuppliersDashboardDto> GetAsync(
        string? period, DateTime? from, DateTime? to, int? supplierId, CancellationToken ct = default);
}

/// <summary>
/// Tổng quan NCC: công nợ suy ra từ giá trị PO, thanh toán từ DNTT đã trả (status = paid).
/// Mốc thời gian của thanh toán dùng CreatedAt vì hệ thống chưa lưu ngày trả thực tế.
/// </summary>
public sealed class SuppliersDashboardService(WarehouseDbContext db) : ISuppliersDashboardService
{
    private const int TopSuppliers = 10;
    private const int TrendMonths = 12;

    public async Task<SuppliersDashboardDto> GetAsync(
        string? period, DateTime? from, DateTime? to, int? supplierId, CancellationToken ct = default)
    {
        var (rangeFrom, rangeTo, periodKey) =
            PurchasingDashboardService.ResolveRange(period, from, to);

        var orders = db.PurchaseOrders.AsNoTracking()
            .Where(o => o.CreatedAt >= rangeFrom && o.CreatedAt < rangeTo && o.SupplierId != null);
        var payments = db.PurchasePayments.AsNoTracking()
            .Where(p => p.Status == PurchasePaymentStatuses.Paid
                        && p.CreatedAt >= rangeFrom && p.CreatedAt < rangeTo
                        && p.SupplierId != null);

        if (supplierId is int sid)
        {
            orders = orders.Where(o => o.SupplierId == sid);
            payments = payments.Where(p => p.SupplierId == sid);
        }

        var supplierCount = await db.Suppliers.AsNoTracking().CountAsync(ct);
        var contactCount = await db.SupplierContacts.AsNoTracking()
            .CountAsync(c => c.Supplier != null, ct);
        var orderCount = await orders.CountAsync(ct);

        var debtRows = await orders
            .GroupBy(o => new { o.SupplierId, Name = o.Supplier!.SupplierName })
            .Select(g => new
            {
                SupplierId = g.Key.SupplierId!.Value,
                g.Key.Name,
                Debt = g.Sum(o => o.TotalAmount),
            })
            .ToListAsync(ct);

        var paidRows = await payments
            .GroupBy(p => p.SupplierId)
            .Select(g => new { SupplierId = g.Key!.Value, Paid = g.Sum(p => p.Amount) })
            .ToListAsync(ct);

        var paidBySupplier = paidRows.ToDictionary(r => r.SupplierId, r => r.Paid);

        var debtBySupplier = debtRows
            .Select(r =>
            {
                var paid = paidBySupplier.GetValueOrDefault(r.SupplierId, 0m);
                return new SupplierDebtRowDto(
                    r.SupplierId,
                    r.Name,
                    Math.Round(r.Debt, 2),
                    Math.Round(paid, 2),
                    Math.Round(r.Debt - paid, 2));
            })
            .OrderByDescending(r => r.Remaining)
            .ThenBy(r => r.SupplierName)
            .Take(TopSuppliers)
            .ToList();

        var debtTotal = Math.Round(debtRows.Sum(r => r.Debt), 2);
        var paymentTotal = Math.Round(paidRows.Sum(r => r.Paid), 2);

        var trend = await BuildTrendAsync(orders, payments, rangeFrom, rangeTo, ct);

        return new SuppliersDashboardDto(
            supplierCount,
            contactCount,
            orderCount,
            debtTotal,
            paymentTotal,
            Math.Round(debtTotal - paymentTotal, 2),
            debtBySupplier,
            trend,
            rangeFrom,
            rangeTo,
            periodKey,
            DateTime.UtcNow);
    }

    private static async Task<List<SupplierTrendPointDto>> BuildTrendAsync(
        IQueryable<PurchaseOrder> orders,
        IQueryable<PurchasePayment> payments,
        DateTime rangeFrom,
        DateTime rangeTo,
        CancellationToken ct)
    {
        // Giới hạn 12 tháng gần nhất trong kỳ để kỳ "từ trước đến nay" không sinh hàng nghìn cột.
        var lastBucket = new DateTime(rangeTo.Year, rangeTo.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var earliestBucket = lastBucket.AddMonths(-(TrendMonths - 1));
        var firstBucket = rangeFrom > earliestBucket
            ? new DateTime(rangeFrom.Year, rangeFrom.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            : earliestBucket;

        var debtByMonth = await orders
            .Where(o => o.CreatedAt >= firstBucket)
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(o => o.TotalAmount) })
            .ToListAsync(ct);

        var paymentByMonth = await payments
            .Where(p => p.CreatedAt >= firstBucket)
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(ct);

        var debtLookup = debtByMonth.ToDictionary(x => (x.Year, x.Month), x => x.Amount);
        var paymentLookup = paymentByMonth.ToDictionary(x => (x.Year, x.Month), x => x.Amount);

        var points = new List<SupplierTrendPointDto>();
        for (var bucket = firstBucket; bucket <= lastBucket; bucket = bucket.AddMonths(1))
        {
            var key = (bucket.Year, bucket.Month);
            points.Add(new SupplierTrendPointDto(
                $"T{bucket.Month}",
                Math.Round(debtLookup.GetValueOrDefault(key, 0m), 2),
                Math.Round(paymentLookup.GetValueOrDefault(key, 0m), 2)));
        }

        return points;
    }
}
