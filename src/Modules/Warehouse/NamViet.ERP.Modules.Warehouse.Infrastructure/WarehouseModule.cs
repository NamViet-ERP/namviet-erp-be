using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Services;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure;

public static class WarehouseModule
{
    public static IServiceCollection AddWarehouseModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

        // Npgsql 8+: Dictionary/POCO jsonb mapping requires explicit dynamic JSON opt-in.
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<WarehouseDbContext>(opt =>
            opt.UseNpgsql(dataSource,
                b => b.MigrationsHistoryTable("__ef_migrations_warehouse", "warehouse")));

        services.AddScoped<IInventoryDomainService, InventoryDomainService>();

        return services;
    }
}
