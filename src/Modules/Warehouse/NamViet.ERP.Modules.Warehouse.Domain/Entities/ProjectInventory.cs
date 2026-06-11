using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Tồn kho thực tế và kế hoạch của từng vật tư theo dự án</summary>
public class ProjectInventory : BaseEntity
{
    public int Id { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;

    public decimal PlannedImportQty { get; set; } = 0m;    // Nhập dự kiến
    public decimal PlannedExportQty { get; set; } = 0m;    // Xuất dự kiến
    public decimal ActualImportQty { get; set; } = 0m;     // Nhập thực tế
    public decimal ActualExportQty { get; set; } = 0m;     // Xuất thực tế

    // Tính toán: actual_import - actual_export
    public decimal ActualInventoryQty => ActualImportQty - ActualExportQty;
    // Tính toán: planned_import - planned_export
    public decimal PlannedInventoryQty => PlannedImportQty - PlannedExportQty;

    public Project Project { get; set; } = null!;
    public Material Material { get; set; } = null!;
}
