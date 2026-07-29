using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Nhóm nhà cung cấp (theo ngành / loại). Một NCC có thể thuộc nhiều nhóm.</summary>
public class SupplierGroup : BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<SupplierGroupMember> Members { get; set; } = [];
}

/// <summary>Bảng nối NCC ↔ nhóm NCC.</summary>
public class SupplierGroupMember
{
    public int Id { get; set; }
    public int SupplierGroupId { get; set; }
    public int SupplierId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SupplierGroup? Group { get; set; }
    public Supplier? Supplier { get; set; }
}
