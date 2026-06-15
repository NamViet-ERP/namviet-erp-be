using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class Conversion : BaseEntity
{
    public string Id { get; set; } = string.Empty;
    public string VoucherCode { get; set; } = string.Empty;
    public DateOnly VoucherDate { get; set; }
    public string FromWarehouseId { get; set; } = string.Empty;
    public string FromWarehouseName { get; set; } = string.Empty;
    public string FromType { get; set; } = string.Empty;
    public string ToWarehouseId { get; set; } = string.Empty;
    public string ToWarehouseName { get; set; } = string.Empty;
    public string ToType { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public string Note { get; set; } = string.Empty;

    public ICollection<ConversionItem> Items { get; set; } = [];
}

public class ConversionItem
{
    public int Id { get; set; }
    public string ConversionId { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    public Conversion Conversion { get; set; } = null!;
    public Material? Material { get; set; }
}
