using NamViet.ERP.SharedKernel.Entities;
using NamViet.ERP.Modules.Hr.Domain.Enums;

namespace NamViet.ERP.Modules.Hr.Domain.Entities;

/// <summary>Bản ghi đóng BHXH theo tháng</summary>
public class SocialInsuranceRecord : BaseEntity
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal SalaryBase { get; set; }
    public decimal EmployeeAmount { get; set; }
    public decimal CompanyAmount { get; set; }
    public string Status { get; set; } = "Pending";    // Pending/Paid

    public Employee Employee { get; set; } = null!;
}

/// <summary>Công việc BHXH cần xử lý</summary>
public class SocialInsuranceTask : BaseEntity
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;       // Tham gia/Thôi/Điều chỉnh...
    public int EmployeeId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateOnly? DueDate { get; set; }
    /// <summary>NV HR phụ trách xử lý (FK → hr.employees), không phải tài khoản login.</summary>
    public int? AssigneeId { get; set; }
    public string? Note { get; set; }

    public Employee Employee { get; set; } = null!;
    public Employee? Assignee { get; set; }
}

/// <summary>Khen thưởng nhân viên</summary>
public class Reward : BaseEntity
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public RewardType Type { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public DateOnly Date { get; set; }
    public string? DecisionNo { get; set; }

    public Employee Employee { get; set; } = null!;
}

// ── KPI (dùng chung cho: tuyển dụng / BHXH / chế độ / truyền thông) ──────
public class KpiDefinition : BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;  // recruitment/insurance/benefit/comms
    public string? Unit { get; set; }
    public decimal? Target { get; set; }
}

public class KpiRecord : BaseEntity
{
    public int Id { get; set; }
    public int KpiId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public int? OwnerId { get; set; }                       // FK → employees
    public int? DepartmentId { get; set; }
    public decimal TargetValue { get; set; }
    public decimal ActualValue { get; set; }
    public string? Note { get; set; }

    public KpiDefinition Kpi { get; set; } = null!;
}

// ── Công cụ hành chính ─────────────────────────────────────────────────────
public class AdminRequest : BaseEntity
{
    public int Id { get; set; }
    public AdminRequestType Type { get; set; }
    public int EmployeeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public decimal? Quantity { get; set; }
    public string Status { get; set; } = "Pending";
    public int? ApproverId { get; set; }

    public Employee Employee { get; set; } = null!;
}

public class Notification : BaseEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Target { get; set; }                     // All/Department/Employee
    public Guid CreatedById { get; set; }
}
