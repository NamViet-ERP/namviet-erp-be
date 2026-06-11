using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Vật tư – material_code format: XX-YYY-ZZZZ-SX</summary>
public class Material : BaseEntity
{
    public string MaterialCode { get; set; } = string.Empty;    // PK
    public string MaterialName { get; set; } = string.Empty;
    public string? MaterialCategory { get; set; }               // Mã nhóm vật tư chính
    public string Unit { get; set; } = string.Empty;            // ĐVT
    public string? TechnicalSpec { get; set; }                  // Quy cách kỹ thuật
    public string? QualityStandard { get; set; }                // ASTM, JIS...

    public ICollection<PriceBook> PriceBooks { get; set; } = [];
    public ICollection<ProjectInventory> ProjectInventories { get; set; } = [];
    public ICollection<WarehouseTransactionItem> TransactionItems { get; set; } = [];
}
