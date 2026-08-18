using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Hr;

public interface IEmployeeAccountLinkSynchronizer
{
    /// <summary>
    /// Writes <c>employees.UserId</c> when a login already exists with the same employee code.
    /// Idempotent. Feature code must read the link from UserId only.
    /// </summary>
    Task<int> SynchronizeAsync(CancellationToken ct = default);

    Task<int> AttachByCodeAsync(string employeeCode, Guid userId, CancellationToken ct = default);
}

public sealed class EmployeeAccountLinkSynchronizer(
    HrDbContext hr,
    IdentityDbContext identity,
    IEmployeeUserLinkService links) : IEmployeeAccountLinkSynchronizer
{
    public async Task<int> SynchronizeAsync(CancellationToken ct = default)
    {
        var missingCodes = await hr.Employees.AsNoTracking()
            .Where(e => e.UserId == null)
            .Select(e => e.Code)
            .ToListAsync(ct);
        if (missingCodes.Count == 0) return 0;

        var missing = missingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var users = await identity.Users.AsNoTracking()
            .Where(u => u.DeletedAt == null && u.EmployeeCode != "")
            .Select(u => new { u.Id, u.EmployeeCode })
            .ToListAsync(ct);

        var map = users
            .Where(u => missing.Contains(u.EmployeeCode))
            .GroupBy(u => u.EmployeeCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        return await links.AttachMissingUserIdsAsync(map, ct);
    }

    public Task<int> AttachByCodeAsync(string employeeCode, Guid userId, CancellationToken ct = default)
    {
        var code = employeeCode.Trim();
        if (code.Length == 0) return Task.FromResult(0);
        return links.AttachMissingUserIdsAsync(
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { [code] = userId },
            ct);
    }
}
