using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Chứng từ dự toán vật tư theo dự án. Luôn dùng version mới nhất để vận hành.</summary>
public class PurchaseEstimate : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CurrentVersion { get; set; } = 1;
    public string Status { get; set; } = PurchaseEstimateStatuses.Draft;
    public Guid? ShareToken { get; set; }
    public DateTime? ShareTokenExpiresAt { get; set; }
    public string? PreparerName { get; set; }
    public DateTime? PreparerDate { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ConvertedPlanCode { get; set; }
    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public Guid UpdatedById { get; set; }
    public string UpdatedByName { get; set; } = string.Empty;

    public ICollection<PurchaseEstimateVersion> Versions { get; set; } = [];
    public ICollection<PurchaseEstimateProposal> Proposals { get; set; } = [];
    public ICollection<PurchaseEstimateApproval> Approvals { get; set; } = [];

    public static string GenerateCode(int sequence) => $"DT-{sequence:D4}";
}

public static class PurchaseEstimateStatuses
{
    public const string Draft = "draft";
    public const string Shared = "shared";
    public const string Submitted = "submitted";
    public const string Reviewed = "reviewed";
    public const string PendingApproval = "pending_approval";
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Draft, Shared, Submitted, Reviewed, PendingApproval, Approved, Rejected,
    };

    public static bool CanFill(string? status) =>
        string.Equals(status, Draft, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Shared, StringComparison.OrdinalIgnoreCase);

    public static bool CanEditLines(string? status) =>
        string.Equals(status, Draft, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Shared, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Submitted, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Reviewed, StringComparison.OrdinalIgnoreCase);
}

public class PurchaseEstimateVersion : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstimateId { get; set; }
    public int VersionNumber { get; set; }
    public Guid UpdatedById { get; set; }
    public string UpdatedByName { get; set; } = string.Empty;

    public PurchaseEstimate? Estimate { get; set; }
    public ICollection<PurchaseEstimateLine> Lines { get; set; } = [];
}

public class PurchaseEstimateLine : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VersionId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal OldQty { get; set; }
    public decimal NewQty { get; set; }

    public PurchaseEstimateVersion? Version { get; set; }
}

public class PurchaseEstimateProposal : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstimateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Spec { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public string? Note { get; set; }

    public PurchaseEstimate? Estimate { get; set; }
}

public class PurchaseEstimateApproval : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstimateId { get; set; }
    public int Level { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public Guid? ActionById { get; set; }
    public string? ActionByName { get; set; }
    public DateTime? ActionAt { get; set; }
    public string? Note { get; set; }

    public PurchaseEstimate? Estimate { get; set; }
}
