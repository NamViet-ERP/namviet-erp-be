using NamViet.ERP.SharedKernel.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>
/// Phiếu kho tổng hợp – ticket_code là PK (VD: NK-001, XK-002, MT-001...)
/// Bao gồm: Nhập / Xuất / Xuất mượn / Nhập trả mượn / Kết chuyển
/// </summary>
public class WarehouseTransaction : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
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

    // FE WMS facade fields
    public string? WarehouseId { get; set; }
    public string? FromWarehouseId { get; set; }
    public string? ToWarehouseId { get; set; }
    public string? ReferenceCode { get; set; }
    public string? SoCode { get; set; }
    public string? Customer { get; set; }
    public string? WarehouseKeeper { get; set; }
    public WarehouseKeeperApprovalStatus WarehouseApprovalStatus { get; set; } = WarehouseKeeperApprovalStatus.Pending;
    public bool IsDirectProcurement { get; set; }
    public string? BeneficiaryProjectCode { get; set; }
    public string? SupplierName { get; set; }
    /// <summary>Liên kết cặp phiếu NK↔XK cho Direct Issue.</summary>
    public string? LinkedTicketCode { get; set; }

    /// <summary>Optimistic concurrency token (PostgreSQL xmin).</summary>
    public uint RowVersion { get; set; }

    public Project? Project { get; set; }
    public Supplier? Supplier { get; set; }
    public WarehouseTransaction? LinkedTransaction { get; set; }
    public ICollection<WarehouseTransactionItem> Items { get; set; } = [];
    public ICollection<InventoryReservation> Reservations { get; set; } = [];

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
            TransactionType.ExportReturn => "HT",
            _ => "PK"
        };
        return $"{prefix}-{sequence:D4}";
    }
}
