using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class Supplier : BaseEntity
{
    public int Id { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public int MaxDebtDays { get; set; } = 30;
    public string Rating { get; set; } = "A";                  // A, B, C

    public ICollection<PriceBook> PriceBooks { get; set; } = [];
    public ICollection<WarehouseTransaction> Transactions { get; set; } = [];
}
