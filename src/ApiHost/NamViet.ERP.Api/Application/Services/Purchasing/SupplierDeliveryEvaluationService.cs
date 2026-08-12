using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.SupplierEvaluation;
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
        var rows = await query
            .OrderBy(v => v.Status == SupplierDeliveryEvaluationStatuses.Pending ? 0 : 1)
            .ThenBy(v => v.DueAt)
            .ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new
            {
                v.Id,
                v.SupplierId,
                SupplierName = v.Supplier!.SupplierName,
                v.PoCode,
                v.ImportTicketCode,
                v.ProjectCode,
                v.ReceivedAt,
                v.DueAt,
                v.Status,
                v.Quality,
                v.Delivery,
                v.Price,
                v.Service,
                v.Compliance,
                v.Score,
                v.Grade,
                v.Note,
                v.EvaluatedByName,
                v.EvaluatedAt,
            })
            .ToListAsync(ct);

        var items = rows.Select(v => new SupplierDeliveryEvaluationDto(
            v.Id,
            v.SupplierId,
            v.SupplierName,
            v.PoCode,
            v.ImportTicketCode,
            v.ProjectCode,
            v.ReceivedAt,
            v.DueAt,
            v.Status,
            v.Status == SupplierDeliveryEvaluationStatuses.Pending && v.DueAt < now,
            v.Quality,
            v.Delivery,
            v.Price,
            v.Service,
            v.Compliance,
            v.Score,
            v.Grade,
            v.Grade is null ? null : SupplierGradeMapper.OrderGradeLabel(v.Grade),
            v.Note,
            v.EvaluatedByName,
            v.EvaluatedAt)).ToList();

        return new SupplierDeliveryEvaluationPagedResponse(
            items, total, page, pageSize, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task EnsurePendingFromApprovedImportAsync(
        WarehouseTransaction import,
        CancellationToken ct = default)
    {
        // Header SupplierId is often unset on PO imports; fall back to unique line SupplierId.
        if (import.SupplierId is not int supplierId)
        {
            var lineIds = (import.Items ?? [])
                .Where(i => i.SupplierId is int)
                .Select(i => i.SupplierId!.Value)
                .Distinct()
                .ToList();
            if (lineIds.Count != 1)
                return;
            supplierId = lineIds[0];
            import.SupplierId = supplierId;
        }
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
            ProjectCode = string.IsNullOrWhiteSpace(import.ProjectCode) ? null : import.ProjectCode.Trim(),
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

        OrderEvaluationCriteria criteria;
        decimal score;
        try
        {
            criteria = new OrderEvaluationCriteria(
                request.Quality,
                request.Delivery,
                request.Price,
                request.Service,
                request.Compliance);
            score = OrderEvaluationScoring.Compute(criteria);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new PurchasingConflictException(ex.Message);
        }

        var grade = await ResolveGradeAsync(score, ct);

        evaluation.Quality = Math.Round(criteria.Quality, 2);
        evaluation.Delivery = Math.Round(criteria.Delivery, 2);
        evaluation.Price = Math.Round(criteria.Price, 2);
        evaluation.Service = Math.Round(criteria.Service, 2);
        evaluation.Compliance = Math.Round(criteria.Compliance, 2);
        evaluation.Score = score;
        evaluation.Grade = grade;
        evaluation.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        evaluation.Status = SupplierDeliveryEvaluationStatuses.Completed;
        evaluation.EvaluatedAt = request.EvaluatedAt ?? DateOnly.FromDateTime(DateTime.UtcNow);
        evaluation.EvaluatedById = currentUser.IsAuthenticated ? currentUser.Id : null;
        evaluation.EvaluatedByName = currentUser.IsAuthenticated ? currentUser.Name : null;
        evaluation.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await SupplierRatingRecompute.ApplyAsync(db, evaluation.SupplierId, settings, ct);
        await db.SaveChangesAsync(ct);

        return ToDto(evaluation, evaluation.Supplier?.SupplierName ?? evaluation.SupplierNameSnapshot ?? string.Empty);
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

    private static SupplierDeliveryEvaluationDto ToDto(SupplierDeliveryEvaluation v, string supplierName) =>
        new(
            v.Id,
            v.SupplierId,
            supplierName,
            v.PoCode,
            v.ImportTicketCode,
            v.ProjectCode,
            v.ReceivedAt,
            v.DueAt,
            v.Status,
            false,
            v.Quality,
            v.Delivery,
            v.Price,
            v.Service,
            v.Compliance,
            v.Score,
            v.Grade,
            v.Grade is null ? null : SupplierGradeMapper.OrderGradeLabel(v.Grade),
            v.Note,
            v.EvaluatedByName,
            v.EvaluatedAt);
}

/// <summary>Cập nhật Supplier.Rating = avg Score các delivery eval completed.</summary>
internal static class SupplierRatingRecompute
{
    public static async Task ApplyAsync(
        WarehouseDbContext db,
        int supplierId,
        ISupplierModuleSettingsService settings,
        CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId, ct);
        if (supplier is null)
            return;

        var scores = await db.SupplierDeliveryEvaluations
            .Where(v => v.SupplierId == supplierId
                        && v.Status == SupplierDeliveryEvaluationStatuses.Completed
                        && v.Score != null)
            .Select(v => v.Score!.Value)
            .ToListAsync(ct);

        if (scores.Count == 0)
        {
            supplier.Rating = SupplierGrades.D;
            supplier.UpdatedAt = DateTime.UtcNow;
            return;
        }

        var avg = Math.Round(scores.Average(), 2, MidpointRounding.AwayFromZero);
        var config = await settings.GetAsync(ct);
        supplier.Rating = SupplierGrades.FromScore(
            avg,
            config.GradeAMinScore,
            config.GradeBMinScore,
            config.GradeCMinScore,
            config.GradeDMinScore,
            config.GradeEMinScore,
            config.GradeFMinScore,
            config.GradeGMinScore,
            config.GradeHMinScore);
        supplier.UpdatedAt = DateTime.UtcNow;
    }
}
