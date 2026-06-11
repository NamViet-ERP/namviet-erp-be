using NamViet.ERP.SharedKernel.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>
/// Phiếu kho tổng hợp – ticket_code là PK (VD: NK-001, XK-002, MT-001...)
/// Bao gồm: Nhập / Xuất / Xuất mượn / Nhập trả mượn / Kết chuyển
/// </summary>
public class WarehouseTransaction : BaseEntity
{
    public string TicketCode { get; set; } = string.Empty;             // PK
    public TransactionType TransactionType { get; set; }
    public string? ProjectCode { get; set; }
    public int? SupplierId { get; set; }
    public string? InvoiceNumber { get; set; }                         // Số hóa đơn VAT
    public string? DnttNumber { get; set; }                            // Số đề nghị thanh toán
    public Guid CreatedById { get; set; }                              // FK → identity.users
    public string CreatedByName { get; set; } = string.Empty;         // Denormalized
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Notes { get; set; }

    public Project? Project { get; set; }
    public Supplier? Supplier { get; set; }
    public ICollection<WarehouseTransactionItem> Items { get; set; } = [];

    /// <summary>Sinh ticket_code tự động theo loại phiếu và số thứ tự</summary>
    public static string GenerateTicketCode(TransactionType type, int sequence)
    {
        var prefix = type switch
        {
            TransactionType.Import => "NK",
            TransactionType.Export => "XK",
            TransactionType.LoanExport => "MT",
            TransactionType.LoanImport => "TT",
            TransactionType.Transfer => "KC",
            _ => "PK"
        };
        return $"{prefix}-{sequence:D4}";
    }
}
