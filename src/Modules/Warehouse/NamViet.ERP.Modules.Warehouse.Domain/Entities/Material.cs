using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Vật tư – material_code format: XX-YYY-ZZZZ-SX</summary>
public class Material : BaseEntity
{
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string Unit { get; set; } = string.Empty;
    /// <summary>Quy cách (ngắn).</summary>
    public string? TechnicalSpec { get; set; }
    /// <summary>Thông số kỹ thuật chung — nhập tay dạng văn bản.</summary>
    public string? GeneralTechnicalSpec { get; set; }
    public string? QualityStandard { get; set; }
    /// <summary>Giá trần tham chiếu — cảnh báo khi mua vượt.</summary>
    public decimal? CeilingPrice { get; set; }
    /// <summary>A / B / C — mức độ quan trọng / giá trị.</summary>
    public string? AbcClass { get; set; }
    public bool IsConsumable { get; set; }
    /// <summary>false = ngừng sử dụng; vẫn giữ lịch sử dự án/phiếu.</summary>
    public bool IsActive { get; set; } = true;

    public MaterialCategory Category { get; set; } = null!;
    public ICollection<ProjectInventory> ProjectInventories { get; set; } = [];
    public ICollection<WarehouseTransactionItem> TransactionItems { get; set; } = [];
}
