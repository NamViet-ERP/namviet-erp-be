using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Identity.Application.Abstractions;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Services;

public class RefreshTokenRevoker(IdentityDbContext db) : IRefreshTokenRevoker
{
    public async Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), ct);
    }
}
