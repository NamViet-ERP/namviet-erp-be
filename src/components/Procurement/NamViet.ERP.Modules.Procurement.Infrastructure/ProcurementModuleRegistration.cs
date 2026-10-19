using Microsoft.Extensions.DependencyInjection;

namespace NamViet.ERP.Modules.Procurement.Infrastructure;

public static class ProcurementModuleRegistration
{
    public static IServiceCollection AddProcurementModule(this IServiceCollection services)
    {
        return services;
    }
}
