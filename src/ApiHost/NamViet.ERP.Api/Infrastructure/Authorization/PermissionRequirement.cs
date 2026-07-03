using Microsoft.AspNetCore.Authorization;

namespace NamViet.ERP.Api.Infrastructure.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
