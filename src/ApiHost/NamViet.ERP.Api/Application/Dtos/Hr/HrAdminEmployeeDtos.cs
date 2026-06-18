namespace NamViet.ERP.Api.Application.Dtos.Hr;

public record CreateEmployeeRequest(
    string Code, string FullName, string Gender, string BirthDate,
    string? Phone, string? Email, string? Cccd, string? CccdDate, string? CccdPlace,
    string? AddressPermanent, string? AddressCurrent,
    int DepartmentId, int? PositionId, int? ManagerId,
    string JoinDate, string Education,
    string? BankAccount, string? BankName, string? TaxCode, string? SiNumber, string? PhotoUrl);

public record UpdateEmployeeRequest(
    string FullName, string? Phone, string? Email, string? Cccd,
    string? AddressPermanent, string? AddressCurrent,
    int DepartmentId, int? PositionId, int? ManagerId, string Status,
    string? BankAccount, string? BankName, string? TaxCode, string? SiNumber, string? PhotoUrl);

public record CreateContractRequest(
    string ContractNo, string Type, string StartDate, string? EndDate,
    decimal Salary, decimal Allowance, string? FileUrl);

public record CreateAppointmentRequest(
    int? OldPositionId, int NewPositionId, string? DecisionNo,
    string EffectiveDate, string? Note);
