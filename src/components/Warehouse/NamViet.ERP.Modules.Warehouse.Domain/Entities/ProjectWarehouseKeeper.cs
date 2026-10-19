namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Thủ kho được gán phụ trách một kho dự án (nhiều user / dự án).</summary>
public class ProjectWarehouseKeeper
{
    public int Id { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    public Project? Project { get; set; }
}
