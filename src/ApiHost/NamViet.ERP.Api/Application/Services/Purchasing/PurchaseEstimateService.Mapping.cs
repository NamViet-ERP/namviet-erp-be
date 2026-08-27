using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.SharedKernel.Authorization;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public sealed partial class PurchaseEstimateService
{
    private async Task<PurchaseEstimateDetailDto> MapDetailAsync(
        PurchaseEstimate header, int? version, CancellationToken ct)
    {
        var versions = await db.PurchaseEstimateVersions.AsNoTracking()
            .Where(v => v.EstimateId == header.Id)
            .OrderBy(v => v.VersionNumber)
            .Select(v => new PurchaseEstimateVersionSummaryDto(v.Id, v.VersionNumber, v.CreatedAt, v.UpdatedByName))
            .ToListAsync(ct);

        var viewed = version is > 0 ? version.Value : header.CurrentVersion;
        var ver = await db.PurchaseEstimateVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.EstimateId == header.Id && v.VersionNumber == viewed, ct);
        var lines = ver is null
            ? []
            : await db.PurchaseEstimateLines.AsNoTracking()
                .Where(l => l.VersionId == ver.Id)
                .OrderBy(l => l.CategoryName).ThenBy(l => l.MaterialName)
                .Select(l => new PurchaseEstimateLineDto(
                    l.Id, l.MaterialCode, l.MaterialName, l.CategoryName, l.Unit, l.OldQty, l.NewQty, l.EstimateUnitPrice))
                .ToListAsync(ct);

        var proposals = await db.PurchaseEstimateProposals.AsNoTracking()
            .Where(p => p.EstimateId == header.Id)
            .OrderBy(p => p.Name)
            .Select(p => new PurchaseEstimateProposalDto(p.Id, p.Name, p.Spec, p.Unit, p.Qty, p.Note))
            .ToListAsync(ct);
        var approvals = await db.PurchaseEstimateApprovals.AsNoTracking()
            .Where(a => a.EstimateId == header.Id)
            .OrderBy(a => a.Level)
            .Select(a => new PurchaseEstimateApprovalDto(
                a.Id, a.Level, a.Name, a.Status, a.ActionByName, a.ActionAt, a.Note))
            .ToListAsync(ct);

        return new PurchaseEstimateDetailDto(
            header.Id, header.Code, header.ProjectCode, header.Title, header.Status,
            header.CurrentVersion, viewed, null, header.ShareTokenExpiresAt,
            header.PreparerName, header.PreparerDate, header.ReviewedByName, header.ReviewedAt,
            header.ConvertedPlanCode,
            header.CreatedAt, header.CreatedByName, header.UpdatedAt, header.UpdatedByName,
            versions, lines, proposals, approvals);
    }

    private async Task<PublicPurchaseEstimateDto> MapPublicAsync(PurchaseEstimate header, CancellationToken ct)
    {
        var projectName = await db.Projects.AsNoTracking()
            .Where(p => p.ProjectCode == header.ProjectCode)
            .Select(p => p.ProjectName)
            .FirstOrDefaultAsync(ct);
        var ver = await db.PurchaseEstimateVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.EstimateId == header.Id && v.VersionNumber == header.CurrentVersion, ct);
        var lines = ver is null
            ? []
            : await db.PurchaseEstimateLines.AsNoTracking()
                .Where(l => l.VersionId == ver.Id)
                .OrderBy(l => l.CategoryName).ThenBy(l => l.MaterialName)
                .Select(l => new PublicPurchaseEstimateLineDto(
                    l.MaterialCode, l.MaterialName, l.CategoryName, l.Unit, l.NewQty))
                .ToListAsync(ct);
        var proposals = await db.PurchaseEstimateProposals.AsNoTracking()
            .Where(p => p.EstimateId == header.Id)
            .Select(p => new PurchaseEstimateProposalDto(p.Id, p.Name, p.Spec, p.Unit, p.Qty, p.Note))
            .ToListAsync(ct);
        /* Link chia sẻ chỉ để xem Ver mới nhất — không mở form điền công khai */
        var canFill = false;
        var preparers = Array.Empty<PublicPurchaseEstimatePreparerDto>();
        return new PublicPurchaseEstimateDto(
            header.Code, header.Title, header.ProjectCode, projectName, header.Status,
            canFill,
            header.PreparerName, header.PreparerDate, lines, proposals, preparers);
    }

    private async Task<IReadOnlyList<PublicPurchaseEstimatePreparerDto>> ListPreparersAsync(CancellationToken ct)
    {
        var roleNames = new[] { RolePermissionTemplates.ProjectRole, RolePermissionTemplates.ProductionRole };
        var rows = await (
            from u in identity.Users.AsNoTracking()
            join ur in identity.Set<IdentityUserRole<Guid>>().AsNoTracking() on u.Id equals ur.UserId
            join r in identity.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where u.DeletedAt == null && u.IsActive
                  && r.Name != null && roleNames.Contains(r.Name)
            orderby r.Name, u.Name
            select new { u.Id, u.Name, Role = r.Name! }
        ).ToListAsync(ct);

        return rows
            .Select(x => new PublicPurchaseEstimatePreparerDto(
                x.Id, x.Name, x.Role, WmsMapper.ToClientRoleLabel(x.Role)))
            .ToList();
    }

    private static void EnsureShareTokenAlive(PurchaseEstimate header)
    {
        if (header.ShareTokenExpiresAt is null || header.ShareTokenExpiresAt <= DateTime.UtcNow)
            throw new PurchasingConflictException("Link điền đã hết hạn. Liên hệ phòng Cung ứng để cấp lại.");
    }

    private static string FormatPreparerLabel(PublicPurchaseEstimatePreparerDto p) =>
        $"{p.Name} - {p.RoleLabel}";

    private async Task<int> NextEstimateSequenceAsync(CancellationToken ct)
    {
        var codes = await db.PurchaseEstimates.IgnoreQueryFilters()
            .Select(e => e.Code)
            .ToListAsync(ct);
        var next = 1;
        foreach (var code in codes)
        {
            if (code.Length > 3
                && code.StartsWith("DT-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(code.AsSpan(3), out var n))
                next = Math.Max(next, n + 1);
        }
        return next;
    }

    private static PurchaseEstimateVersion NewVersion(
        Guid estimateId, int number, Guid userId, string userName, DateTime now) =>
        new()
        {
            EstimateId = estimateId,
            VersionNumber = number,
            UpdatedById = userId,
            UpdatedByName = userName,
            CreatedAt = now,
            UpdatedAt = now,
        };

    private void Touch(PurchaseEstimate header)
    {
        var (userId, userName) = Actor();
        header.UpdatedById = userId;
        header.UpdatedByName = userName;
        header.UpdatedAt = DateTime.UtcNow;
    }

    private (Guid Id, string Name) Actor()
    {
        var name = string.IsNullOrWhiteSpace(currentUser.Name) ? currentUser.Email : currentUser.Name;
        if (string.IsNullOrWhiteSpace(name)) name = "Hệ thống";
        return (currentUser.IsAuthenticated ? currentUser.Id : Guid.Empty, name);
    }
}
