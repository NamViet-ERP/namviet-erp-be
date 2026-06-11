using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure;

public static class WarehouseModule
{
    public static IServiceCollection AddWarehouseModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

        services.AddDbContext<WarehouseDbContext>(opt =>
            opt.UseNpgsql(connectionString,
                b => b.MigrationsHistoryTable("__ef_migrations_warehouse", "warehouse")));

        return services;
    }
}
