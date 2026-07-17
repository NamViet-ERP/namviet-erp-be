using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Identity.Domain.Entities;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Persistence;

public static class RefreshTokenRotationStore
{
    public static async Task<RefreshToken?> TryConsumeAsync(
        IdentityDbContext db,
        string presentedToken,
        string presentedTokenHash,
        string replacementTokenHash,
        DateTime consumedAt,
        CancellationToken ct)
    {
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(
                t => t.TokenHash == presentedTokenHash
                    || (t.TokenHash == null && t.Token == presentedToken),
                ct);

        if (stored is null || stored.RevokedAt.HasValue || stored.ExpiresAt <= consumedAt)
            return null;

        var consumed = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAt == null && t.ExpiresAt > consumedAt)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.RevokedAt, consumedAt)
                    .SetProperty(t => t.TokenHash, presentedTokenHash)
                    .SetProperty(t => t.Token, (string?)null)
                    .SetProperty(t => t.ReplacedByToken, (string?)null)
                    .SetProperty(t => t.ReplacedByTokenHash, replacementTokenHash),
                ct);

        return consumed == 1 ? stored : null;
    }
}
