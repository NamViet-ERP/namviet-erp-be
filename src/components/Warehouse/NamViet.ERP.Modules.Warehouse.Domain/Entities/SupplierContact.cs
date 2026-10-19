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

    /// <summary>primary | secondary | backup. Tối đa một primary / NCC.</summary>
    public string Role { get; set; } = "secondary";

    /// <summary>Legacy mirror of <see cref="Role"/> == primary. Kept for DB back-compat.</summary>
    public bool IsPrimary { get; set; }

    public Supplier? Supplier { get; set; }
}
