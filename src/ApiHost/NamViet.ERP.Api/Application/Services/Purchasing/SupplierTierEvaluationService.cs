using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public sealed record SubmitTierEvaluationOutcome(
    SupplierTierEvaluationDto? Evaluation,
    bool NotFound = false,
    string? ErrorMessage = null);

public interface ISupplierTierEvaluationService
{
    Task<SourcingKpiSummaryDto> GetSourcingKpiAsync(CancellationToken ct = default);

    Task<SubmitTierEvaluationOutcome> SubmitTierEvaluationAsync(
        int supplierId,
        SubmitTierEvaluationRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<SupplierTierEvaluationDto>?> GetGradeHistoryAsync(
        int supplierId,
        CancellationToken ct = default);
}

public sealed class SupplierTierEvaluationService(
    WarehouseDbContext db,
    ICurrentUser currentUser) : ISupplierTierEvaluationService
{
    public async Task<SourcingKpiSummaryDto> GetSourcingKpiAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalSuppliers = await db.Suppliers.AsNoTracking().CountAsync(ct);
        var totalNewThisMonth = await db.Suppliers.AsNoTracking()
            .CountAsync(s => s.CreatedAt >= startOfMonth, ct);

        var gradeGroups = await db.Suppliers.AsNoTracking()
            .GroupBy(s => s.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var gradeCounts = gradeGroups
            .GroupBy(g => g.Rating ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count), StringComparer.Ordinal);

        var gradeACount = gradeCounts.GetValueOrDefault(SupplierGrades.A, 0);
        var gradeBCount = gradeCounts.GetValueOrDefault(SupplierGrades.B, 0);
        var gradeCCount = gradeCounts.GetValueOrDefault(SupplierGrades.C, 0);
        var gradeDCount = gradeCounts.GetValueOrDefault(SupplierGrades.D, 0);
        var otherCount = totalSuppliers - (gradeACount + gradeBCount + gradeCCount + gradeDCount);

        var promotionsThisMonth = await db.SupplierTierEvaluations
            .AsNoTracking()
            .Where(e => e.CreatedAt >= startOfMonth && e.OldGrade != e.NewGrade)
            .CountAsync(ct);

        var promotionRate = totalNewThisMonth > 0
            ? Math.Round((decimal)promotionsThisMonth / totalNewThisMonth * 100m, 1)
            : 0m;

        return new SourcingKpiSummaryDto(
            totalSuppliers,
            totalNewThisMonth,
            promotionsThisMonth,
            gradeACount,
            gradeBCount,
            gradeCCount,
            gradeDCount,
            otherCount,
            promotionRate);
    }

    public async Task<SubmitTierEvaluationOutcome> SubmitTierEvaluationAsync(
        int supplierId,
        SubmitTierEvaluationRequest request,
        CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId, ct);
        if (supplier == null)
        {
            return new SubmitTierEvaluationOutcome(
                Evaluation: null,
                NotFound: true,
                ErrorMessage: $"Không tìm thấy nhà cung cấp id={supplierId}.");
        }

        var price = Math.Clamp(request.PriceScore, 0m, 50m);
        var profile = Math.Clamp(request.ProfileScore, 0m, 20m);
        var payment = Math.Clamp(request.PaymentScore, 0m, 20m);
        var service = Math.Clamp(request.ServiceScore, 0m, 10m);
        var total = price + profile + payment + service;

        var oldGrade = supplier.Rating;
        string newGrade;

        if (total >= 80m)
        {
            if (!string.IsNullOrWhiteSpace(request.TargetGrade))
            {
                newGrade = request.TargetGrade.Trim().ToUpperInvariant();
            }
            else
            {
                newGrade = oldGrade switch
                {
                    SupplierGrades.D => SupplierGrades.C,
                    SupplierGrades.C => SupplierGrades.B,
                    SupplierGrades.B => SupplierGrades.A,
                    _ => SupplierGrades.C
                };
            }
            supplier.Rating = newGrade;
            supplier.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.TargetGrade)
                && !string.Equals(request.TargetGrade.Trim(), oldGrade, StringComparison.OrdinalIgnoreCase))
            {
                return new SubmitTierEvaluationOutcome(
                    Evaluation: null,
                    NotFound: false,
                    ErrorMessage: $"Tổng điểm đánh giá là {total}/100, chưa đạt mức tối thiểu 80 điểm để thăng hạng lên {request.TargetGrade}.");
            }
            newGrade = oldGrade;
        }

        var eval = new SupplierTierEvaluation
        {
            SupplierId = supplier.Id,
            OldGrade = oldGrade,
            NewGrade = newGrade,
            TotalScore = total,
            PriceScore = price,
            ProfileScore = profile,
            PaymentScore = payment,
            ServiceScore = service,
            EvaluationDetailsJson = request.DetailsJson,
            Reason = request.Reason?.Trim(),
            EvaluatedById = currentUser.Id.ToString(),
            EvaluatedByName = !string.IsNullOrWhiteSpace(currentUser.Name) ? currentUser.Name : currentUser.Email,
            CreatedAt = DateTime.UtcNow
        };

        db.SupplierTierEvaluations.Add(eval);
        await db.SaveChangesAsync(ct);

        var dto = new SupplierTierEvaluationDto(
            eval.Id,
            eval.SupplierId,
            eval.OldGrade,
            eval.NewGrade,
            eval.TotalScore,
            eval.PriceScore,
            eval.ProfileScore,
            eval.PaymentScore,
            eval.ServiceScore,
            eval.EvaluationDetailsJson,
            eval.Reason,
            eval.EvaluatedById,
            eval.EvaluatedByName,
            eval.CreatedAt);

        return new SubmitTierEvaluationOutcome(dto);
    }

    public async Task<IReadOnlyList<SupplierTierEvaluationDto>?> GetGradeHistoryAsync(
        int supplierId,
        CancellationToken ct = default)
    {
        var exists = await db.Suppliers.AnyAsync(s => s.Id == supplierId, ct);
        if (!exists)
            return null;

        return await db.SupplierTierEvaluations
            .AsNoTracking()
            .Where(e => e.SupplierId == supplierId)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new SupplierTierEvaluationDto(
                e.Id,
                e.SupplierId,
                e.OldGrade,
                e.NewGrade,
                e.TotalScore,
                e.PriceScore,
                e.ProfileScore,
                e.PaymentScore,
                e.ServiceScore,
                e.EvaluationDetailsJson,
                e.Reason,
                e.EvaluatedById,
                e.EvaluatedByName,
                e.CreatedAt))
            .ToListAsync(ct);
    }
}
