using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace NamViet.ERP.Api.Infrastructure.Auth;

public static class AuthCookieNames
{
    public const string AccessToken = "nv_access_token";
    public const string RefreshToken = "nv_refresh_token";
}

public interface IAuthCookieService
{
    /// <summary>HttpOnly refresh cookie only — access JWT stays in response body (cookie size / XSS trade-off).</summary>
    void SetRefreshCookie(HttpResponse response, string refreshToken, DateTime refreshTokenExpiryUtc);

    void ClearAuthCookies(HttpResponse response);

    string? GetRefreshToken(HttpRequest request);
}

public sealed class AuthCookieService(IHostEnvironment env, IConfiguration configuration) : IAuthCookieService
{
    private readonly bool _secure =
        configuration.GetValue("AuthCookies:Secure", !env.IsDevelopment());

    private readonly SameSiteMode _sameSite = ParseSameSite(
        configuration["AuthCookies:SameSite"] ?? "Lax");

    private readonly string? _domain = NullIfEmpty(configuration["AuthCookies:Domain"]);

    public void SetRefreshCookie(HttpResponse response, string refreshToken, DateTime refreshTokenExpiryUtc)
    {
        // Drop legacy access cookie if present (too large with permission claims).
        response.Cookies.Append(AuthCookieNames.AccessToken, string.Empty, BuildOptions(DateTime.UtcNow.AddDays(-1)));
        response.Cookies.Append(AuthCookieNames.RefreshToken, refreshToken, BuildOptions(refreshTokenExpiryUtc));
    }

    public void ClearAuthCookies(HttpResponse response)
    {
        var clear = BuildOptions(DateTime.UtcNow.AddDays(-1));
        response.Cookies.Append(AuthCookieNames.AccessToken, string.Empty, clear);
        response.Cookies.Append(AuthCookieNames.RefreshToken, string.Empty, clear);
    }

    public string? GetRefreshToken(HttpRequest request) =>
        request.Cookies.TryGetValue(AuthCookieNames.RefreshToken, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private CookieOptions BuildOptions(DateTime expiresUtc) => new()
    {
        HttpOnly = true,
        Secure = _secure,
        SameSite = _sameSite,
        Domain = _domain,
        Path = "/",
        Expires = DateTime.SpecifyKind(expiresUtc, DateTimeKind.Utc),
        IsEssential = true,
    };

    private static SameSiteMode ParseSameSite(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "none" => SameSiteMode.None,
            "strict" => SameSiteMode.Strict,
            _ => SameSiteMode.Lax,
        };

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
