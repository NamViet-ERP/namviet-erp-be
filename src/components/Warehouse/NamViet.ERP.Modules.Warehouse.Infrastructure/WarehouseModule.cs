using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Services;
using Npgsql;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure;

public static class WarehouseModule
{
    public static IServiceCollection AddWarehouseModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

        var normalized = NormalizeConnectionString(connectionString);

        // Npgsql 8+: Dictionary/POCO jsonb mapping requires explicit dynamic JSON opt-in.
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(normalized);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();
        services.AddSingleton(dataSource);

        services.AddDbContext<WarehouseDbContext>(opt =>
            opt.UseNpgsql(dataSource, b =>
            {
                b.MigrationsHistoryTable("__ef_migrations_warehouse", "warehouse");
                b.CommandTimeout(60);
            }));

        services.AddScoped<IInventoryDomainService, InventoryDomainService>();

        return services;
    }

    private static string NormalizeConnectionString(string connectionString)
    {
        try
        {
            var csb = new NpgsqlConnectionStringBuilder(connectionString);
            if (csb.KeepAlive == 0)
                csb.KeepAlive = 30;
            return csb.ConnectionString;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return connectionString;
        }
    }
}
