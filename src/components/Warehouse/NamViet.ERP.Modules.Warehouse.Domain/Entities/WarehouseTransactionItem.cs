namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Chi tiết dòng hàng trong phiếu kho</summary>
public class WarehouseTransactionItem
{
    public int Id { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;
    /// <summary>Tên tại thời điểm lập phiếu — không đổi khi sửa master.</summary>
    public string? MaterialNameSnapshot { get; set; }
    /// <summary>Đơn vị tại thời điểm lập phiếu.</summary>
    public string? UnitSnapshot { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>SL dự kiến khi chốt phiếu. Sau duyệt, <see cref="Quantity"/> = SL đạt vào tồn.</summary>
    public decimal PlannedQuantity { get; set; }
    /// <summary>SL đạt vào tồn (set khi duyệt thực tế).</summary>
    public decimal? AcceptedQty { get; set; }
    /// <summary>SL hàng lỗi → phiếu HL gắn NK.</summary>
    public decimal? DefectQty { get; set; }
    /// <summary>SL thiếu — còn trên PO, có báo cáo thiếu.</summary>
    public decimal? ShortageQty { get; set; }
    public string? DefectLineReason { get; set; }
    public string? ShortageLineReason { get; set; }
    public decimal UnitPrice { get; set; }                      // Đơn giá tại thời điểm nhập
    public decimal TotalAmount => Quantity * UnitPrice;         // Thành tiền (tính toán)
    public string? ConstructionPhase { get; set; }              // Cấu phần thi công (Móng, Thân, Ống...)
    public string? Notes { get; set; }
    public int? SupplierId { get; set; }
    /// <summary>NCC tại thời điểm lập dòng — mỗi VT có thể khác nhau.</summary>
    public string? SupplierName { get; set; }

    public WarehouseTransaction Transaction { get; set; } = null!;
    public Material Material { get; set; } = null!;
}
