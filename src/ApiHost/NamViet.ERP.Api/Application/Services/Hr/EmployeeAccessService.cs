using NamViet.ERP.SharedKernel.Authorization;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Hr;

public sealed class EmployeeAccessService(IEmployeeInfoProvider employeeInfo) : IEmployeeAccessService
{
    public const string ViewAllPermission = "hrm.employees.view:all";
    public const string ViewOwnPermission = "hrm.employees.view:own";

    public async Task<int?> ResolveLinkedEmployeeIdAsync(ICurrentUser user, CancellationToken ct = default)
    {
        if (user.EmployeeId is int claimId)
            return claimId;

        if (!user.IsAuthenticated || user.Id == Guid.Empty)
            return null;

        var linked = await employeeInfo.GetByUserAsync(user.Id, user.EmployeeCode, ct);
        return linked?.EmployeeId;
    }

    public async Task<bool> CanViewEmployeeAsync(ICurrentUser user, int employeeId, CancellationToken ct = default)
    {
        if (PermissionDataScope.HasViewAll(user, ViewAllPermission))
            return true;

        if (!PermissionDataScope.HasViewOwn(user, ViewOwnPermission))
            return false;

        var ownId = await ResolveLinkedEmployeeIdAsync(user, ct);
        return ownId.HasValue && ownId.Value == employeeId;
    }
}
