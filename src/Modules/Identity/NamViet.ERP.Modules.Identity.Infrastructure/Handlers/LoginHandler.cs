using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Application.Commands;
using NamViet.ERP.Modules.Identity.Application.DTOs;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Services;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Handlers;

public class LoginHandler(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IdentityDbContext db,
    HrDbContext hrDb)
    : IRequestHandler<LoginCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email)
            ?? await userManager.Users.FirstOrDefaultAsync(u => u.UserName == request.Email, ct)
            ?? throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");

        if (user.DeletedAt.HasValue)
            throw new UnauthorizedAccessException("Tài khoản đã bị vô hiệu hóa.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Tài khoản của bạn đã bị khóa hoặc chưa được kích hoạt.");

        var validPassword = await userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var refreshTokenValue = tokenService.GenerateRefreshToken();

        var oldTokens = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);
        foreach (var t in oldTokens) t.RevokedAt = DateTime.UtcNow;

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = tokenService.GetRefreshTokenExpiry(request.RememberMe)
        };
        db.RefreshTokens.Add(refreshToken);

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var employee = await hrDb.Employees
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.UserId == user.Id || e.Code == user.EmployeeCode, ct);

        return new AuthResponse(
            accessToken,
            refreshTokenValue,
            tokenService.GetAccessTokenExpiry(),
            new UserDto(user.Id, user.EmployeeCode, user.Name, user.Email!,
                employee?.Department?.Name, user.AvatarUrl, [.. roles]));
    }
}
