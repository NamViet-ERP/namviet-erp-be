namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Nhóm nước / khu vực xuất xứ (G7, EU, ĐNA, Nhóm 1, Châu Á, Khác...).</summary>
public class MaterialRegion
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
