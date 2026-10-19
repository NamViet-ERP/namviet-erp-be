using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class WarehouseInventory : BaseEntity
{
    public int Id { get; set; }
    public string WarehouseId { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;
    public decimal Qty { get; set; }

    /// <summary>Optimistic concurrency token (PostgreSQL xmin).</summary>
    public uint RowVersion { get; set; }

    public WmsWarehouse Warehouse { get; set; } = null!;
    public Material Material { get; set; } = null!;
}
