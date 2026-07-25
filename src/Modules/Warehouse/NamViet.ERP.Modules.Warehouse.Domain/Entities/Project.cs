using NamViet.ERP.SharedKernel.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Dự án thi công hoặc Kho tổng – đóng vai trò "kho" trong hệ thống</summary>
public class Project : BaseEntity
{
    public string ProjectCode { get; set; } = string.Empty;    // PK – VD: DA-MAY-01
    public string ProjectName { get; set; } = string.Empty;
    public ProjectType ProjectType { get; set; } = ProjectType.Project;
    public Guid PmId { get; set; }                              // FK → identity.users
    public string PmName { get; set; } = string.Empty;         // Denormalized display
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal MaterialBudget { get; set; } = 0m;
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    /// <summary>Client-facing project identifier (e.g. proj-xxx from UI). Falls back to <see cref="ProjectCode"/> when null.</summary>
    public string? ClientProjectId { get; set; }
    public string? Stage { get; set; }

    public ICollection<ProjectInventory> Inventories { get; set; } = [];
    public ICollection<WarehouseTransaction> Transactions { get; set; } = [];
    public ICollection<ProjectWarehouseKeeper> WarehouseKeepers { get; set; } = [];
}
