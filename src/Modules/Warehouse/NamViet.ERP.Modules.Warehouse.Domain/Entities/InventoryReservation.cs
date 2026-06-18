using NamViet.ERP.SharedKernel.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Giữ kho (soft-booking) từ kho tổng cho dự án, liên kết phiếu nhập dự kiến.</summary>
public class InventoryReservation : BaseEntity
{
    public int Id { get; set; }
    public string WarehouseId { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;
    public string SourceTicketCode { get; set; } = string.Empty;
    /// <summary>Số lượng giữ từ tồn kho tổng.</summary>
    public decimal ReservedQty { get; set; }
    /// <summary>Phần thiếu dự kiến nhập từ NCC bên ngoài (không trừ kho tổng).</summary>
    public decimal ExternalQty { get; set; }
    public decimal FulfilledQty { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Holding;
    public DateTime? FulfilledAt { get; set; }

    public WmsWarehouse Warehouse { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public Material Material { get; set; } = null!;
    public WarehouseTransaction SourceTransaction { get; set; } = null!;

    public decimal RequestedQty => ReservedQty + ExternalQty;
}
