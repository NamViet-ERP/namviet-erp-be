using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Hr;

public interface IEmployeeOnboardingInviteService
{
    Task<(EmployeeOnboardingInvite Invite, string RawToken)> CreateAsync(
        Guid creatorUserId, DateTime expiresAtUtc, CancellationToken ct);
    Task<EmployeeOnboardingInvite?> FindValidAsync(string rawToken, CancellationToken ct);
}

public sealed class EmployeeOnboardingInviteService(HrDbContext db) : IEmployeeOnboardingInviteService
{
    public async Task<(EmployeeOnboardingInvite Invite, string RawToken)> CreateAsync(
        Guid creatorUserId, DateTime expiresAtUtc, CancellationToken ct)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var invite = new EmployeeOnboardingInvite
        {
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc),
            CreatedByUserId = creatorUserId,
        };
        db.EmployeeOnboardingInvites.Add(invite);
        await db.SaveChangesAsync(ct);
        return (invite, rawToken);
    }

    public Task<EmployeeOnboardingInvite?> FindValidAsync(string rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return Task.FromResult<EmployeeOnboardingInvite?>(null);

        var hash = HashToken(rawToken.Trim());
        var now = DateTime.UtcNow;
        return db.EmployeeOnboardingInvites.FirstOrDefaultAsync(
            i => i.TokenHash == hash
                && i.ExpiresAt > now
                && i.ConsumedAt == null
                && i.RevokedAt == null,
            ct);
    }

    public static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
}
