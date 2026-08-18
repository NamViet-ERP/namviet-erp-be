using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Hr.Domain.Entities;

public enum EmployeeStatus { Active, OnLeave, Resigned, Terminated }
public enum Gender { Male, Female, Other }
public enum Education { University, College, HighSchool, Other }

public class Employee : BaseEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;           // Mã NV – VD: NV-0001
    public Guid? UserId { get; set; }                           // Canonical login: identity.users.Id
    public string FullName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly BirthDate { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Cccd { get; set; }                           // CCCD
    public DateOnly? CccdDate { get; set; }
    public string? CccdPlace { get; set; }
    public string? AddressPermanent { get; set; }
    public string? AddressCurrent { get; set; }
    /// <summary>Null = chưa phân phòng (vd. vừa onboard công khai).</summary>
    public int? DepartmentId { get; set; }
    public int? PositionId { get; set; }
    public int? ManagerId { get; set; }
    public DateOnly JoinDate { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public string? BankAccount { get; set; }
    public string? BankName { get; set; }
    public string? TaxCode { get; set; }
    public string? SiNumber { get; set; }                       // Số BHXH
    public string? PhotoUrl { get; set; }
    public Education Education { get; set; }
    public string? Segment { get; set; }
    public string? Branch { get; set; }
    public DateOnly? OfficialDate { get; set; }
    public DateOnly? ResignDate { get; set; }
    public string? MaritalStatus { get; set; }
    public string? CmndNumber { get; set; }
    public string? CmndIssueDate { get; set; }
    public string? CmndIssuePlace { get; set; }
    public string? CccdBackUrl { get; set; }
    public string? CccdFrontUrl { get; set; }
    public string? School { get; set; }
    public string? Major { get; set; }
    public string? DegreePhotoUrl { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankBranch { get; set; }
    public string? HadSocialInsurance { get; set; }

    public Department? Department { get; set; }
    public Position? Position { get; set; }
    public Employee? Manager { get; set; }
    public ICollection<LaborContract> Contracts { get; set; } = [];
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = [];
}
