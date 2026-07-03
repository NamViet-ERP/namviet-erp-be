using Microsoft.AspNetCore.Authorization;

namespace NamViet.ERP.Api.Infrastructure.Authorization;

public sealed class PermissionAnyRequirement(IReadOnlyList<string> permissions) : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; } = permissions;
}
