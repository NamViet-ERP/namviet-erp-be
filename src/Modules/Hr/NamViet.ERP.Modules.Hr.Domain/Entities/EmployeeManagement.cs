using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Hr.Domain.Entities;

public enum ContractType { Indefinite, Fixed12, Fixed6, Probation, Freelance }
public enum ContractStatus { Draft, Active, Expired, Terminated }

/// <summary>Hợp đồng lao động</summary>
public class LaborContract : BaseEntity
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string ContractNo { get; set; } = string.Empty;
    public ContractType Type { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal Salary { get; set; }
    public decimal Allowance { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Active;
    public DateOnly? SignedDate { get; set; }
    public string? FileUrl { get; set; }

    public Employee Employee { get; set; } = null!;
}

/// <summary>Bổ nhiệm – thay đổi chức vụ</summary>
public class Appointment : BaseEntity
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int? OldPositionId { get; set; }
    public int NewPositionId { get; set; }
    public string? DecisionNo { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Note { get; set; }

    public Employee Employee { get; set; } = null!;
}

/// <summary>Luân chuyển – thay đổi bộ phận</summary>
public class HrTransfer : BaseEntity
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int FromDepartmentId { get; set; }
    public int ToDepartmentId { get; set; }
    public string? DecisionNo { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Note { get; set; }

    public Employee Employee { get; set; } = null!;
}
