using Microsoft.AspNetCore.Authorization;

namespace NamViet.ERP.Api.Infrastructure.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireAnyPermissionAttribute : AuthorizeAttribute
{
    public RequireAnyPermissionAttribute(params string[] permissions)
    {
        Policy = PermissionPolicies.AnyName(permissions);
    }
}
