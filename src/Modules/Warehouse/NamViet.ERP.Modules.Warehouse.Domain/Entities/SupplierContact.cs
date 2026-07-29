using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Người liên hệ của nhà cung cấp.</summary>
public class SupplierContact : BaseEntity
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Note { get; set; }

    /// <summary>Liên hệ chính — tối đa một liên hệ chính cho mỗi NCC.</summary>
    public bool IsPrimary { get; set; }

    public Supplier? Supplier { get; set; }
}
