using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>
/// Đánh giá NCC theo lần nhận hàng (phiếu NK duyệt thực tế) — khác đánh giá KPI theo kỳ.
/// </summary>
public class SupplierDeliveryEvaluation : BaseEntity
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string? SupplierNameSnapshot { get; set; }

    public string PoCode { get; set; } = string.Empty;
    public string ImportTicketCode { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }
    public DateTime DueAt { get; set; }
    public string Status { get; set; } = SupplierDeliveryEvaluationStatuses.Pending;

    public decimal? Score { get; set; }
    public string? Grade { get; set; }
    public decimal? OnTimeDeliveryRate { get; set; }
    public decimal? DefectRate { get; set; }
    public decimal? WarrantyCompletionRate { get; set; }
    public string? Note { get; set; }

    public Guid? EvaluatedById { get; set; }
    public string? EvaluatedByName { get; set; }
    public DateOnly? EvaluatedAt { get; set; }

    public Supplier? Supplier { get; set; }
}

public static class SupplierDeliveryEvaluationStatuses
{
    public const string Pending = "pending";
    public const string Completed = "completed";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Pending, Completed,
    };
}
