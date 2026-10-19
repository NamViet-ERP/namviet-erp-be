using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class ProjectGanttSnapshotItem : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SnapshotId { get; set; }
    
    // We store the TaskId just in case, but rely heavily on Name/WbsCode for historical matching
    public string TaskId { get; set; } = string.Empty; 
    public string TaskName { get; set; } = string.Empty;
    public double PlanPct { get; set; }
    public double ActualPct { get; set; }
    
    public ProjectGanttSnapshot? Snapshot { get; set; }
}
