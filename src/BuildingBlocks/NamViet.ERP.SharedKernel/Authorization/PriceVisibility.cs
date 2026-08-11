using System.Security.Claims;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.SharedKernel.Authorization;

/// <summary>
/// Single rule for "may this user see money on warehouse screens".
/// Thủ kho (<see cref="RolePermissionTemplates.WarehouseRole"/>) không bao giờ thấy
/// đơn giá / thành tiền / giá trị tồn — kể cả khi claim bị gán nhầm.
/// Every controller and service must go through here — do not re-derive the rule.
/// </summary>
public static class PriceVisibility
{
    public const string Permission = "wms.prices.view";

    public static bool CanViewPrices(ICurrentUser user)
    {
        if (user.IsInRole(RolePermissionTemplates.AdminRole))
            return true;
        if (user.IsInRole(RolePermissionTemplates.WarehouseRole))
            return false;
        return user.HasPermission(Permission);
    }

    public static bool CanViewPrices(ClaimsPrincipal? user)
    {
        if (user is null)
            return false;
        if (user.IsInRole(RolePermissionTemplates.AdminRole))
            return true;
        if (user.IsInRole(RolePermissionTemplates.WarehouseRole))
            return false;
        return user.HasClaim(PermissionCatalog.ClaimType, Permission);
    }

    /// <summary>Zero out a money value the caller is not allowed to read.</summary>
    public static decimal Mask(bool canViewPrices, decimal value) => canViewPrices ? value : 0m;
}
