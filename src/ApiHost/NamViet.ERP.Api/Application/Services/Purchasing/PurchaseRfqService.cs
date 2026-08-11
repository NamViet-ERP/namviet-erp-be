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
        string groupBy, int? year, CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseRfqWinRateDto>> WinRatesAsync(CancellationToken ct = default);
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
        var today = VietnamClock.Today;

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
            rows.Select(q => ToDto(q, today)).ToList(), total, page, pageSize, totalPages);
    }

    public async Task<PurchaseRfqQuoteDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.PurchaseRfqQuotes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id, ct);
        return row is null ? null : ToDto(row, VietnamClock.Today);
    }

    public async Task<PurchaseRfqQuoteDto> CreateAsync(
        UpsertPurchaseRfqQuoteRequest request, CancellationToken ct = default)
    {
        var entity = await MapToEntityAsync(new PurchaseRfqQuote(), request, ct);
        var now = DateTime.UtcNow;
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        db.PurchaseRfqQuotes.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity, VietnamClock.Today);
    }

    public async Task<PurchaseRfqQuoteDto?> UpdateAsync(
        Guid id, UpsertPurchaseRfqQuoteRequest request, CancellationToken ct = default)
    {
        var entity = await db.PurchaseRfqQuotes.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (entity is null) return null;
        await MapToEntityAsync(entity, request, ct);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(entity, VietnamClock.Today);
    }

    public async Task<IReadOnlyList<PurchaseRfqAverageDto>> AveragesAsync(
        string groupBy, int? year, CancellationToken ct = default)
    {
        var maxYear = VietnamClock.Today.Year;
        var minYear = maxYear - 4;
        var query = db.PurchaseRfqQuotes.AsNoTracking().Where(q => q.QuoteDate.Year >= minYear);
        if (year is >= 2000)
            query = query.Where(q => q.QuoteDate.Year == year.Value);

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

    public async Task<IReadOnlyList<PurchaseRfqWinRateDto>> WinRatesAsync(CancellationToken ct = default)
    {
        var rows = await db.PurchaseRfqQuotes.AsNoTracking()
            .Where(q => q.Status == PurchaseRfqQuoteStatuses.Won || q.Status == PurchaseRfqQuoteStatuses.Lost)
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

    private async Task<PurchaseRfqQuote> MapToEntityAsync(
        PurchaseRfqQuote entity, UpsertPurchaseRfqQuoteRequest request, CancellationToken ct)
    {
        var code = request.MaterialCode.Trim();
        var mat = await db.Materials.AsNoTracking().FirstOrDefaultAsync(m => m.MaterialCode == code, ct)
            ?? throw new PurchasingConflictException($"Không tìm thấy vật tư {code}.");
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct)
            ?? throw new PurchasingConflictException("Nhà cung cấp không tồn tại.");
        if (request.UnitPrice <= 0)
            throw new PurchasingConflictException("Đơn giá chào phải lớn hơn 0.");

        var status = string.IsNullOrWhiteSpace(request.Status)
            ? PurchaseRfqQuoteStatuses.Active
            : request.Status.Trim().ToLowerInvariant();
        if (!PurchaseRfqQuoteStatuses.Allowed.Contains(status))
            throw new PurchasingConflictException("Trạng thái báo giá không hợp lệ.");

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
        entity.Status = status;
        entity.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        return entity;
    }

    private static PurchaseRfqQuoteDto ToDto(PurchaseRfqQuote q, DateOnly today)
    {
        var status = q.Status;
        if (string.Equals(status, PurchaseRfqQuoteStatuses.Active, StringComparison.OrdinalIgnoreCase)
            && q.ValidUntil is { } until && until < today)
            status = PurchaseRfqQuoteStatuses.Expired;
        return new PurchaseRfqQuoteDto(
            q.Id, q.MaterialCode, q.MaterialName, q.SupplierId, q.SupplierName, q.ProjectCode,
            q.QuoteDate, q.UnitPrice, q.ValidUntil, q.LeadTimeDays, q.PaymentTerm, status, q.PoRef, q.Note);
    }
}
