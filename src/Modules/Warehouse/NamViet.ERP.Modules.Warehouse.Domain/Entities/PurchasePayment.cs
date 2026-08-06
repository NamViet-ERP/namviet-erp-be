using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Đề nghị thanh toán (DNTT) — gắn 1 đợt thanh toán của PO (tuần tự đợt 1 → 2 → …).</summary>
public class PurchasePayment : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string PoCode { get; set; } = string.Empty;
    /// <summary>Số đợt trên PO (1..N). DNTT tuần tự: đợt N chỉ sau khi đợt trước đã paid.</summary>
    public int StageNumber { get; set; } = 1;
    public int? SupplierId { get; set; }
    public string? SupplierNameSnapshot { get; set; }
    public string? InvoiceNo { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = PurchasePaymentStatuses.Draft;
    public string? Note { get; set; }

    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }
    public Supplier? Supplier { get; set; }
}

public static class PurchasePaymentStatuses
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string Paid = "paid";
    public const string Cancelled = "cancelled";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Draft, Submitted, Paid, Cancelled,
    };
}
