using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class PurchasePlan : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string Status { get; set; } = PurchasePlanStatuses.Draft;
    public string? Note { get; set; }

    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }

    public decimal BudgetTotal { get; set; }
    public decimal ProposalTotal { get; set; }
    public decimal PurchaseTotal { get; set; }
    public decimal PaidTotal { get; set; }

    public Project? Project { get; set; }
    public ICollection<PurchasePlanLine> Lines { get; set; } = [];
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = [];
}

public static class PurchasePlanStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Closed = "closed";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Draft, Active, Closed,
    };

    public static bool IsEditable(string status) =>
        !string.Equals(status, Closed, StringComparison.OrdinalIgnoreCase);
}
