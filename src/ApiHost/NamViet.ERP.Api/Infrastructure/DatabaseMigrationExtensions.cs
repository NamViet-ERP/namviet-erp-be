using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using Npgsql;

namespace NamViet.ERP.Api.Infrastructure;

public static class DatabaseMigrationExtensions
{
    private const string MigrationAdvisoryLockQuery = "SELECT pg_advisory_lock(hashtext('namviet-erp-database-migrator'));";
    private const string MigrationAdvisoryUnlockQuery = "SELECT pg_advisory_unlock(hashtext('namviet-erp-database-migrator'));";

    public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app, CancellationToken ct = default)
    {
        var autoMigrate = app.Configuration.GetValue("Database:AutoMigrate", true);
        if (!autoMigrate)
        {
            app.Logger.LogInformation("Database auto-migration is disabled (Database:AutoMigrate = false). Skipping migrations.");
            return;
        }

        var connectionString = app.Configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            app.Logger.LogWarning("ConnectionStrings:Default is empty or missing. Skipping database migrations.");
            return;
        }

        app.Logger.LogInformation("Connecting to PostgreSQL database to acquire migration advisory lock...");

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        app.Logger.LogInformation("Acquiring advisory lock for database migrations...");
        await using (var lockCmd = new NpgsqlCommand(MigrationAdvisoryLockQuery, conn))
        {
            await lockCmd.ExecuteNonQueryAsync(ct);
        }
        app.Logger.LogInformation("PostgreSQL migration advisory lock acquired successfully.");

        try
        {
            app.Logger.LogInformation("Starting database migrations across all contexts...");
            await using var scope = app.Services.CreateAsyncScope();

            try
            {
                app.Logger.LogInformation("Applying migrations for IdentityDbContext...");
                var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                await identityDb.Database.MigrateAsync(ct);
                app.Logger.LogInformation("IdentityDbContext migrations applied successfully.");
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Failed to apply migrations for IdentityDbContext: {Message}. Inner: {InnerMessage}",
                    ex.Message, ex.InnerException?.Message);
                throw;
            }

            try
            {
                app.Logger.LogInformation("Applying migrations for WarehouseDbContext...");
                var warehouseDb = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
                await warehouseDb.Database.MigrateAsync(ct);
                app.Logger.LogInformation("WarehouseDbContext migrations applied successfully.");
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Failed to apply migrations for WarehouseDbContext: {Message}. Inner: {InnerMessage}",
                    ex.Message, ex.InnerException?.Message);
                throw;
            }

            try
            {
                app.Logger.LogInformation("Applying migrations for HrDbContext...");
                var hrDb = scope.ServiceProvider.GetRequiredService<HrDbContext>();
                await hrDb.Database.MigrateAsync(ct);
                app.Logger.LogInformation("HrDbContext migrations applied successfully.");
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Failed to apply migrations for HrDbContext: {Message}. Inner: {InnerMessage}",
                    ex.Message, ex.InnerException?.Message);
                throw;
            }

            app.Logger.LogInformation("All database migrations completed successfully.");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Database migration sequence failed: {Message}", ex.Message);
            throw;
        }
        finally
        {
            try
            {
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    await conn.OpenAsync(CancellationToken.None);
                }

                await using var unlockCmd = new NpgsqlCommand(MigrationAdvisoryUnlockQuery, conn);
                await unlockCmd.ExecuteNonQueryAsync(CancellationToken.None);
                app.Logger.LogInformation("PostgreSQL migration advisory lock released successfully.");
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Failed to release PostgreSQL migration advisory lock.");
            }
        }
    }
}
