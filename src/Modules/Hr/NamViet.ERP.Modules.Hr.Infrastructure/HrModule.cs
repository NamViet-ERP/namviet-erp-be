using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Modules.Hr.Infrastructure;

public static class HrModule
{
    public static IServiceCollection AddHrModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

        services.AddDbContext<HrDbContext>(opt =>
            opt.UseNpgsql(connectionString,
                b => b.MigrationsHistoryTable("__ef_migrations_hr", "hr")));

        return services;
    }
}
