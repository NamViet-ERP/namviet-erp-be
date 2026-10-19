using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Danh mục vật tư dạng cây (Adjacency List), tối đa 5 cấp.</summary>
public class MaterialCategory : BaseEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public MaterialCategory? Parent { get; set; }
    public ICollection<MaterialCategory> Children { get; set; } = [];
    public ICollection<Material> Materials { get; set; } = [];
}
