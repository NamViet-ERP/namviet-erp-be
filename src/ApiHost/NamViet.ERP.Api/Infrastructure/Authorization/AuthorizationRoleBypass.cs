using System.Security.Claims;
using NamViet.ERP.SharedKernel.Authorization;

namespace NamViet.ERP.Api.Infrastructure.Authorization;

internal static class AuthorizationRoleBypass
{
    internal static bool IsAdmin(ClaimsPrincipal user) =>
        user.IsInRole(RolePermissionTemplates.AdminRole);
}
