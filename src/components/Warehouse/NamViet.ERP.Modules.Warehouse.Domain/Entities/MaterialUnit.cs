namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Đơn vị tính chuẩn — dùng cho dropdown khi khai báo vật tư.</summary>
public class MaterialUnit
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
