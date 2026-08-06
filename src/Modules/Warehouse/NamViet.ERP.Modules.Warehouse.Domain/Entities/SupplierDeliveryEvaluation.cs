using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>
/// Đánh giá NCC theo lần nhận hàng (phiếu NK duyệt thực tế) — 5 tiêu chí thang 1–10.
/// </summary>
public class SupplierDeliveryEvaluation : BaseEntity
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string? SupplierNameSnapshot { get; set; }

    public string PoCode { get; set; } = string.Empty;
    public string ImportTicketCode { get; set; } = string.Empty;
    public string? ProjectCode { get; set; }

    public DateTime ReceivedAt { get; set; }
    public DateTime DueAt { get; set; }
    public string Status { get; set; } = SupplierDeliveryEvaluationStatuses.Pending;

    /// <summary>Chất lượng (1–10).</summary>
    public decimal? Quality { get; set; }

    /// <summary>Tiến độ giao hàng (1–10).</summary>
    public decimal? Delivery { get; set; }

    /// <summary>Giá cả (1–10).</summary>
    public decimal? Price { get; set; }

    /// <summary>Dịch vụ (1–10).</summary>
    public decimal? Service { get; set; }

    /// <summary>Tuân thủ (1–10).</summary>
    public decimal? Compliance { get; set; }

    /// <summary>Điểm tổng có trọng số — BE tính.</summary>
    public decimal? Score { get; set; }

    /// <summary>Hạng A–H — BE tính.</summary>
    public string? Grade { get; set; }

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
