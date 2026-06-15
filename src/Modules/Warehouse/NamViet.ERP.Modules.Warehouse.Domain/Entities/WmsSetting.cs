using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class WmsSetting : BaseEntity
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = "{}";
}
