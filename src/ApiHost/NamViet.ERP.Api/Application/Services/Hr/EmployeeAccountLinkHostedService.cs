namespace NamViet.ERP.Api.Application.Services.Hr;

/// <summary>
/// One-shot repair at process start so screens that only read <c>employees.UserId</c> see existing logins.
/// </summary>
public sealed class EmployeeAccountLinkHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<EmployeeAccountLinkHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sync = scope.ServiceProvider.GetRequiredService<IEmployeeAccountLinkSynchronizer>();
            var attached = await sync.SynchronizeAsync(cancellationToken);
            if (attached > 0)
                logger.LogInformation("Attached {Count} employee records to existing login accounts.", attached);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to synchronize employee login links.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
