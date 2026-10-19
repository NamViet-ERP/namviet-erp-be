using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class ProjectGanttSnapshot : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProjectCode { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public double PlanPct { get; set; }
    public double ActualPct { get; set; }
    public int ItemCount { get; set; }

    public Project? Project { get; set; }
    public ICollection<ProjectGanttSnapshotItem> Items { get; set; } = new List<ProjectGanttSnapshotItem>();
}
