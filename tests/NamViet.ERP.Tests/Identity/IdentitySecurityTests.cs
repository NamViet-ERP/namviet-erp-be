using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NamViet.ERP.Modules.Identity.Application.Abstractions;
using NamViet.ERP.Modules.Identity.Application.Commands;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Identity.Infrastructure.Handlers;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Services;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Tests.Identity;

public sealed class IdentitySecurityTests
{
    [Fact]
    public void Refresh_token_hash_is_deterministic_and_not_plaintext()
    {
        var hasher = new RefreshTokenHasher();

        var first = hasher.Hash("bearer-token-value");
        var second = hasher.Hash("bearer-token-value");

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.NotEqual("bearer-token-value", first);
    }

    [Fact]
    public async Task Conditional_rotation_allows_only_one_concurrent_consumer()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Default Timeout=10;Pooling=False";
        var hasher = new RefreshTokenHasher();
        const string rawToken = "legacy-or-current-token";

        try
        {
            await using (var setup = CreateSqliteContext(connectionString))
            {
                await setup.Database.EnsureCreatedAsync();
                var user = CreateUser("rotation@namviet.vn");
                setup.Users.Add(user);
                setup.RefreshTokens.Add(new RefreshToken
                {
                    UserId = user.Id,
                    TokenHash = hasher.Hash(rawToken),
                    ExpiresAt = DateTime.UtcNow.AddDays(1)
                });
                await setup.SaveChangesAsync();
            }

            var now = DateTime.UtcNow;
            async Task<bool> ConsumeAsync(string replacement)
            {
                await using var db = CreateSqliteContext(connectionString);
                var result = await RefreshTokenRotationStore.TryConsumeAsync(
                    db,
                    rawToken,
                    hasher.Hash(rawToken),
                    hasher.Hash(replacement),
                    now,
                    CancellationToken.None);
                return result is not null;
            }

            var outcomes = await Task.WhenAll(
                Task.Run(() => ConsumeAsync("replacement-one")),
                Task.Run(() => ConsumeAsync("replacement-two")));

            Assert.Single(outcomes, success => success);

            await using var verify = CreateSqliteContext(connectionString);
            var stored = await verify.RefreshTokens.SingleAsync();
            Assert.NotNull(stored.RevokedAt);
            Assert.NotNull(stored.ReplacedByTokenHash);
            Assert.DoesNotContain("replacement-", stored.ReplacedByTokenHash!);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task Legacy_plaintext_token_can_rotate_without_storing_new_plaintext()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var hasher = new RefreshTokenHasher();

        try
        {
            await using var db = CreateSqliteContext(connectionString);
            await db.Database.EnsureCreatedAsync();
            var user = CreateUser("legacy@namviet.vn");
            db.Users.Add(user);
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                Token = "legacy-plaintext",
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            });
            await db.SaveChangesAsync();

            var replacementHash = hasher.Hash("new-secret-token");
            var consumed = await RefreshTokenRotationStore.TryConsumeAsync(
                db,
                "legacy-plaintext",
                hasher.Hash("legacy-plaintext"),
                replacementHash,
                DateTime.UtcNow,
                CancellationToken.None);

            Assert.NotNull(consumed);
            db.ChangeTracker.Clear();
            var stored = await db.RefreshTokens.SingleAsync();
            Assert.Null(stored.Token);
            Assert.Equal(hasher.Hash("legacy-plaintext"), stored.TokenHash);
            Assert.Equal(replacementHash, stored.ReplacedByTokenHash);
            Assert.Null(stored.ReplacedByToken);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task Unknown_disabled_and_wrong_password_use_same_external_error()
    {
        await using var provider = CreateLoginServices();
        await using var scope = provider.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var handler = scope.ServiceProvider.GetRequiredService<LoginHandler>();

        var active = CreateUser("active@namviet.vn");
        Assert.True((await userManager.CreateAsync(active, "Correct123!")).Succeeded);
        var disabled = CreateUser("disabled@namviet.vn");
        disabled.IsActive = false;
        Assert.True((await userManager.CreateAsync(disabled, "Correct123!")).Succeeded);

        var unknown = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.Handle(new LoginCommand("unknown@namviet.vn", "Wrong123"), CancellationToken.None));
        var inactive = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.Handle(new LoginCommand(disabled.Email!, "Correct123!"), CancellationToken.None));
        var wrong = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.Handle(new LoginCommand(active.Email!, "Wrong123"), CancellationToken.None));

        Assert.Equal(LoginHandler.InvalidLoginMessage, unknown.Message);
        Assert.Equal(unknown.Message, inactive.Message);
        Assert.Equal(unknown.Message, wrong.Message);
        Assert.Equal(1, (await userManager.FindByIdAsync(active.Id.ToString()))!.AccessFailedCount);
    }

    [Fact]
    public async Task Password_failures_apply_configured_lockout()
    {
        var options = new IdentityOptions();
        IdentitySecurityOptions.ConfigureLockout(options);
        Assert.True(options.Lockout.AllowedForNewUsers);
        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Lockout.DefaultLockoutTimeSpan);

        await using var provider = CreateLoginServices();
        await using var scope = provider.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var handler = scope.ServiceProvider.GetRequiredService<LoginHandler>();
        var user = CreateUser("lockout@namviet.vn");
        Assert.True((await userManager.CreateAsync(user, "Correct123!")).Succeeded);

        for (var attempt = 0; attempt < options.Lockout.MaxFailedAccessAttempts; attempt++)
        {
            var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => handler.Handle(new LoginCommand(user.Email!, "Wrong123"), CancellationToken.None));
            Assert.Equal(LoginHandler.InvalidLoginMessage, error.Message);
        }

        var reloaded = await userManager.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(reloaded);
        Assert.True(await userManager.IsLockedOutAsync(reloaded!));
    }

    private static IdentityDbContext CreateSqliteContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new IdentityDbContext(options);
    }

    private static ServiceProvider CreateLoginServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseInMemoryDatabase($"identity-login-{Guid.NewGuid():N}"));
        services
            .AddIdentity<ApplicationUser, ApplicationRole>(IdentitySecurityOptions.ConfigureLockout)
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "test-only-signing-key-at-least-32-characters",
                ["Jwt:Issuer"] = "tests",
                ["Jwt:Audience"] = "tests"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IRefreshTokenHasher, RefreshTokenHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPermissionService, EmptyPermissionService>();
        services.AddScoped<IEmployeeInfoProvider, EmptyEmployeeInfoProvider>();
        services.AddScoped<LoginHandler>();
        return services.BuildServiceProvider();
    }

    private static ApplicationUser CreateUser(string email) => new()
    {
        Id = Guid.NewGuid(),
        UserName = email,
        Email = email,
        EmployeeCode = $"NV-{Guid.NewGuid():N}"[..12],
        Name = "Identity Security Test",
        IsActive = true,
        LockoutEnabled = true
    };

    private sealed class EmptyEmployeeInfoProvider : IEmployeeInfoProvider
    {
        public Task<LinkedEmployeeInfo?> GetByUserAsync(
            Guid userId,
            string? employeeCode,
            CancellationToken ct = default) =>
            Task.FromResult<LinkedEmployeeInfo?>(null);
    }

    private sealed class EmptyPermissionService : IPermissionService
    {
        public Task<IReadOnlyList<string>> GetPermissionsForUserAsync(
            ApplicationUser user,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(
            ApplicationRole role,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task SetRolePermissionsAsync(
            ApplicationRole role,
            IReadOnlyList<string> permissions,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public IReadOnlyList<string> ValidatePermissions(IReadOnlyList<string> permissions) => permissions;
    }
}
