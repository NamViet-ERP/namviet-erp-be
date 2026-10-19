using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Báo giá chào thầu (RFQ) từ NCC — nhập tay, không lấy từ KHMH.</summary>
public class PurchaseRfqQuote : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? ProjectCode { get; set; }
    public DateOnly QuoteDate { get; set; }
    public decimal UnitPrice { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public int? LeadTimeDays { get; set; }
    public string? PaymentTerm { get; set; }
    /// <summary><see cref="PurchaseRfqQuoteStatuses"/>.</summary>
    public string Status { get; set; } = PurchaseRfqQuoteStatuses.Active;
    public string? PoRef { get; set; }
    public string? Note { get; set; }
}

public static class PurchaseRfqQuoteStatuses
{
    public const string Active = "active";
    public const string Won = "won";
    public const string Lost = "lost";
    public const string Expired = "expired";

    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Active, Won, Lost, Expired,
    };
}
