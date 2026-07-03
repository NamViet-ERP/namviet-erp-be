using Microsoft.AspNetCore.Authorization;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Infrastructure.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        Permission = permission;
        Policy = PermissionPolicies.Name(permission);
    }

    public string Permission { get; }
}
