namespace NamViet.ERP.Modules.Identity.Domain.Entities;

public class RefreshToken
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    // Legacy plaintext columns remain nullable during the rollout. New records only use hashes.
    public string? Token { get; set; }
    public string? TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsExpired && !IsRevoked;

    public ApplicationUser User { get; set; } = null!;
}
