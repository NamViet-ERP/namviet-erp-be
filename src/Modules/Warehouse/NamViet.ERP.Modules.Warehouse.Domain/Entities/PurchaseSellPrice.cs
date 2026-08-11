using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Giá bán / giá chào cho dự án — dùng tab Mua vào – Bán ra.</summary>
public class PurchaseSellPrice : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string? ProjectCode { get; set; }
    public decimal SellPrice { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? Note { get; set; }
}
