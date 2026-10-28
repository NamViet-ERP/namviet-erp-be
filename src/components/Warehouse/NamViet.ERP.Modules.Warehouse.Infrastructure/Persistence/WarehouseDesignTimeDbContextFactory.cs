using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

/// <summary>Scaffold migrations without starting the API or connecting to a database.</summary>
public sealed class WarehouseDesignTimeDbContextFactory : IDesignTimeDbContextFactory<WarehouseDbContext>
{
    public WarehouseDbContext CreateDbContext(string[] args)
    {
        // Port 1 deliberately cannot target a working local database. Executing migrations
        // requires an explicit connection; scaffolding and SQL generation do not connect.
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=127.0.0.1;Port=1;Database=migration_design;Username=design";
        return new WarehouseDbContext(new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseNpgsql(connection, options => options.MigrationsHistoryTable("__ef_migrations_warehouse", "warehouse"))
            .Options);
    }
}
