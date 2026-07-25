using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Vật tư – material_code format: XX-YYY-ZZZZ-SX</summary>
public class Material : BaseEntity
{
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? TechnicalSpec { get; set; }
    public string? QualityStandard { get; set; }
    public bool IsConsumable { get; set; }
    /// <summary>false = ngừng sử dụng; vẫn giữ lịch sử dự án/phiếu.</summary>
    public bool IsActive { get; set; } = true;

    public MaterialCategory Category { get; set; } = null!;
    public ICollection<ProjectInventory> ProjectInventories { get; set; } = [];
    public ICollection<WarehouseTransactionItem> TransactionItems { get; set; } = [];
}
