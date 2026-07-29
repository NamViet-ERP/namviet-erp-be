using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace NamViet.ERP.Api.Infrastructure.Health;

/// <summary>Readiness: PostgreSQL reachable via ConnectionStrings:Default.</summary>
public sealed class PostgresReadyHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var cs = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            return HealthCheckResult.Unhealthy("ConnectionStrings:Default is missing.");

        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            _ = await cmd.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL OK");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL unreachable", ex);
        }
    }
}
