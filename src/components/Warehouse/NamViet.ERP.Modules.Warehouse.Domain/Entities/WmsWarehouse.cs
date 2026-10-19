using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Kho vật lý – khớp FE Warehouse interface</summary>
public class WmsWarehouse : BaseEntity
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Manager { get; set; } = string.Empty;
    public string Status { get; set; } = "active";

    public ICollection<WarehouseInventory> Inventories { get; set; } = [];
}
