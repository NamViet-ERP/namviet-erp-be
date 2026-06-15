using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class ExpectedImport : BaseEntity
{
    public string Id { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal Surplus { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateOnly ExpectedDate { get; set; }

    public Project? Project { get; set; }
    public Material? Material { get; set; }
}
