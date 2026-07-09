using NamViet.ERP.SharedKernel.Entities;

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

    public decimal ActualInventoryQty => ActualImportQty - ActualExportQty;
    public decimal PlannedInventoryQty => PlannedImportQty - PlannedExportQty;
    public decimal ExpectedInventoryQty => ActualInventoryQty + PlannedInventoryQty;

    public Project Project { get; set; } = null!;
    public Material Material { get; set; } = null!;

    public void AddPlannedImport(decimal qty)
    {
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
        PlannedImportQty += qty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemovePlannedImport(decimal qty)
    {
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
        PlannedImportQty = Math.Max(0m, PlannedImportQty - qty);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddReserved(decimal qty)
    {
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
        ReservedQty += qty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveReserved(decimal qty)
    {
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
        ReservedQty -= qty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyActualImport(decimal qty)
    {
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
        ActualImportQty += qty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyActualExport(decimal qty)
    {
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
        ActualExportQty += qty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddPlannedExport(decimal qty)
    {
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
        PlannedExportQty += qty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemovePlannedExport(decimal qty)
    {
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
        PlannedExportQty = Math.Max(0m, PlannedExportQty - qty);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Hoàn trả vật tư chưa dùng hết về kho dự án — giảm xuất thực tế.</summary>
    public void ApplyExportReturn(decimal qty)
    {
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
        if (qty > ActualExportQty)
            throw new InvalidOperationException(
                $"Không thể hoàn trả {qty} — xuất thực tế hiện tại {ActualExportQty}.");
        ActualExportQty -= qty;
        UpdatedAt = DateTime.UtcNow;
    }
}
