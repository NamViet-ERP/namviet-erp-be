using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Time;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchaseRfqService
{
    Task<PurchaseRfqQuotePagedResponse> ListAsync(
        int page,
        int pageSize,
        int? supplierId,
        string? status,
        int? year,
        int? quarter,
        string? search,
        CancellationToken ct = default);

    Task<PurchaseRfqQuoteDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PurchaseRfqQuoteDto> CreateAsync(UpsertPurchaseRfqQuoteRequest request, CancellationToken ct = default);

    Task<PurchaseRfqQuoteDto?> UpdateAsync(Guid id, UpsertPurchaseRfqQuoteRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseRfqAverageDto>> AveragesAsync(
        string groupBy,
        int? year,
        int? quarter = null,
        int? supplierId = null,
        string? status = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseRfqWinRateDto>> WinRatesAsync(
        int? supplierId = null,
        string? status = null,
        int? year = null,
        int? quarter = null,
        CancellationToken ct = default);
}

public sealed class PurchaseRfqService(WarehouseDbContext db) : IPurchaseRfqService
{
    public async Task<PurchaseRfqQuotePagedResponse> ListAsync(
        int page,
        int pageSize,
        int? supplierId,
        string? status,
        int? year,
        int? quarter,
        string? search,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        await PersistExpiredAsync(ct);

        var query = db.PurchaseRfqQuotes.AsNoTracking().AsQueryable();
        if (supplierId is > 0)
            query = query.Where(q => q.SupplierId == supplierId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(q => q.Status.ToLower() == status.Trim().ToLower());
        if (year is >= 2000)
        {
            var from = new DateOnly(year.Value, 1, 1);
            var to = new DateOnly(year.Value, 12, 31);
            if (quarter is >= 1 and <= 4)
            {
                var startMonth = (quarter.Value - 1) * 3 + 1;
                from = new DateOnly(year.Value, startMonth, 1);
                to = from.AddMonths(3).AddDays(-1);
            }
            query = query.Where(q => q.QuoteDate >= from && q.QuoteDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(q =>
                q.MaterialCode.ToLower().Contains(s)
                || q.MaterialName.ToLower().Contains(s)
                || q.SupplierName.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        var rows = await query
            .OrderByDescending(q => q.QuoteDate)
            .ThenByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PurchaseRfqQuotePagedResponse(
            rows.Select(ToDto).ToList(), total, page, pageSize, totalPages);
    }

    public async Task<PurchaseRfqQuoteDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await PersistExpiredAsync(ct);
        var row = await db.PurchaseRfqQuotes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id, ct);
        return row is null ? null : ToDto(row);
    }

    public async Task<PurchaseRfqQuoteDto> CreateAsync(
        UpsertPurchaseRfqQuoteRequest request, CancellationToken ct = default)
    {
        var entity = await MapFieldsAsync(new PurchaseRfqQuote(), request, ct);
        ApplyOpenLifecycleStatus(entity);
        var now = DateTime.UtcNow;
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        db.PurchaseRfqQuotes.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<PurchaseRfqQuoteDto?> UpdateAsync(
        Guid id, UpsertPurchaseRfqQuoteRequest request, CancellationToken ct = default)
    {
        await PersistExpiredAsync(ct);
        var entity = await db.PurchaseRfqQuotes.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (entity is null) return null;

        var current = entity.Status.Trim().ToLowerInvariant();
        if (current is PurchaseRfqQuoteStatuses.Won or PurchaseRfqQuoteStatuses.Lost)
            throw new PurchasingConflictException("Báo giá đã kết thúc (thắng/thua), không thể chỉnh sửa.");

        var requested = NormalizeStatus(request.Status);
        if (requested is PurchaseRfqQuoteStatuses.Won or PurchaseRfqQuoteStatuses.Lost)
        {
            if (current != PurchaseRfqQuoteStatuses.Active)
                throw new PurchasingConflictException("Chỉ báo giá còn hiệu lực mới đánh dấu thắng/thua.");
            await MapFieldsAsync(entity, request, ct);
            entity.Status = requested;
        }
        else
        {
            await MapFieldsAsync(entity, request, ct);
            ApplyOpenLifecycleStatus(entity);
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<PurchaseRfqAverageDto>> AveragesAsync(
        string groupBy,
        int? year,
        int? quarter = null,
        int? supplierId = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var maxYear = VietnamClock.Today.Year;
        var minYear = maxYear - 4;
        var query = ApplyListFilters(
            db.PurchaseRfqQuotes.AsNoTracking().Where(q => q.QuoteDate.Year >= minYear),
            supplierId, status, year, quarter);

        var rows = await query.Select(q => new { q.QuoteDate, q.UnitPrice }).ToListAsync(ct);
        var byYear = string.Equals(groupBy, "year", StringComparison.OrdinalIgnoreCase);
        if (byYear)
        {
            return rows
                .GroupBy(r => r.QuoteDate.Year)
                .OrderByDescending(g => g.Key)
                .Select(g => new PurchaseRfqAverageDto(
                    $"Năm {g.Key}", g.Key, null,
                    Math.Round(g.Average(x => x.UnitPrice), 2), g.Count()))
                .ToList();
        }

        return rows
            .GroupBy(r => (r.QuoteDate.Year, Quarter: (r.QuoteDate.Month - 1) / 3 + 1))
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Quarter)
            .Select(g => new PurchaseRfqAverageDto(
                $"Q{g.Key.Quarter}/{g.Key.Year}", g.Key.Year, g.Key.Quarter,
                Math.Round(g.Average(x => x.UnitPrice), 2), g.Count()))
            .ToList();
    }

    public async Task<IReadOnlyList<PurchaseRfqWinRateDto>> WinRatesAsync(
        int? supplierId = null,
        string? status = null,
        int? year = null,
        int? quarter = null,
        CancellationToken ct = default)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
        IQueryable<PurchaseRfqQuote> query = db.PurchaseRfqQuotes.AsNoTracking();
        if (normalized is PurchaseRfqQuoteStatuses.Won or PurchaseRfqQuoteStatuses.Lost)
            query = query.Where(q => q.Status.ToLower() == normalized);
        else
            query = query.Where(q =>
                q.Status == PurchaseRfqQuoteStatuses.Won || q.Status == PurchaseRfqQuoteStatuses.Lost);

        query = ApplyListFilters(query, supplierId, status: null, year, quarter);

        var rows = await query
            .GroupBy(q => new { q.SupplierId, q.SupplierName })
            .Select(g => new
            {
                g.Key.SupplierId,
                g.Key.SupplierName,
                Won = g.Count(x => x.Status == PurchaseRfqQuoteStatuses.Won),
                Total = g.Count(),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new PurchaseRfqWinRateDto(
                r.SupplierId, r.SupplierName, r.Won, r.Total,
                r.Total == 0 ? 0 : (int)Math.Round(r.Won * 100d / r.Total)))
            .OrderByDescending(r => r.Rate)
            .ToList();
    }

    private static IQueryable<PurchaseRfqQuote> ApplyListFilters(
        IQueryable<PurchaseRfqQuote> query,
        int? supplierId,
        string? status,
        int? year,
        int? quarter)
    {
        if (supplierId is > 0)
            query = query.Where(q => q.SupplierId == supplierId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(q => q.Status.ToLower() == status.Trim().ToLower());
        if (year is >= 2000)
        {
            var from = new DateOnly(year.Value, 1, 1);
            var to = new DateOnly(year.Value, 12, 31);
            if (quarter is >= 1 and <= 4)
            {
                var startMonth = (quarter.Value - 1) * 3 + 1;
                from = new DateOnly(year.Value, startMonth, 1);
                to = from.AddMonths(3).AddDays(-1);
            }
            query = query.Where(q => q.QuoteDate >= from && q.QuoteDate <= to);
        }

        return query;
    }

    /// <summary>Ghi expired vào DB khi hạn giữ giá đã qua (không để active ảo).</summary>
    private async Task PersistExpiredAsync(CancellationToken ct)
    {
        var today = VietnamClock.Today;
        var now = DateTime.UtcNow;
        await db.PurchaseRfqQuotes
            .Where(q =>
                q.Status == PurchaseRfqQuoteStatuses.Active
                && q.ValidUntil != null
                && q.ValidUntil < today)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(q => q.Status, PurchaseRfqQuoteStatuses.Expired)
                    .SetProperty(q => q.UpdatedAt, now),
                ct);
    }

    private async Task<PurchaseRfqQuote> MapFieldsAsync(
        PurchaseRfqQuote entity, UpsertPurchaseRfqQuoteRequest request, CancellationToken ct)
    {
        var code = request.MaterialCode.Trim();
        var mat = await db.Materials.AsNoTracking().FirstOrDefaultAsync(m => m.MaterialCode == code, ct)
            ?? throw new PurchasingConflictException($"Không tìm thấy vật tư {code}.");
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct)
            ?? throw new PurchasingConflictException("Nhà cung cấp không tồn tại.");
        if (request.UnitPrice <= 0)
            throw new PurchasingConflictException("Đơn báo giá phải lớn hơn 0.");

        entity.MaterialCode = mat.MaterialCode;
        entity.MaterialName = mat.MaterialName;
        entity.SupplierId = supplier.Id;
        entity.SupplierName = supplier.SupplierName;
        entity.ProjectCode = string.IsNullOrWhiteSpace(request.ProjectCode) ? null : request.ProjectCode.Trim();
        entity.QuoteDate = request.QuoteDate;
        entity.UnitPrice = request.UnitPrice;
        entity.ValidUntil = request.ValidUntil;
        entity.LeadTimeDays = request.LeadTimeDays;
        entity.PaymentTerm = string.IsNullOrWhiteSpace(request.PaymentTerm) ? null : request.PaymentTerm.Trim();
        entity.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        return entity;
    }

    /// <summary>Phiếu mở (chưa thắng/thua): còn hiệu lực hoặc hết hạn theo hạn giữ giá.</summary>
    private static void ApplyOpenLifecycleStatus(PurchaseRfqQuote entity)
    {
        var today = VietnamClock.Today;
        entity.Status = entity.ValidUntil is { } until && until < today
            ? PurchaseRfqQuoteStatuses.Expired
            : PurchaseRfqQuoteStatuses.Active;
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        var t = status.Trim().ToLowerInvariant();
        return PurchaseRfqQuoteStatuses.Allowed.Contains(t) ? t : null;
    }

    private static PurchaseRfqQuoteDto ToDto(PurchaseRfqQuote q) =>
        new(
            q.Id, q.MaterialCode, q.MaterialName, q.SupplierId, q.SupplierName, q.ProjectCode,
            q.QuoteDate, q.UnitPrice, q.ValidUntil, q.LeadTimeDays, q.PaymentTerm, q.Status, q.PoRef, q.Note);
}
