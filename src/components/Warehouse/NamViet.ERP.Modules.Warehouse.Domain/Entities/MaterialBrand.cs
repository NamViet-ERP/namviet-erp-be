namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Nhãn hiệu / Hãng sản xuất vật tư (Hòa Phát, Wilo, Ebara, Posco, System Fan...).</summary>
public class MaterialBrand
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
