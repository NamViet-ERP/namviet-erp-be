using Microsoft.Extensions.DependencyInjection;

namespace NamViet.ERP.Modules.CustomerService.Infrastructure;

public static class CustomerServiceModuleRegistration
{
    public static IServiceCollection AddCustomerServiceModule(this IServiceCollection services)
    {
        return services;
    }
}
