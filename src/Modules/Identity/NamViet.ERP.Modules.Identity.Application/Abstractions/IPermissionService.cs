using NamViet.ERP.Modules.Identity.Domain.Entities;

namespace NamViet.ERP.Modules.Identity.Application.Abstractions;

public interface IPermissionService
{
    Task<IReadOnlyList<string>> GetPermissionsForUserAsync(ApplicationUser user, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(ApplicationRole role, CancellationToken ct = default);
    Task SetRolePermissionsAsync(ApplicationRole role, IReadOnlyList<string> permissions, CancellationToken ct = default);
    IReadOnlyList<string> ValidatePermissions(IReadOnlyList<string> permissions);
}
