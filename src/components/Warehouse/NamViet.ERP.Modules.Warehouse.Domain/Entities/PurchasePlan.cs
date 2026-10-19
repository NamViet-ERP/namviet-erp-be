using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class PurchasePlan : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;

    /// <summary>plan = KHMH; adhoc = mua hàng phát sinh.</summary>
    public string Kind { get; set; } = PurchasePlanKinds.Plan;

    public string Status { get; set; } = PurchasePlanStatuses.Draft;
    public string? Note { get; set; }

    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public Guid? L1ApprovedById { get; set; }
    public string? L1ApprovedByName { get; set; }
    public DateTime? L1ApprovedAt { get; set; }

    public Guid? L2ApprovedById { get; set; }
    public string? L2ApprovedByName { get; set; }
    public DateTime? L2ApprovedAt { get; set; }

    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectReason { get; set; }

    /// <summary>Phiếu bị từ chối mà phiếu này được lập lại từ đó.</summary>
    public string? RemadeFromCode { get; set; }

    /// <summary>Phiếu nháp mới nhất được lập lại từ phiếu từ chối này.</summary>
    public string? RemadeToCode { get; set; }

    public decimal BudgetTotal { get; set; }
    public decimal ProposalTotal { get; set; }
    public decimal PurchaseTotal { get; set; }
    public decimal PaidTotal { get; set; }

    public Project? Project { get; set; }
    public ICollection<PurchasePlanLine> Lines { get; set; } = [];
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = [];
}

public static class PurchasePlanKinds
{
    public const string Plan = "plan";
    public const string Adhoc = "adhoc";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Plan, Adhoc,
    };

    public static bool IsValid(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) && All.Contains(kind.Trim());
}

public static class PurchasePlanStatuses
{
    public const string Draft = "draft";
    public const string PendingL1 = "pending_l1";
    public const string PendingL2 = "pending_l2";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Closed = "closed";

    /** Legacy — migrated to approved. */
    public const string ActiveLegacy = "active";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Draft, PendingL1, PendingL2, Approved, Rejected, Closed,
    };

    public static bool IsEditable(string status) =>
        string.Equals(status, Draft, StringComparison.OrdinalIgnoreCase);

    public static bool CanCreatePurchaseOrder(string status) =>
        string.Equals(status, Approved, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, ActiveLegacy, StringComparison.OrdinalIgnoreCase);

    public static bool CanClose(string status) =>
        string.Equals(status, Approved, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, ActiveLegacy, StringComparison.OrdinalIgnoreCase);
}
