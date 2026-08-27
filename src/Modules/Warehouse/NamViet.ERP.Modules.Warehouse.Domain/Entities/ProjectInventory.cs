using NamViet.ERP.SharedKernel.Entities;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Tồn kho thực tế và kế hoạch của từng vật tư theo dự án</summary>
public class ProjectInventory : BaseEntity
{
    public int Id { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;

    public decimal PlannedImportQty { get; set; }
    public decimal PlannedExportQty { get; set; }
    public decimal ActualImportQty { get; set; }
    public decimal ActualExportQty { get; set; }
    /// <summary>Cache denormalized — nguồn sự thật: inventory_reservations (Holding).</summary>
    public decimal ReservedQty { get; set; }
    /// <summary>Ngưỡng tồn tối thiểu theo dự án × vật tư (0 = không cảnh báo).</summary>
    public decimal MinStockQty { get; set; }

    /// <summary>Optimistic concurrency token (PostgreSQL xmin).</summary>
    public uint RowVersion { get; set; }

    /// <summary>Tồn thực tế = nhập đã duyệt − xuất đã duyệt (gồm hoàn trả / chuyển kho đã duyệt).</summary>
    public decimal ActualInventoryQty => ActualImportQty - ActualExportQty;

    /// <summary>
    /// Khả dụng chưa trừ HL chờ duyệt = tồn thực tế − xuất/chuyển kho dự kiến.
    /// Cổng XK/CK/HL dùng <c>IInventoryDomainService.GetProjectAvailableQtyAsync</c> (có trừ phiếu HL tồn kho chờ duyệt).
    /// </summary>
    public decimal AvailableQty => ActualInventoryQty - PlannedExportQty;

    /// <summary>Tồn dự kiến = tồn thực tế + nhập/hoàn trả/chuyển kho dự kiến (không trừ xuất dự kiến).</summary>
    public decimal PlannedInventoryQty => ActualInventoryQty + PlannedImportQty;

    public Project Project { get; set; } = null!;
    public Material Material { get; set; } = null!;

    public void AddPlannedImport(decimal qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(qty);
        PlannedImportQty += qty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemovePlannedImport(decimal qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(qty);
        PlannedImportQty = Math.Max(0m, PlannedImportQty - qty);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddReserved(decimal qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(qty);
        ReservedQty += qty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveReserved(decimal qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(qty);
        ReservedQty -= qty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyActualImport(decimal qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(qty);
        ActualImportQty += qty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyActualExport(decimal qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(qty);
        ActualExportQty += qty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddPlannedExport(decimal qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(qty);
        PlannedExportQty += qty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemovePlannedExport(decimal qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(qty);
        PlannedExportQty = Math.Max(0m, PlannedExportQty - qty);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Hoàn trả vật tư chưa dùng hết về kho dự án — giảm xuất thực tế.</summary>
    public void ApplyExportReturn(decimal qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(qty);
        if (qty > ActualExportQty)
            throw new DomainRuleException(
                $"Không thể hoàn trả {qty} — xuất thực tế hiện tại {ActualExportQty}.");
        ActualExportQty -= qty;
        UpdatedAt = DateTime.UtcNow;
    }
}
