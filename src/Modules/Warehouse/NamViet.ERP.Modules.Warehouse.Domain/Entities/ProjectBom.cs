using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class ProjectBom : BaseEntity
{
    public int Id { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;
    public decimal Qty { get; set; }

    public Project? Project { get; set; }
    public Material? Material { get; set; }
}
