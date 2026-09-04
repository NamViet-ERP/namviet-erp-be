using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Authorization;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Wms;

/// <summary>
/// Admin / PM: không giới hạn dự án.
/// Thủ kho: chỉ thao tác (tạo/sửa phiếu) trên dự án được gán trong project_warehouse_keepers.
/// Role khác: không áp dụng scope (chỉ dựa vào permission).
/// </summary>
public sealed class ProjectWarehouseAccessService(
    WarehouseDbContext db,
    ICurrentUser currentUser)
{
    public bool IsUnrestricted =>
        currentUser.IsInRole(RolePermissionTemplates.AdminRole)
        || currentUser.IsInRole(RolePermissionTemplates.PmRole);

    public bool IsWarehouseKeeper =>
        currentUser.IsInRole(RolePermissionTemplates.WarehouseRole);

    public async Task<bool> CanOperateOnProjectAsync(string projectCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectCode)) return false;
        if (IsUnrestricted) return true;
        if (!IsWarehouseKeeper) return true;

        return await db.ProjectWarehouseKeepers.AsNoTracking()
            .AnyAsync(k => k.ProjectCode == projectCode && k.UserId == currentUser.Id, ct);
    }

    public async Task EnsureCanOperateOnProjectAsync(string projectCode, CancellationToken ct = default)
    {
        if (await CanOperateOnProjectAsync(projectCode, ct)) return;
        throw new ForbiddenException(
            $"Bạn không được gán phụ trách kho dự án '{projectCode}'.");
    }

    /// <summary>null = không lọc; empty = thủ kho chưa được gán dự án nào.</summary>
    public async Task<HashSet<string>?> GetScopedProjectCodesOrNullAsync(CancellationToken ct = default)
    {
        if (IsUnrestricted || !IsWarehouseKeeper) return null;

        var codes = await db.ProjectWarehouseKeepers.AsNoTracking()
            .Where(k => k.UserId == currentUser.Id)
            .Select(k => k.ProjectCode)
            .ToListAsync(ct);
        return codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
