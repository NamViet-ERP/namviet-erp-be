using Microsoft.AspNetCore.Authorization;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.SharedKernel.Authorization;

namespace NamViet.ERP.Api.Infrastructure;

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, PermissionAnyAuthorizationHandler>();

        services.AddAuthorization(options =>
        {
            foreach (var permission in PermissionCatalog.All)
            {
                options.AddPolicy(
                    PermissionPolicies.Name(permission),
                    policy => policy.Requirements.Add(new PermissionRequirement(permission)));
            }

            options.AddPolicy(
                PermissionPolicies.AnyName("hrm.employees.view:all", "hrm.employees.view:own"),
                policy => policy.Requirements.Add(new PermissionAnyRequirement(
                [
                    "hrm.employees.view:all",
                    "hrm.employees.view:own",
                ])));

            options.AddPolicy(
                PermissionPolicies.AnyName("purchasing.suppliers.view", "wms.suppliers.view:all"),
                policy => policy.Requirements.Add(new PermissionAnyRequirement(
                [
                    "purchasing.suppliers.view",
                    "wms.suppliers.view:all",
                ])));
        });

        return services;
    }
}
