using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.SharedKernel.Authorization;

public static class PermissionDataScope
{
    public static bool HasViewAll(ICurrentUser user, string allPermission) =>
        user.HasPermission(allPermission);

    public static bool HasViewOwn(ICurrentUser user, string ownPermission) =>
        user.HasPermission(ownPermission);

    public static bool IsOwnScopeOnly(ICurrentUser user, string allPermission, string ownPermission) =>
        HasViewOwn(user, ownPermission) && !HasViewAll(user, allPermission);
}
