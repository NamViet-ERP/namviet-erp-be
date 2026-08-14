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
/// Tổng quan NCC: công nợ từ DNTT (không hủy), không lấy tổng giá trị PO.
/// Thanh toán = DNTT status = paid. Mốc thời gian dùng CreatedAt vì chưa lưu ngày trả thực tế.
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
            .Where(p => p.Status != PurchasePaymentStatuses.Cancelled
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

        var requestedRows = await payments
            .GroupBy(p => p.SupplierId)
            .Select(g => new
            {
                SupplierId = g.Key!.Value,
                Requested = g.Sum(p => p.Amount),
                Paid = g.Sum(p => p.Status == PurchasePaymentStatuses.Paid ? p.Amount : 0m),
            })
            .ToListAsync(ct);

        var supplierIds = requestedRows.Select(r => r.SupplierId).ToList();
        var nameMap = supplierIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Suppliers.AsNoTracking()
                .Where(s => supplierIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.SupplierName, ct);

        var debtBySupplier = requestedRows
            .Select(r =>
            {
                var name = nameMap.GetValueOrDefault(r.SupplierId);
                if (string.IsNullOrWhiteSpace(name)) name = $"NCC #{r.SupplierId}";
                return new SupplierDebtRowDto(
                    r.SupplierId,
                    name,
                    Math.Round(r.Requested, 2),
                    Math.Round(r.Paid, 2),
                    Math.Round(r.Requested - r.Paid, 2));
            })
            .OrderByDescending(r => r.Remaining)
            .ThenBy(r => r.SupplierName)
            .Take(TopSuppliers)
            .ToList();

        var debtTotal = Math.Round(requestedRows.Sum(r => r.Requested), 2);
        var paymentTotal = Math.Round(requestedRows.Sum(r => r.Paid), 2);

        var paidTrendQuery = payments.Where(p => p.Status == PurchasePaymentStatuses.Paid);
        var trend = await BuildTrendAsync(payments, paidTrendQuery, rangeFrom, rangeTo, ct);

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
        IQueryable<PurchasePayment> requested,
        IQueryable<PurchasePayment> paid,
        DateTime rangeFrom,
        DateTime rangeTo,
        CancellationToken ct)
    {
        var lastBucket = new DateTime(rangeTo.Year, rangeTo.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var earliestBucket = lastBucket.AddMonths(-(TrendMonths - 1));
        var firstBucket = rangeFrom > earliestBucket
            ? new DateTime(rangeFrom.Year, rangeFrom.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            : earliestBucket;

        var debtByMonth = await requested
            .Where(p => p.CreatedAt >= firstBucket)
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(ct);

        var paymentByMonth = await paid
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
