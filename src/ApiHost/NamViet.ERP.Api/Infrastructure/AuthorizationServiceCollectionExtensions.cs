using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.SharedKernel.Authorization;

namespace NamViet.ERP.Api.Infrastructure;

public sealed class CustomAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var policy = await base.GetPolicyAsync(policyName);
        if (policy != null) return policy;

        if (policyName.StartsWith(PermissionPolicies.AnyPrefix, StringComparison.Ordinal))
        {
            var raw = policyName[PermissionPolicies.AnyPrefix.Length..];
            var perms = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionAnyRequirement(perms))
                .Build();
        }

        if (policyName.StartsWith(PermissionPolicies.Prefix, StringComparison.Ordinal))
        {
            var perm = policyName[PermissionPolicies.Prefix.Length..];
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(perm))
                .Build();
        }

        return null;
    }
}

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, PermissionAnyAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, CustomAuthorizationPolicyProvider>();

        services.AddAuthorization(options =>
        {
            foreach (var permission in PermissionCatalog.All)
            {
                options.AddPolicy(
                    PermissionPolicies.Name(permission),
                    policy => policy.Requirements.Add(new PermissionRequirement(permission)));
            }
        });

        return services;
    }
}

