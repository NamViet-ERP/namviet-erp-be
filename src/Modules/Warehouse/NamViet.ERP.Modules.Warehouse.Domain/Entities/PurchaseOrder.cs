using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class PurchaseOrder : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string? ProjectCode { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public DateOnly OrderDate { get; set; }

    public string ApprovalStatus { get; set; } = PurchaseOrderApprovalStatuses.Draft;
    public string OrderPlacementStatus { get; set; } = PurchaseOrderPlacementStatuses.NotPlaced;
    public string DeliveryStatus { get; set; } = PurchaseOrderDeliveryStatuses.None;
    public string PaymentStatus { get; set; } = PurchaseOrderPaymentStatuses.Unpaid;
    public bool PaymentWarning { get; set; }
    public string? Note { get; set; }
    public decimal TotalAmount { get; set; }

    public int SupplierPaymentTermsDays { get; set; }
    public string? SupplierNameSnapshot { get; set; }
    public DateOnly? PaymentDueDate { get; set; }

    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid? PlacedById { get; set; }
    public string? PlacedByName { get; set; }
    public DateTime? PlacedAt { get; set; }

    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }

    public Project? Project { get; set; }
    public PurchasePlan? Plan { get; set; }
    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; } = [];
}

public class PurchaseOrderItem
{
    public int Id { get; set; }
    public string PoCode { get; set; } = string.Empty;
    public int? PlanLineId { get; set; }
    public string? MaterialCode { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Spec { get; set; }
    public decimal EstPrice { get; set; }
    public decimal PoPrice { get; set; }
    public decimal Ordered { get; set; }
    public decimal Received { get; set; }
    /// <summary>SL đã ghi nhận hàng lỗi (phiếu HL đã duyệt).</summary>
    public decimal Defected { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateOnly? ExpectedDeliveryDate { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public PurchasePlanLine? PlanLine { get; set; }
    public Material? Material { get; set; }
}

public static class PurchaseOrderApprovalStatuses
{
    public const string Draft = "draft";
    public const string Approved = "approved";
}

public static class PurchaseOrderPlacementStatuses
{
    public const string NotPlaced = "not_placed";
    public const string Placed = "placed";
}

public static class PurchaseOrderDeliveryStatuses
{
    public const string None = "none";
    public const string Partial = "partial";
    public const string Complete = "complete";
}

public static class PurchaseOrderPaymentStatuses
{
    public const string Unpaid = "unpaid";
    public const string Partial = "partial";
    public const string Paid = "paid";
    public const string Overdue = "overdue";
}
