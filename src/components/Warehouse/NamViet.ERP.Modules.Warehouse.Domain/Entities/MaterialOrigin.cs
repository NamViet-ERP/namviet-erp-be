namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Xuất xứ vật tư — dùng cho dropdown khi khai báo vật tư.</summary>
public class MaterialOrigin
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Region { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
