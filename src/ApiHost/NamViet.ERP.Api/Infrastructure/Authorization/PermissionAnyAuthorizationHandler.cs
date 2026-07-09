using Microsoft.AspNetCore.Authorization;
using NamViet.ERP.SharedKernel.Authorization;

namespace NamViet.ERP.Api.Infrastructure.Authorization;

public class PermissionAnyAuthorizationHandler : AuthorizationHandler<PermissionAnyRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionAnyRequirement requirement)
    {
        if (AuthorizationRoleBypass.IsAdmin(context.User)
            || requirement.Permissions.Any(p => context.User.HasClaim(PermissionCatalog.ClaimType, p)))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
