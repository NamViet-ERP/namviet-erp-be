using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Phiếu hàng lỗi / bảo hành (HL) — từ PO hoặc từ tồn kho dự án.</summary>
public class PurchaseDefect : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TicketCode { get; set; } = string.Empty;
    /// <summary><see cref="PurchaseDefectSourceTypes"/> — po | stock.</summary>
    public string SourceType { get; set; } = PurchaseDefectSourceTypes.Po;
    /// <summary>Null khi SourceType = stock.</summary>
    public string? PoCode { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string Status { get; set; } = PurchaseDefectStatuses.Draft;
    public string? Reason { get; set; }
    public string? Note { get; set; }

    /// <summary><see cref="PurchaseDefectCauseSources"/> — sau khi duyệt.</summary>
    public string? CauseSource { get; set; }
    public string? SupplierFeedback { get; set; }
    /// <summary><see cref="PurchaseDefectHandlingMethods"/> — sau khi duyệt.</summary>
    public string? HandlingMethod { get; set; }
    public Guid? ProcessedById { get; set; }
    public string? ProcessedByName { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }
    public ICollection<PurchaseDefectItem> Items { get; set; } = [];
    public ICollection<PurchaseDefectAttachment> Attachments { get; set; } = [];

    public static string GenerateTicketCode(int sequence) => $"HL-{sequence:D4}";

    public bool IsFromPo =>
        string.Equals(SourceType, PurchaseDefectSourceTypes.Po, StringComparison.OrdinalIgnoreCase);

    public bool IsFromStock =>
        string.Equals(SourceType, PurchaseDefectSourceTypes.Stock, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// approved (chưa điền) | processing (điền dở) | processed (đủ 3 trường) — chỉ khi Status = approved.
    /// </summary>
    public string ResolveProcessingPhase()
    {
        if (!string.Equals(Status, PurchaseDefectStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            return Status;

        var filled = 0;
        if (!string.IsNullOrWhiteSpace(CauseSource)) filled++;
        if (!string.IsNullOrWhiteSpace(SupplierFeedback)) filled++;
        if (!string.IsNullOrWhiteSpace(HandlingMethod)) filled++;
        if (filled == 0) return PurchaseDefectProcessingPhases.Approved;
        if (filled < 3) return PurchaseDefectProcessingPhases.Processing;
        return PurchaseDefectProcessingPhases.Processed;
    }
}

public class PurchaseDefectItem
{
    public int Id { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    /// <summary>0 khi phiếu nguồn tồn kho (không gắn dòng PO).</summary>
    public int PoItemId { get; set; }
    public string? MaterialCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? LineReason { get; set; }

    public PurchaseDefect Defect { get; set; } = null!;
}

public class PurchaseDefectAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TicketCode { get; set; } = string.Empty;
    public string Kind { get; set; } = "photo";
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public PurchaseDefect? Defect { get; set; }
}

public static class PurchaseDefectStatuses
{
    public const string Draft = "draft";
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

public static class PurchaseDefectSourceTypes
{
    public const string Po = "po";
    public const string Stock = "stock";
}

public static class PurchaseDefectCauseSources
{
    public const string Supplier = "supplier";
    public const string Customer = "customer";
}

public static class PurchaseDefectHandlingMethods
{
    public const string WarrantyRepair = "warranty_repair";
    public const string Replace = "replace";
}

public static class PurchaseDefectProcessingPhases
{
    public const string Approved = "approved";
    public const string Processing = "processing";
    public const string Processed = "processed";
}
