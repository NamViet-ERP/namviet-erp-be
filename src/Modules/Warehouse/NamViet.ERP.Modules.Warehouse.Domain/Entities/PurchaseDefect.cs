using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Phiếu hàng lỗi (HL) — ghi nhận hàng lỗi từ PO, không cộng tồn kho.</summary>
public class PurchaseDefect : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TicketCode { get; set; } = string.Empty;
    public string PoCode { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string Status { get; set; } = PurchaseDefectStatuses.Draft;
    public string? Reason { get; set; }
    public string? Note { get; set; }

    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }
    public ICollection<PurchaseDefectItem> Items { get; set; } = [];
    public ICollection<PurchaseDefectAttachment> Attachments { get; set; } = [];

    public static string GenerateTicketCode(int sequence) => $"HL-{sequence:D4}";
}

public class PurchaseDefectItem
{
    public int Id { get; set; }
    public string TicketCode { get; set; } = string.Empty;
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
