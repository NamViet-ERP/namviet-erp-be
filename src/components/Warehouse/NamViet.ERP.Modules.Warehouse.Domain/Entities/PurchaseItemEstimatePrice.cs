using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Giá dự toán (giá công ty) — tab Giá dự toán &amp; Chênh lệch.</summary>
public class PurchaseItemEstimatePrice : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string? ProjectCode { get; set; }
    public decimal EstimatePrice { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? Note { get; set; }
}
