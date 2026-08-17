namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>
/// Append-only activity / notification log — reusable across modules (purchasing, wms, …).
/// </summary>
public class ActivityEvent
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public Guid? ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;

    /// <summary>Logical module key, e.g. purchasing, wms.</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Stable action key, e.g. po.created, import.approved.</summary>
    public string Action { get; set; } = string.Empty;

    public string? EntityType { get; set; }
    public string? EntityCode { get; set; }

    /// <summary>Human-readable description shown in the feed.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Short badge label, e.g. Đơn đặt hàng / KHMH / Phiếu nhập hàng.</summary>
    public string CategoryLabel { get; set; } = string.Empty;
}

public static class ActivityModules
{
    public const string Purchasing = "purchasing";
    public const string Wms = "wms";
}

public static class ActivityActions
{
    public const string PlanCreated = "plan.created";
    public const string PlanUpdated = "plan.updated";
    public const string PoCreated = "po.created";
    public const string PoUpdated = "po.updated";
    public const string PoApproved = "po.approved";
    public const string PoUnapproved = "po.unapproved";
    public const string PoPlaced = "po.placed";
    public const string ImportCreated = "import.created";
    public const string ImportSubmitted = "import.submitted";
    public const string ImportApproved = "import.approved";
    public const string PaymentCreated = "payment.created";
    public const string PaymentSubmitted = "payment.submitted";
    public const string PaymentPaid = "payment.paid";
    public const string PaymentCancelled = "payment.cancelled";
}

public static class ActivityCategories
{
    public const string Plan = "KHMH";
    public const string Order = "Đơn đặt hàng";
    public const string Import = "Phiếu nhập hàng";
    public const string Payment = "DNTT";
}
