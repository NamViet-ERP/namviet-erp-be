using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Identity.Application.Commands;
using NamViet.ERP.Modules.Identity.Application.DTOs;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Services;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Handlers;

public class LoginHandler(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IdentityDbContext db)
    : IRequestHandler<LoginCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email)
            ?? throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");

        if (user.DeletedAt.HasValue)
            throw new UnauthorizedAccessException("Tài khoản đã bị vô hiệu hóa.");

        var validPassword = await userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var refreshTokenValue = tokenService.GenerateRefreshToken();

        // Revoke old tokens
        var oldTokens = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);
        foreach (var t in oldTokens) t.RevokedAt = DateTime.UtcNow;

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = tokenService.GetRefreshTokenExpiry()
        };
        db.RefreshTokens.Add(refreshToken);

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new AuthResponse(
            accessToken,
            refreshTokenValue,
            DateTime.UtcNow.AddMinutes(60),
            new UserDto(user.Id, user.EmployeeCode, user.Name, user.Email!, user.Department, user.AvatarUrl, [.. roles]));
    }
}
