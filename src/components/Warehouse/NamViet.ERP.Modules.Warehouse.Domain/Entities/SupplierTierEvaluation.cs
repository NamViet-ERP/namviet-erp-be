namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class SupplierTierEvaluation
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string? OldGrade { get; set; }
    public string NewGrade { get; set; } = string.Empty;
    public decimal TotalScore { get; set; }
    public decimal PriceScore { get; set; }
    public decimal ProfileScore { get; set; }
    public decimal PaymentScore { get; set; }
    public decimal ServiceScore { get; set; }
    public string? EvaluationDetailsJson { get; set; }
    public string? Reason { get; set; }
    public string? EvaluatedById { get; set; }
    public string? EvaluatedByName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Supplier Supplier { get; set; } = null!;
}
