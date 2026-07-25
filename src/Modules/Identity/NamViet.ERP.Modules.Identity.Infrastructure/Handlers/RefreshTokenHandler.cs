using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Identity.Application.Abstractions;
using NamViet.ERP.Modules.Identity.Application.Commands;
using NamViet.ERP.Modules.Identity.Application.DTOs;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Services;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Handlers;

public class RefreshTokenHandler(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IRefreshTokenHasher refreshTokenHasher,
    IPermissionService permissionService,
    IdentityDbContext db,
    IEmployeeInfoProvider employeeInfo) : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var presentedTokenHash = refreshTokenHasher.Hash(request.RefreshToken);
        var newRefreshValue = tokenService.GenerateRefreshToken();
        var newRefreshHash = refreshTokenHasher.Hash(newRefreshValue);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var stored = await RefreshTokenRotationStore.TryConsumeAsync(
            db,
            request.RefreshToken,
            presentedTokenHash,
            newRefreshHash,
            now,
            ct);

        if (stored is null)
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");

        var user = stored.User;
        if (user.DeletedAt.HasValue || !user.IsActive)
            throw new UnauthorizedAccessException("Tài khoản không còn hoạt động.");

        var remainingDays = Math.Max(1, (stored.ExpiresAt - now).TotalDays);
        var newRefresh = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newRefreshHash,
            ExpiresAt = now.AddDays(remainingDays),
        };
        db.RefreshTokens.Add(newRefresh);

        var roles = await userManager.GetRolesAsync(user);
        var permissions = await permissionService.GetPermissionsForUserAsync(user, ct);
        var linked = await employeeInfo.GetByUserAsync(user.Id, user.EmployeeCode, ct);
        var accessToken = tokenService.GenerateAccessToken(user, roles, permissions, linked?.EmployeeId);

        user.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new AuthResponse(
            accessToken,
            newRefreshValue,
            tokenService.GetAccessTokenExpiry(),
            newRefresh.ExpiresAt,
            new UserDto(user.Id, user.EmployeeCode, user.Name, user.Email!,
                linked?.DepartmentName, user.AvatarUrl, [.. roles]));
    }
}
