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
        PlannedImportQty -= qty;
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
}
