using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>
/// Danh mục dịch vụ thi công, thầu phụ và chi phí ngoài dự án EPC.
/// Không quản lý tồn kho vật lý nhưng bắt buộc có mã định danh chuẩn để kiểm soát KHMH, PO và DNTT.
/// </summary>
public class ServiceExpenseItem : BaseEntity
{
    public int Id { get; set; }
    /// <summary>Mã định danh duy nhất (VD: DV-VC-01, CP-TK-01)</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Tên dịch vụ / chi phí ngoài</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Phân loại: "service" (Dịch vụ) | "expense" (Chi phí ngoài)</summary>
    public string Type { get; set; } = "service";
    /// <summary>Đơn vị tính (Công, Chuyến, Lần, Gói, Tháng, Hệ...)</summary>
    public string Unit { get; set; } = string.Empty;
    /// <summary>Đơn giá tham chiếu (VNĐ)</summary>
    public decimal? ReferencePrice { get; set; }
    /// <summary>Ghi chú / Diễn giải mục đích</summary>
    public string? Description { get; set; }
    /// <summary>Trạng thái kích hoạt (false = ngừng dùng)</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>Thứ tự hiển thị</summary>
    public int SortOrder { get; set; } = 0;
}
