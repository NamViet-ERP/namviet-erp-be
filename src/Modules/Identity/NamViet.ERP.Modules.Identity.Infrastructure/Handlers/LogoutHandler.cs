using MediatR;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Identity.Application.Commands;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Services;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Handlers;

public class LogoutHandler(
    IRefreshTokenHasher refreshTokenHasher,
    IdentityDbContext db) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return;

        var hash = refreshTokenHasher.Hash(request.RefreshToken);
        var now = DateTime.UtcNow;
        var tokens = await db.RefreshTokens
            .Where(t => t.RevokedAt == null && t.ExpiresAt > now
                && (t.TokenHash == hash || t.Token == request.RefreshToken))
            .ToListAsync(ct);

        foreach (var token in tokens)
            token.RevokedAt = now;

        if (tokens.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
