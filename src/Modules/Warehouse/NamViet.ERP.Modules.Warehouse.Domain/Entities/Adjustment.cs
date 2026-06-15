using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class Adjustment : BaseEntity
{
    public string Id { get; set; } = string.Empty;
    public string VoucherCode { get; set; } = string.Empty;
    public DateOnly VoucherDate { get; set; }
    public string StocktakeCode { get; set; } = string.Empty;
    public string Type { get; set; } = "increase";
    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string Condition { get; set; } = "new";
    public string WarehouseId { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public bool IsNew { get; set; } = true;

    public WmsWarehouse? Warehouse { get; set; }
    public ICollection<AdjustmentItem> Items { get; set; } = [];
}

public class AdjustmentItem
{
    public int Id { get; set; }
    public string AdjustmentId { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal AdjustQty { get; set; }
    public string Reason { get; set; } = string.Empty;

    public Adjustment Adjustment { get; set; } = null!;
    public Material? Material { get; set; }
}
