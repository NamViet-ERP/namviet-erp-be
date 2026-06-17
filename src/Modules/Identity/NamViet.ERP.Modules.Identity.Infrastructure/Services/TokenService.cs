using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NamViet.ERP.Modules.Identity.Domain.Entities;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Services;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);
    string GenerateRefreshToken();
    DateTime GetAccessTokenExpiry();
    DateTime GetRefreshTokenExpiry(bool rememberMe = true);
}

public class TokenService(IConfiguration configuration) : ITokenService
{
    private readonly string _signingKey = configuration["Jwt:SigningKey"]
        ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "namviet-erp";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "namviet-erp-client";
    private readonly int _accessMinutes = int.TryParse(configuration["Jwt:AccessTokenMinutes"], out var m) ? m : 60;
    private readonly int _refreshDays = int.TryParse(configuration["Jwt:RefreshTokenDays"], out var d) ? d : 30;
    private readonly int _sessionRefreshDays = int.TryParse(configuration["Jwt:SessionRefreshTokenDays"], out var s) ? s : 1;

    public string GenerateAccessToken(ApplicationUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("employee_code", user.EmployeeCode),
            new("name", user.Name),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public DateTime GetAccessTokenExpiry() =>
        DateTime.UtcNow.AddMinutes(_accessMinutes);

    public DateTime GetRefreshTokenExpiry(bool rememberMe = true) =>
        DateTime.UtcNow.AddDays(rememberMe ? _refreshDays : _sessionRefreshDays);
}
