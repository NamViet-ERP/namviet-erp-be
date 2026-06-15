using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class PurchaseOrder : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string? ProjectCode { get; set; }
    public int? SupplierId { get; set; }
    public DateOnly OrderDate { get; set; }

    public Project? Project { get; set; }
    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; } = [];
}

public class PurchaseOrderItem
{
    public int Id { get; set; }
    public string PoCode { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;
    // Snapshot tại thời điểm đặt PO — không derive realtime từ Material
    public string Sku { get; set; } = string.Empty;
    // Snapshot tại thời điểm đặt PO — không derive realtime từ Material
    public string Name { get; set; } = string.Empty;
    public decimal EstPrice { get; set; }
    public decimal PoPrice { get; set; }
    public decimal Ordered { get; set; }
    public decimal Received { get; set; }
    // Snapshot tại thời điểm đặt PO — không derive realtime từ Material
    public string Unit { get; set; } = string.Empty;

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public Material? Material { get; set; }
}
