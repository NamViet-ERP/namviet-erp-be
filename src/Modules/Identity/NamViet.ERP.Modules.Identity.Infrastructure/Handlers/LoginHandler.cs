using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Modules.Identity.Application.Abstractions;
using NamViet.ERP.Modules.Identity.Application.Commands;
using NamViet.ERP.Modules.Identity.Application.DTOs;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Services;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Handlers;

public class LoginHandler(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService,
    IRefreshTokenHasher refreshTokenHasher,
    IPermissionService permissionService,
    IdentityDbContext db,
    IEmployeeInfoProvider employeeInfo,
    ILogger<LoginHandler> logger) : IRequestHandler<LoginCommand, AuthResponse>
{
    public const string InvalidLoginMessage = "Thông tin đăng nhập không chính xác.";

    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Username)
            ?? await userManager.Users.FirstOrDefaultAsync(u => u.UserName == request.Username, ct);

        if (user is null)
            throw new UnauthorizedAccessException(InvalidLoginMessage);

        if (user.DeletedAt.HasValue || !user.IsActive)
        {
            logger.LogWarning("Login rejected for disabled Identity user {UserId}.", user.Id);
            throw new UnauthorizedAccessException(InvalidLoginMessage);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            if (signInResult.IsLockedOut)
                logger.LogWarning("Login rejected because Identity user {UserId} is locked out.", user.Id);
            throw new UnauthorizedAccessException(InvalidLoginMessage);
        }

        var roles = await userManager.GetRolesAsync(user);
        var permissions = await permissionService.GetPermissionsForUserAsync(user, ct);
        var linked = await employeeInfo.GetByUserAsync(user.Id, user.EmployeeCode, ct);
        var accessToken = tokenService.GenerateAccessToken(user, roles, permissions, linked?.EmployeeId);
        var refreshTokenValue = tokenService.GenerateRefreshToken();

        var oldTokens = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);
        foreach (var token in oldTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            if (token.TokenHash is null && !string.IsNullOrWhiteSpace(token.Token))
            {
                token.TokenHash = refreshTokenHasher.Hash(token.Token);
                token.Token = null;
            }
            if (token.ReplacedByTokenHash is null && !string.IsNullOrWhiteSpace(token.ReplacedByToken))
                token.ReplacedByTokenHash = refreshTokenHasher.Hash(token.ReplacedByToken);
            token.ReplacedByToken = null;
        }

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHasher.Hash(refreshTokenValue),
            ExpiresAt = tokenService.GetRefreshTokenExpiry(request.RememberMe)
        };
        db.RefreshTokens.Add(refreshToken);

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new AuthResponse(
            accessToken,
            refreshTokenValue,
            tokenService.GetAccessTokenExpiry(),
            new UserDto(user.Id, user.EmployeeCode, user.Name, user.Email!,
                linked?.DepartmentName, user.AvatarUrl, [.. roles]));
    }
}
