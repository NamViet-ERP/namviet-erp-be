using Microsoft.AspNetCore.Identity;
using NamViet.ERP.Modules.Identity.Application.Abstractions;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.SharedKernel.Authorization;
using System.Security.Claims;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Services;

public class PermissionService(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager) : IPermissionService
{
    public async Task<IReadOnlyList<string>> GetPermissionsForUserAsync(
        ApplicationUser user,
        CancellationToken ct = default)
    {
        var roleNames = await userManager.GetRolesAsync(user);
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var roleName in roleNames)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null) continue;

            foreach (var permission in await GetPermissionsForRoleAsync(role, ct))
                permissions.Add(permission);
        }

        return permissions.OrderBy(p => p, StringComparer.Ordinal).ToList();
    }

    public async Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(
        ApplicationRole role,
        CancellationToken ct = default)
    {
        if (string.Equals(role.Name, RolePermissionTemplates.AdminRole, StringComparison.OrdinalIgnoreCase))
        {
            return PermissionCatalog.All.OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        var claims = await roleManager.GetClaimsAsync(role);
        var fromClaims = claims
            .Where(c => c.Type == PermissionCatalog.ClaimType)
            .Select(c => c.Value)
            .Where(PermissionCatalog.IsValid)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (fromClaims.Count > 0)
            return fromClaims;

        return RolePermissionTemplates.ForIdentityRole(role.Name);
    }

    public async Task SetRolePermissionsAsync(
        ApplicationRole role,
        IReadOnlyList<string> permissions,
        CancellationToken ct = default)
    {
        var validated = ValidatePermissions(permissions);
        var existing = await roleManager.GetClaimsAsync(role);

        foreach (var claim in existing.Where(c => c.Type == PermissionCatalog.ClaimType))
            await roleManager.RemoveClaimAsync(role, claim);

        foreach (var permission in validated)
            await roleManager.AddClaimAsync(role, new Claim(PermissionCatalog.ClaimType, permission));
    }

    public IReadOnlyList<string> ValidatePermissions(IReadOnlyList<string> permissions)
    {
        var invalid = permissions.Where(p => !PermissionCatalog.IsValid(p)).Distinct().ToList();
        if (invalid.Count > 0)
            throw new ArgumentException($"Permission không hợp lệ: {string.Join(", ", invalid)}");

        return permissions.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToList();
    }
}
