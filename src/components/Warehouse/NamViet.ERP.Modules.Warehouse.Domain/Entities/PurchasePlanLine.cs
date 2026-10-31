namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class PurchasePlanLine
{
    public int Id { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public int LineNo { get; set; }

    public string LineType { get; set; } = PurchasePlanLineTypes.Material;

    public string? SystemName { get; set; }
    public string? Category { get; set; }
    /// <summary>Mã vật tư danh mục kho — bắt buộc khi tạo/import dòng mới nếu là material.</summary>
    public string? MaterialCode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Spec { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? KgPerUnit { get; set; }
    public string? LineNote { get; set; }

    public string? PurchaseStatus { get; set; }
    public string? StatusNote { get; set; }

    public decimal? BudgetUnitPrice { get; set; }
    public decimal? BudgetAmount { get; set; }

    public decimal? ProposalKgPerUnit { get; set; }
    public decimal? ProposalUnitPrice { get; set; }
    public decimal? ProposalAmount { get; set; }
    public string? ProposalSupplierName { get; set; }
    public string? ProposalNote { get; set; }
    public string? ProposalStatus { get; set; }

    public DateOnly? RequiredDate { get; set; }
    public DateOnly? OrderedDate { get; set; }
    public DateOnly? ExpectedDeliveryDate { get; set; }
    public DateOnly? ActualReceiptDate { get; set; }
    public string? DeliveryNote { get; set; }

    public decimal? PurchaseKgPerUnit { get; set; }
    public decimal? PurchaseUnitPrice { get; set; }
    public decimal? PurchaseAmount { get; set; }
    public string? PurchaseSupplierName { get; set; }
    public string? PurchaseNote { get; set; }

    public decimal? SelectedUnitPrice { get; set; }
    public decimal? SelectedAmount { get; set; }
    public string? SelectedSupplierName { get; set; }
    public string? SelectionReason { get; set; }

    public decimal OrderedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal DefectQty { get; set; }

    public PurchasePlan Plan { get; set; } = null!;
    public ICollection<PurchasePlanLineQuote> Quotes { get; set; } = [];
    public ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = [];

    public decimal RemainingOrderableQty => Math.Max(0m, Quantity - OrderedQty);
}

public static class PurchasePlanLineTypes
{
    public const string Material = "material";
    public const string Service = "service";
    public const string Expense = "expense";

    public static bool IsValid(string? type) =>
        type is Material or Service or Expense;
}

public static class PurchaseLineStatuses
{
    public const string NotPurchased = "chưa mua";
    public const string Ordered = "đã đặt";
    public const string PartialDelivery = "giao 1 phần";
    public const string Delivered = "đã giao";
}
