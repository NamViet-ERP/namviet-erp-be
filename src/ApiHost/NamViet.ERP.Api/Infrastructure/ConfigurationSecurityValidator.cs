namespace NamViet.ERP.Api.Infrastructure;

/// <summary>
/// Fail fast when required secrets are missing or still use known placeholder values.
/// </summary>
public static class ConfigurationSecurityValidator
{
    private static readonly string[] JwtPlaceholderMarkers =
    [
        "REPLACE_WITH_32_CHAR",
        "replace_with_a_long_random_secret",
        "changeme",
        "your_secret_here",
    ];

    private static readonly string[] WeakDbPasswordMarkers =
    [
        "change_me_strong",
    ];

    private static readonly string[] CorsPlaceholderMarkers =
    [
        "your-frontend-domain.com",
        "localhost",
    ];

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        ValidateJwtSigningKey(configuration);
        ValidateConnectionString(configuration, environment);
        ValidateCorsOrigins(configuration, environment);
    }

    private static void ValidateJwtSigningKey(IConfiguration configuration)
    {
        var key = configuration["Jwt:SigningKey"]?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is required. Set via dotnet user-secrets, environment variable Jwt__SigningKey, or .env.");
        }

        if (key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
        }

        var lower = key.ToLowerInvariant();
        foreach (var marker in JwtPlaceholderMarkers)
        {
            if (lower.Contains(marker, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Jwt:SigningKey must not be a placeholder. Generate a random secret (≥32 chars).");
            }
        }
    }

    private static void ValidateConnectionString(IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("Default")?.Trim();
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default is required. Set via dotnet user-secrets, environment variable, or .env.");
        }

        if (environment.IsDevelopment())
            return;

        var lower = connectionString.ToLowerInvariant();
        foreach (var marker in WeakDbPasswordMarkers)
        {
            if (lower.Contains(marker, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Connection string uses a known default password. Use a strong password outside Development.");
            }
        }
    }

    private static void ValidateCorsOrigins(IConfiguration configuration, IHostEnvironment environment)
    {
        // Development may use localhost origins from appsettings / .env.
        if (environment.IsDevelopment())
            return;

        var raw = configuration["Cors:AllowedOrigins"]?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins is required outside Development. Set Cors__AllowedOrigins to the real frontend origin(s).");
        }

        var lower = raw.ToLowerInvariant();
        foreach (var marker in CorsPlaceholderMarkers)
        {
            if (lower.Contains(marker, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Cors:AllowedOrigins must not use placeholder or localhost origins outside Development. " +
                    "Set Cors__AllowedOrigins to the real HTTPS frontend origin(s).");
            }
        }
    }
}
