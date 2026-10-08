using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Hạng mục thi công trên lịch Gantt của một dự án.</summary>
public class ProjectConstructionTask : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProjectCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public int PercentComplete { get; set; }
    public int SortOrder { get; set; }

    public string WbsCode { get; set; } = string.Empty;
    public int Level { get; set; } = 0;
    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; } = false;
    public string? Department { get; set; }
    public string? StatusLabel { get; set; }
    public string? StatusTone { get; set; }
    public string? Notes { get; set; }

    public Project? Project { get; set; }
}
