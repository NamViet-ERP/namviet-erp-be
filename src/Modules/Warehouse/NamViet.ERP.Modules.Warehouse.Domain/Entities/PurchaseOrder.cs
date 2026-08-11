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
    public ICollection<PurchaseOrderAttachment> Attachments { get; set; } = [];
    public ICollection<PurchaseOrderInstallment> Installments { get; set; } = [];
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
    /// <summary>Số tháng bảo hành (0 = không theo dõi BH).</summary>
    public int WarrantyMonths { get; set; }
    /// <summary>delivery | acceptance — mốc tính hạn BH.</summary>
    public string WarrantyAnchorType { get; set; } = "delivery";

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

/// <summary>Tài liệu đính kèm PO (hợp đồng, hóa đơn, CO/CQ, BB giao hàng, …).</summary>
public class PurchaseOrderAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PoCode { get; set; } = string.Empty;
    /// <summary>contract | invoice | co | cq | bbgh | bbnt | cnxk | tokhai | pl | other</summary>
    public string DocType { get; set; } = "other";
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? UploadedByName { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }
}

public static class PurchaseOrderAttachmentDocTypes
{
    public const string Contract = "contract";
    public const string Invoice = "invoice";
    public const string Co = "co";
    public const string Cq = "cq";
    public const string Bbgh = "bbgh";
    public const string Bbnt = "bbnt";
    /// <summary>Chứng nhận xuất xưởng.</summary>
    public const string Cnxk = "cnxk";
    /// <summary>Tờ khai hải quan.</summary>
    public const string Tokhai = "tokhai";
    /// <summary>Packing list.</summary>
    public const string Pl = "pl";
    public const string Other = "other";

    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Contract, Invoice, Co, Cq, Bbgh, Bbnt, Cnxk, Tokhai, Pl, Other,
    };
}

public class PurchaseOrderInstallment
{
    public int Id { get; set; }
    public string PoCode { get; set; } = string.Empty;
    public int StageNumber { get; set; }
    public decimal Percent { get; set; }
    public string Status { get; set; } = PurchaseOrderInstallmentStatuses.Locked;
    public string RequiredDocTypes { get; set; } = "[]";

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public ICollection<PurchaseOrderInstallmentDocument> Documents { get; set; } = [];
}

public class PurchaseOrderInstallmentDocument
{
    public int Id { get; set; }
    public int InstallmentId { get; set; }
    public string DocType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string StoragePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? UploadedByName { get; set; }

    public PurchaseOrderInstallment Installment { get; set; } = null!;
}

public static class PurchaseOrderInstallmentStatuses
{
    public const string Pending = "pending";
    public const string Open = "open";
    public const string Completed = "completed";
    public const string Locked = "locked";
}
