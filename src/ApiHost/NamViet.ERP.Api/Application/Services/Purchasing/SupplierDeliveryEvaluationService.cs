using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface ISupplierDeliveryEvaluationService
{
    Task<SupplierDeliveryEvaluationPagedResponse> ListAsync(
        int? supplierId,
        string? poCode,
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task EnsurePendingFromApprovedImportAsync(
        WarehouseTransaction import,
        CancellationToken ct = default);

    Task<SupplierDeliveryEvaluationDto> CompleteAsync(
        int id,
        CompleteSupplierDeliveryEvaluationRequest request,
        CancellationToken ct = default);
}

public sealed class SupplierDeliveryEvaluationService(
    WarehouseDbContext db,
    ISupplierModuleSettingsService settings,
    ICurrentUser currentUser) : ISupplierDeliveryEvaluationService
{
    private static readonly TimeSpan EvaluationWindow = TimeSpan.FromDays(30);

    public async Task<SupplierDeliveryEvaluationPagedResponse> ListAsync(
        int? supplierId,
        string? poCode,
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.SupplierDeliveryEvaluations.AsNoTracking()
            .Where(v => v.Supplier != null);

        if (supplierId is int sid)
            query = query.Where(v => v.SupplierId == sid);
        if (!string.IsNullOrWhiteSpace(poCode))
        {
            var code = poCode.Trim();
            query = query.Where(v => v.PoCode == code);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim();
            query = query.Where(v => v.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(v =>
                EF.Functions.ILike(v.Supplier!.SupplierName, $"%{q}%")
                || EF.Functions.ILike(v.PoCode, $"%{q}%")
                || EF.Functions.ILike(v.ImportTicketCode, $"%{q}%")
                || (v.Note != null && EF.Functions.ILike(v.Note, $"%{q}%")));
        }

        var total = await query.CountAsync(ct);
        var now = DateTime.UtcNow;
        var items = await query
            .OrderBy(v => v.Status == SupplierDeliveryEvaluationStatuses.Pending ? 0 : 1)
            .ThenBy(v => v.DueAt)
            .ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new SupplierDeliveryEvaluationDto(
                v.Id,
                v.SupplierId,
                v.Supplier!.SupplierName,
                v.PoCode,
                v.ImportTicketCode,
                v.ReceivedAt,
                v.DueAt,
                v.Status,
                v.Status == SupplierDeliveryEvaluationStatuses.Pending && v.DueAt < now,
                v.Score,
                v.Grade,
                v.OnTimeDeliveryRate,
                v.DefectRate,
                v.WarrantyCompletionRate,
                v.Note,
                v.EvaluatedByName,
                v.EvaluatedAt))
            .ToListAsync(ct);

        return new SupplierDeliveryEvaluationPagedResponse(
            items, total, page, pageSize, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task EnsurePendingFromApprovedImportAsync(
        WarehouseTransaction import,
        CancellationToken ct = default)
    {
        if (import.SupplierId is not int supplierId)
            return;
        if (string.IsNullOrWhiteSpace(import.ReferenceCode))
            return;

        var poCode = import.ReferenceCode.Trim();
        var ticket = import.TicketCode.Trim();
        if (string.IsNullOrWhiteSpace(ticket))
            return;

        var exists = await db.SupplierDeliveryEvaluations
            .AnyAsync(v => v.ImportTicketCode == ticket, ct);
        if (exists)
            return;

        var supplier = await db.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == supplierId, ct);
        if (supplier is null)
            return;

        var receivedAt = import.ApprovedAt ?? DateTime.UtcNow;
        db.SupplierDeliveryEvaluations.Add(new SupplierDeliveryEvaluation
        {
            SupplierId = supplierId,
            SupplierNameSnapshot = supplier.SupplierName,
            PoCode = poCode,
            ImportTicketCode = ticket,
            ReceivedAt = receivedAt,
            DueAt = receivedAt.Add(EvaluationWindow),
            Status = SupplierDeliveryEvaluationStatuses.Pending,
        });
        // Caller owns SaveChanges (inside import approve transaction).
    }

    public async Task<SupplierDeliveryEvaluationDto> CompleteAsync(
        int id,
        CompleteSupplierDeliveryEvaluationRequest request,
        CancellationToken ct = default)
    {
        var evaluation = await db.SupplierDeliveryEvaluations
            .Include(v => v.Supplier)
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy đánh giá nhận hàng #{id}.");

        if (!string.Equals(
                evaluation.Status,
                SupplierDeliveryEvaluationStatuses.Pending,
                StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Đánh giá này đã hoàn thành.");

        var score = RequireScore(request.Score);
        var onTime = RequireRate(request.OnTimeDeliveryRate, "Tỷ lệ giao hàng đúng hạn");
        var defect = RequireRate(request.DefectRate, "Tỷ lệ hàng lỗi");
        var warranty = RequireRate(request.WarrantyCompletionRate, "Tỷ lệ hoàn thành bảo hành");
        var grade = await ResolveGradeAsync(score, ct);

        evaluation.Score = score;
        evaluation.Grade = grade;
        evaluation.OnTimeDeliveryRate = onTime;
        evaluation.DefectRate = defect;
        evaluation.WarrantyCompletionRate = warranty;
        evaluation.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        evaluation.Status = SupplierDeliveryEvaluationStatuses.Completed;
        evaluation.EvaluatedAt = request.EvaluatedAt ?? DateOnly.FromDateTime(DateTime.UtcNow);
        evaluation.EvaluatedById = currentUser.IsAuthenticated ? currentUser.Id : null;
        evaluation.EvaluatedByName = currentUser.IsAuthenticated ? currentUser.Name : null;
        evaluation.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return new SupplierDeliveryEvaluationDto(
            evaluation.Id,
            evaluation.SupplierId,
            evaluation.Supplier?.SupplierName ?? evaluation.SupplierNameSnapshot ?? string.Empty,
            evaluation.PoCode,
            evaluation.ImportTicketCode,
            evaluation.ReceivedAt,
            evaluation.DueAt,
            evaluation.Status,
            false,
            evaluation.Score,
            evaluation.Grade,
            evaluation.OnTimeDeliveryRate,
            evaluation.DefectRate,
            evaluation.WarrantyCompletionRate,
            evaluation.Note,
            evaluation.EvaluatedByName,
            evaluation.EvaluatedAt);
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
}
