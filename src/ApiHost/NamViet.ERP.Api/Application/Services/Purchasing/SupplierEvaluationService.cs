using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.SupplierEvaluation;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface ISupplierEvaluationService
{
    Task<SupplierEvaluationPagedResponse> ListAsync(
        int? supplierId, string? period, string? search, int page, int pageSize,
        CancellationToken ct = default);

    Task<SupplierEvaluationDto> GetByIdAsync(int id, CancellationToken ct = default);

    Task<SupplierEvaluationDto> CreateAsync(
        UpsertSupplierEvaluationRequest request, CancellationToken ct = default);

    Task<SupplierEvaluationDto> UpdateAsync(
        int id, UpsertSupplierEvaluationRequest request, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class SupplierEvaluationService(
    WarehouseDbContext db,
    ICurrentUser currentUser) : ISupplierEvaluationService
{
    public async Task<SupplierEvaluationPagedResponse> ListAsync(
        int? supplierId, string? period, string? search, int page, int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.SupplierEvaluations.AsNoTracking().Where(v => v.Supplier != null);
        if (supplierId is int sid)
            query = query.Where(v => v.SupplierId == sid);
        if (!string.IsNullOrWhiteSpace(period))
        {
            var p = period.Trim();
            query = query.Where(v => v.Period == p);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(v =>
                EF.Functions.ILike(v.Supplier!.SupplierName, $"%{q}%")
                || EF.Functions.ILike(v.Period, $"%{q}%")
                || EF.Functions.ILike(v.Grade, $"%{q}%")
                || (v.Note != null && EF.Functions.ILike(v.Note, $"%{q}%")));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(v => v.EvaluatedAt)
            .ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(v => ToDto(v, v.Supplier?.SupplierName ?? v.SupplierNameSnapshot ?? string.Empty)).ToList();

        // Supplier nav may be null with AsNoTracking without Include — reload names.
        if (items.Exists(i => string.IsNullOrEmpty(i.SupplierName)))
        {
            var ids = rows.Select(r => r.SupplierId).Distinct().ToArray();
            var names = await db.Suppliers.AsNoTracking()
                .Where(s => ids.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.SupplierName, ct);
            items = rows.Select(v => ToDto(
                v,
                names.GetValueOrDefault(v.SupplierId, v.SupplierNameSnapshot ?? string.Empty))).ToList();
        }

        return new SupplierEvaluationPagedResponse(
            items, total, page, pageSize, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<SupplierEvaluationDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var evaluation = await db.SupplierEvaluations.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy đánh giá NCC #{id}.");

        var name = await db.Suppliers.AsNoTracking()
            .Where(s => s.Id == evaluation.SupplierId)
            .Select(s => s.SupplierName)
            .FirstOrDefaultAsync(ct)
            ?? evaluation.SupplierNameSnapshot
            ?? string.Empty;

        return ToDto(evaluation, name);
    }

    public async Task<SupplierEvaluationDto> CreateAsync(
        UpsertSupplierEvaluationRequest request, CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy nhà cung cấp #{request.SupplierId}.");

        var period = RequirePeriod(request.Period);
        var (score, grade, criteria) = ComputeFromCriteria(request.CriteriaScores);

        if (await db.SupplierEvaluations
                .AnyAsync(v => v.SupplierId == supplier.Id && v.Period == period, ct))
            throw new PurchasingConflictException(
                $"NCC '{supplier.SupplierName}' đã có đánh giá kỳ {period}.");

        var evaluation = new SupplierEvaluation
        {
            SupplierId = supplier.Id,
            SupplierNameSnapshot = supplier.SupplierName,
            Period = period,
            Score = score,
            Grade = grade,
            CriteriaScores = criteria,
            EvaluatedAt = request.EvaluatedAt ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Note = Trim(request.Note),
            EvaluatedById = currentUser.IsAuthenticated ? currentUser.Id : null,
            EvaluatedByName = currentUser.IsAuthenticated ? currentUser.Name : null,
        };

        db.SupplierEvaluations.Add(evaluation);
        await db.SaveChangesAsync(ct);

        return ToDto(evaluation, supplier.SupplierName);
    }

    public async Task<SupplierEvaluationDto> UpdateAsync(
        int id, UpsertSupplierEvaluationRequest request, CancellationToken ct = default)
    {
        var evaluation = await db.SupplierEvaluations.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy đánh giá NCC #{id}.");

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy nhà cung cấp #{request.SupplierId}.");

        var period = RequirePeriod(request.Period);
        var (score, grade, criteria) = ComputeFromCriteria(request.CriteriaScores);

        if (await db.SupplierEvaluations
                .AnyAsync(v => v.Id != id && v.SupplierId == supplier.Id && v.Period == period, ct))
            throw new PurchasingConflictException(
                $"NCC '{supplier.SupplierName}' đã có đánh giá kỳ {period}.");

        evaluation.SupplierId = supplier.Id;
        evaluation.SupplierNameSnapshot = supplier.SupplierName;
        evaluation.Period = period;
        evaluation.Score = score;
        evaluation.Grade = grade;
        evaluation.CriteriaScores = criteria;
        evaluation.EvaluatedAt = request.EvaluatedAt ?? evaluation.EvaluatedAt;
        evaluation.Note = Trim(request.Note);
        evaluation.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToDto(evaluation, supplier.SupplierName);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var evaluation = await db.SupplierEvaluations.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy đánh giá NCC #{id}.");

        evaluation.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static (decimal Score, string Grade, Dictionary<string, int> Criteria) ComputeFromCriteria(
        IReadOnlyDictionary<string, int>? raw)
    {
        if (raw is null || raw.Count == 0)
            throw new PurchasingConflictException("Phải nhập đủ điểm 30 tiêu chí KPI.");

        var copy = raw.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        try
        {
            var result = KpiEvaluationScoring.Compute(copy);
            var grade = SupplierGradeMapper.FromKpiScore(result.OverallScore);
            return (result.OverallScore, grade, copy);
        }
        catch (ArgumentException ex)
        {
            throw new PurchasingConflictException(ex.Message);
        }
    }

    private static SupplierEvaluationDto ToDto(SupplierEvaluation v, string supplierName) =>
        new(
            v.Id,
            v.SupplierId,
            supplierName,
            v.Period,
            v.Score,
            v.Grade,
            SupplierGradeMapper.KpiGradeLabel(v.Grade),
            v.EvaluatedAt,
            v.CriteriaScores ?? new Dictionary<string, int>(StringComparer.Ordinal),
            v.Note,
            v.EvaluatedByName);

    private static string RequirePeriod(string? period)
    {
        var value = (period ?? string.Empty).Trim();
        if (value.Length == 0)
            throw new PurchasingConflictException("Kỳ đánh giá là bắt buộc.");
        if (value.Length > 30)
            throw new PurchasingConflictException("Kỳ đánh giá tối đa 30 ký tự.");
        return value;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
