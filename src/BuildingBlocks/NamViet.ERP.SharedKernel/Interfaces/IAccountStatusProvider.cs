namespace NamViet.ERP.SharedKernel.Interfaces;

/// <summary>
/// Cross-module query interface for checking employee-to-system-account linkage.
/// Implemented by the Identity module, consumed by any module needing account status.
/// </summary>
public enum AccountLinkStatus
{
    /// <summary>Employee has no linked system account.</summary>
    None,
    /// <summary>Linked system account exists and is active.</summary>
    Active,
    /// <summary>Linked system account exists but is locked/disabled.</summary>
    Locked
}

public interface IAccountStatusProvider
{
    /// <summary>
    /// Gets the account linkage status for a single user.
    /// Returns <see cref="AccountLinkStatus.None"/> if userId is not found.
    /// </summary>
    Task<AccountLinkStatus> GetStatusByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Batch version: gets account linkage status for multiple user IDs.
    /// Missing user IDs are returned as <see cref="AccountLinkStatus.None"/>.
    /// </summary>
    Task<Dictionary<Guid, AccountLinkStatus>> GetStatusByUserIdsAsync(
        IEnumerable<Guid> userIds, CancellationToken ct = default);
}
