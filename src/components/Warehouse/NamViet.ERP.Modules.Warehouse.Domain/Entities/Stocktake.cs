using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class Stocktake : BaseEntity
{
    public string Id { get; set; } = string.Empty;
    public string VoucherCode { get; set; } = string.Empty;
    public DateOnly VoucherDate { get; set; }
    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string WarehouseId { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string Condition { get; set; } = "counting";
    public bool IsFrozen { get; set; }
    public string Note { get; set; } = string.Empty;

    public static string GenerateVoucherCode(int sequence) => $"KK-{sequence:D4}";

    public WmsWarehouse? Warehouse { get; set; }
    public ICollection<StocktakeItem> Items { get; set; } = [];
}

public class StocktakeItem
{
    public int Id { get; set; }
    public string StocktakeId { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal SystemQty { get; set; }
    public decimal ActualQty { get; set; }
    public decimal Diff { get; set; }
    public string? Reason { get; set; }

    public Stocktake Stocktake { get; set; } = null!;
    public Material? Material { get; set; }
}
