using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Hr.Domain.Entities;

public class EmployeeOnboardingInvite : BaseEntity
{
    public int Id { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public int? ConsumedEmployeeId { get; set; }
    public Employee? ConsumedEmployee { get; set; }
}
