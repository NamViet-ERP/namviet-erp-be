using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NamViet.ERP.Api.Infrastructure.Health;

/// <summary>
/// Readiness: private upload root exists and is writable
/// (same resolution rules as <see cref="Services.Storage.LocalFileStorage"/>).
/// </summary>
public sealed class FileStorageReadyHealthCheck(
    IWebHostEnvironment env,
    IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var root = ResolveRoot();
            Directory.CreateDirectory(root);

            var probe = Path.Combine(root, $".health-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);

            return Task.FromResult(HealthCheckResult.Healthy($"Storage OK: {root}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("File storage not writable", ex));
        }
    }

    private string ResolveRoot()
    {
        var configured = configuration["FileStorage:Root"];
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        var artifacts = FindRepoRelative(
            env.ContentRootPath,
            Path.Combine("artifacts", "api-run", "App_Data", "private-uploads"));
        if (artifacts is not null)
            return artifacts;

        return Path.GetFullPath(Path.Combine(env.ContentRootPath, "App_Data", "private-uploads"));
    }

    private static string? FindRepoRelative(string start, string relative)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            var candidate = Path.GetFullPath(Path.Combine(dir.FullName, relative));
            if (Directory.Exists(Path.GetDirectoryName(candidate)!) || dir.GetFiles("*.sln").Length > 0)
            {
                if (File.Exists(Path.Combine(dir.FullName, "NamViet.ERP.sln"))
                    || File.Exists(Path.Combine(dir.FullName, "docker-compose.yml")))
                    return Path.GetFullPath(Path.Combine(dir.FullName, relative));
            }
            dir = dir.Parent;
        }
        return null;
    }
}
