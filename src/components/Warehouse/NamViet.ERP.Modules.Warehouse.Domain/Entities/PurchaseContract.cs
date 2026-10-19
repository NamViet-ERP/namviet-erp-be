using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Hợp đồng mua hàng độc lập (gắn PO / NCC / dự án tùy chọn).</summary>
public class PurchaseContract : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    /// <summary>Số hợp đồng bên ngoài (NCC / pháp lý).</summary>
    public string ContractNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? PoCode { get; set; }
    public string? ProjectCode { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    /// <summary><see cref="PurchaseContractStatuses"/>.</summary>
    public string Status { get; set; } = PurchaseContractStatuses.Draft;
    public DateOnly? SignedDate { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal? Amount { get; set; }
    public string? Note { get; set; }
    public string? FileName { get; set; }
    public string? StorageKey { get; set; }
    public DateTime? UploadedAt { get; set; }
    public string? UploadedByName { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }
    public Supplier? Supplier { get; set; }

    public static string GenerateCode(int sequence) => $"HD-{sequence:D4}";

    public void RefreshStatus(DateOnly today)
    {
        if (string.Equals(Status, PurchaseContractStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            return;

        if (EffectiveTo is not null && EffectiveTo.Value < today)
        {
            Status = PurchaseContractStatuses.Expired;
            return;
        }

        if (string.Equals(Status, PurchaseContractStatuses.Expired, StringComparison.OrdinalIgnoreCase)
            && (EffectiveTo is null || EffectiveTo.Value >= today))
        {
            Status = PurchaseContractStatuses.Active;
        }
    }
}

public static class PurchaseContractStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Expired = "expired";
    public const string Cancelled = "cancelled";

    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Draft, Active, Expired, Cancelled,
    };
}
