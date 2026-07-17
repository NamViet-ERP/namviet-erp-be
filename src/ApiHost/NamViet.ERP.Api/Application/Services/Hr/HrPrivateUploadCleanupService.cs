using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Hr;

public sealed class HrPrivateUploadCleanupService(
    IServiceScopeFactory scopeFactory,
    IFileStorage storage,
    ILogger<HrPrivateUploadCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var references = await LoadReferencesAsync(stoppingToken);
                var deleted = await storage.DeleteUnreferencedOlderThanAsync(
                    references, DateTime.UtcNow - Retention, stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Deleted {Count} orphaned private HR uploads.", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Private HR upload cleanup failed.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task<IReadOnlySet<string>> LoadReferencesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        var candidates = await db.Candidates.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.CvFileUrl != null)
            .Select(c => c.CvFileUrl!)
            .ToListAsync(ct);
        var employees = await db.Employees.IgnoreQueryFilters().AsNoTracking()
            .Select(e => new { e.PhotoUrl, e.CccdFrontUrl, e.CccdBackUrl, e.DegreePhotoUrl })
            .ToListAsync(ct);

        var references = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in candidates)
            Add(key);
        foreach (var employee in employees)
        {
            Add(employee.PhotoUrl);
            Add(employee.CccdFrontUrl);
            Add(employee.CccdBackUrl);
            Add(employee.DegreePhotoUrl);
        }
        return references;

        void Add(string? key)
        {
            if (!string.IsNullOrWhiteSpace(key))
                references.Add(HrStorageKeyPolicy.Normalize(key));
        }
    }
}
