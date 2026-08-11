using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Hồ sơ bảo hành theo dòng PO (sau khi nhận hàng).</summary>
public class PurchaseWarranty : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string PoCode { get; set; } = string.Empty;
    public int PoItemId { get; set; }
    public string? ProjectCode { get; set; }
    public string? PlanCode { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? MaterialCode { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public int WarrantyMonths { get; set; }
    /// <summary><see cref="PurchaseWarrantyAnchorTypes"/> — delivery | acceptance.</summary>
    public string AnchorType { get; set; } = PurchaseWarrantyAnchorTypes.Delivery;
    public DateOnly? DeliveryDate { get; set; }
    public DateOnly? AcceptanceDate { get; set; }
    public DateOnly? WarrantyEndDate { get; set; }
    /// <summary><see cref="PurchaseWarrantyStatuses"/>.</summary>
    public string Status { get; set; } = PurchaseWarrantyStatuses.PendingAnchor;
    public string? ImportTicketCode { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }
    public PurchaseOrderItem? PurchaseOrderItem { get; set; }
    public ICollection<PurchaseWarrantyClaim> Claims { get; set; } = [];

    public static string GenerateCode(int sequence) => $"BH-{sequence:D4}";

    public static DateOnly ComputeEndDate(DateOnly start, int months) =>
        start.AddMonths(months).AddDays(-1);

    public void Recalculate(DateOnly today)
    {
        DateOnly? anchor = string.Equals(AnchorType, PurchaseWarrantyAnchorTypes.Acceptance, StringComparison.OrdinalIgnoreCase)
            ? AcceptanceDate
            : DeliveryDate;

        if (anchor is null || WarrantyMonths <= 0)
        {
            WarrantyEndDate = null;
            Status = PurchaseWarrantyStatuses.PendingAnchor;
            return;
        }

        WarrantyEndDate = ComputeEndDate(anchor.Value, WarrantyMonths);
        Status = WarrantyEndDate.Value < today
            ? PurchaseWarrantyStatuses.Expired
            : PurchaseWarrantyStatuses.Active;
    }
}

/// <summary>Phiếu bảo hành (claim) gắn hồ sơ BH.</summary>
public class PurchaseWarrantyClaim : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TicketCode { get; set; } = string.Empty;
    public Guid WarrantyId { get; set; }
    /// <summary><see cref="PurchaseWarrantyClaimStatuses"/>.</summary>
    public string Status { get; set; } = PurchaseWarrantyClaimStatuses.Draft;
    /// <summary>Nguyên nhân (nhập tay).</summary>
    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    /// <summary>Đánh giá dịch vụ BH NCC khi đóng phiếu (1–5).</summary>
    public int? ServiceRating { get; set; }
    public string? ServiceNote { get; set; }

    public PurchaseWarranty? Warranty { get; set; }

    public static string GenerateTicketCode(int sequence) => $"PBH-{sequence:D4}";
}

public static class PurchaseWarrantyAnchorTypes
{
    public const string Delivery = "delivery";
    public const string Acceptance = "acceptance";

    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Delivery, Acceptance,
    };
}

public static class PurchaseWarrantyStatuses
{
    public const string PendingAnchor = "pending_anchor";
    public const string Active = "active";
    public const string Expired = "expired";
    public const string Void = "void";
}

public static class PurchaseWarrantyClaimStatuses
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string Closed = "closed";
    public const string Cancelled = "cancelled";
}
