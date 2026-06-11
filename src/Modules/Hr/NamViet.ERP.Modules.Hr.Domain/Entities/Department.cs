using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Hr.Domain.Entities;

public class Department : BaseEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public string? Block { get; set; }  // Khối: HCNS, CUNG ỨNG, IT, KẾ TOÁN...
    public bool IsActive { get; set; } = true;

    public Department? Parent { get; set; }
    public ICollection<Department> Children { get; set; } = [];
    public ICollection<Position> Positions { get; set; } = [];
    public ICollection<Employee> Employees { get; set; } = [];
}
