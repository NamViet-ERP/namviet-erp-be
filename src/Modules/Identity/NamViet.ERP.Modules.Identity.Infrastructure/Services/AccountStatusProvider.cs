using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Services;

public class AccountStatusProvider(IdentityDbContext db) : IAccountStatusProvider
{
    public async Task<AccountLinkStatus> GetStatusByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.DeletedAt == null)
            .Select(u => new { u.IsActive })
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return AccountLinkStatus.None;

        return user.IsActive ? AccountLinkStatus.Active : AccountLinkStatus.Locked;
    }

    public async Task<Dictionary<Guid, AccountLinkStatus>> GetStatusByUserIdsAsync(
        IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var rows = await db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.DeletedAt == null)
            .Select(u => new { u.Id, u.IsActive })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.Id,
            r => r.IsActive ? AccountLinkStatus.Active : AccountLinkStatus.Locked);
    }
}
