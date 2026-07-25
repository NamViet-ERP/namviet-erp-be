namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class PurchasePlanLineQuote
{
    public int Id { get; set; }
    public int PlanLineId { get; set; }
    public int SortOrder { get; set; }

    public decimal? KgPerUnit { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Amount { get; set; }
    public string? SupplierName { get; set; }
    public string? Note { get; set; }
    public string? Status { get; set; }

    public PurchasePlanLine PlanLine { get; set; } = null!;
}
