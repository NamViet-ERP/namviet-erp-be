using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Bảng giá vật tư theo nhà cung cấp</summary>
public class PriceBook : BaseEntity
{
    public int Id { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public decimal UnitPrice { get; set; }
    public string? PurchaseUrl { get; set; }    // Link mua hàng online
    public string? CatalogUrl { get; set; }     // Link catalog kỹ thuật
    public DateTime PriceDate { get; set; } = DateTime.UtcNow;

    public Material Material { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
}
