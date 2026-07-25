using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Đề nghị thanh toán (DNTT) — chi nhà cung cấp, gắn 1 PO.</summary>
public class PurchasePayment : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string PoCode { get; set; } = string.Empty;
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
