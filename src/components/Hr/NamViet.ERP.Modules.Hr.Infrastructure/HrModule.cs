using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Modules.Hr.Infrastructure.Services;
using NamViet.ERP.SharedKernel.Interfaces;
using Npgsql;

namespace NamViet.ERP.Modules.Hr.Infrastructure;

public static class HrModule
{
    public static IServiceCollection AddHrModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

        var normalized = NormalizeConnectionString(connectionString);
        services.AddDbContext<HrDbContext>(opt =>
            opt.UseNpgsql(normalized, b =>
            {
                b.MigrationsHistoryTable("__ef_migrations_hr", "hr");
                b.CommandTimeout(60);
            }));

        services.AddScoped<IEmployeeInfoProvider, EmployeeInfoProvider>();
        services.AddScoped<IEmployeeUserLinkService, EmployeeUserLinkService>();

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
