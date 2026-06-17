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

public class RefreshTokenHandler(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IdentityDbContext db,
    HrDbContext hrDb)
    : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, ct);

        if (stored is null || !stored.IsActive)
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");

        var user = stored.User;
        if (user.DeletedAt.HasValue || !user.IsActive)
            throw new UnauthorizedAccessException("Tài khoản không còn hoạt động.");

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var newRefreshValue = tokenService.GenerateRefreshToken();

        stored.RevokedAt = DateTime.UtcNow;
        stored.ReplacedByToken = newRefreshValue;

        var remainingDays = Math.Max(1, (stored.ExpiresAt - DateTime.UtcNow).TotalDays);
        var newRefresh = new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshValue,
            ExpiresAt = DateTime.UtcNow.AddDays(remainingDays),
        };
        db.RefreshTokens.Add(newRefresh);

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var employee = await hrDb.Employees
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.UserId == user.Id || e.Code == user.EmployeeCode, ct);

        return new AuthResponse(
            accessToken,
            newRefreshValue,
            tokenService.GetAccessTokenExpiry(),
            new UserDto(user.Id, user.EmployeeCode, user.Name, user.Email!,
                employee?.Department?.Name, user.AvatarUrl, [.. roles]));
    }
}
