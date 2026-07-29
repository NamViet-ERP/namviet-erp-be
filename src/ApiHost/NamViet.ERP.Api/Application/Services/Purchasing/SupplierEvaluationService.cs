using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface ISupplierEvaluationService
{
    Task<SupplierEvaluationPagedResponse> ListAsync(
        int? supplierId, string? period, string? search, int page, int pageSize,
        CancellationToken ct = default);

    Task<SupplierEvaluationDto> CreateAsync(
        UpsertSupplierEvaluationRequest request, CancellationToken ct = default);

    Task<SupplierEvaluationDto> UpdateAsync(
        int id, UpsertSupplierEvaluationRequest request, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class SupplierEvaluationService(
    WarehouseDbContext db,
    ISupplierModuleSettingsService settings,
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
        var items = await query
            .OrderByDescending(v => v.EvaluatedAt)
            .ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new SupplierEvaluationDto(
                v.Id, v.SupplierId, v.Supplier!.SupplierName, v.Period,
                v.Score, v.Grade, v.EvaluatedAt,
                v.OnTimeDeliveryRate, v.DefectRate, v.WarrantyCompletionRate,
                v.Note, v.EvaluatedByName))
            .ToListAsync(ct);

        return new SupplierEvaluationPagedResponse(
            items, total, page, pageSize, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<SupplierEvaluationDto> CreateAsync(
        UpsertSupplierEvaluationRequest request, CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy nhà cung cấp #{request.SupplierId}.");

        var period = RequirePeriod(request.Period);
        var score = RequireScore(request.Score);
        var onTimeDeliveryRate = RequireRate(request.OnTimeDeliveryRate, "Tỷ lệ giao hàng đúng hạn");
        var defectRate = RequireRate(request.DefectRate, "Tỷ lệ hàng lỗi");
        var warrantyCompletionRate = RequireRate(request.WarrantyCompletionRate, "Tỷ lệ hoàn thành bảo hành");

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
            Grade = await ResolveGradeAsync(score, ct),
            EvaluatedAt = request.EvaluatedAt ?? DateOnly.FromDateTime(DateTime.UtcNow),
            OnTimeDeliveryRate = onTimeDeliveryRate,
            DefectRate = defectRate,
            WarrantyCompletionRate = warrantyCompletionRate,
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
        var score = RequireScore(request.Score);
        var onTimeDeliveryRate = RequireRate(request.OnTimeDeliveryRate, "Tỷ lệ giao hàng đúng hạn");
        var defectRate = RequireRate(request.DefectRate, "Tỷ lệ hàng lỗi");
        var warrantyCompletionRate = RequireRate(request.WarrantyCompletionRate, "Tỷ lệ hoàn thành bảo hành");

        if (await db.SupplierEvaluations
                .AnyAsync(v => v.Id != id && v.SupplierId == supplier.Id && v.Period == period, ct))
            throw new PurchasingConflictException(
                $"NCC '{supplier.SupplierName}' đã có đánh giá kỳ {period}.");

        evaluation.SupplierId = supplier.Id;
        evaluation.SupplierNameSnapshot = supplier.SupplierName;
        evaluation.Period = period;
        evaluation.Score = score;
        evaluation.Grade = await ResolveGradeAsync(score, ct);
        evaluation.EvaluatedAt = request.EvaluatedAt ?? evaluation.EvaluatedAt;
        evaluation.OnTimeDeliveryRate = onTimeDeliveryRate;
        evaluation.DefectRate = defectRate;
        evaluation.WarrantyCompletionRate = warrantyCompletionRate;
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

    private async Task<string> ResolveGradeAsync(decimal score, CancellationToken ct)
    {
        var config = await settings.GetAsync(ct);
        return SupplierGrades.FromScore(
            score,
            config.GradeAMinScore,
            config.GradeBMinScore,
            config.GradeCMinScore,
            config.GradeDMinScore,
            config.GradeEMinScore,
            config.GradeFMinScore,
            config.GradeGMinScore,
            config.GradeHMinScore);
    }

    private static SupplierEvaluationDto ToDto(SupplierEvaluation v, string supplierName) =>
        new(v.Id, v.SupplierId, supplierName, v.Period,
            v.Score, v.Grade, v.EvaluatedAt,
            v.OnTimeDeliveryRate, v.DefectRate, v.WarrantyCompletionRate,
            v.Note, v.EvaluatedByName);

    private static string RequirePeriod(string? period)
    {
        var value = (period ?? string.Empty).Trim();
        if (value.Length == 0)
            throw new PurchasingConflictException("Kỳ đánh giá là bắt buộc.");
        if (value.Length > 30)
            throw new PurchasingConflictException("Kỳ đánh giá tối đa 30 ký tự.");
        return value;
    }

    private static decimal RequireScore(decimal score)
    {
        if (score is < 0 or > 100)
            throw new PurchasingConflictException("Điểm đánh giá phải nằm trong khoảng 0–100.");
        return Math.Round(score, 2);
    }

    private static decimal RequireRate(decimal rate, string fieldName)
    {
        if (rate is < 0 or > 100)
            throw new PurchasingConflictException($"{fieldName} phải nằm trong khoảng 0–100%.");
        return Math.Round(rate, 2);
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
