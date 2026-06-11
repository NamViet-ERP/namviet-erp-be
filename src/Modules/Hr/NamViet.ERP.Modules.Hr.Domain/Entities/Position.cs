using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Hr.Domain.Entities;

public class Position : BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string? Level { get; set; }  // Cấp bậc

    public Department Department { get; set; } = null!;
    public ICollection<Employee> Employees { get; set; } = [];
}
