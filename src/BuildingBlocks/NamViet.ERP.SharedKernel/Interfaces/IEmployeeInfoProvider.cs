namespace NamViet.ERP.SharedKernel.Interfaces;

/// <summary>
/// Read-only HR employee lookup for cross-module auth flows (Identity → HR boundary).
/// </summary>
public record LinkedEmployeeInfo(
    int EmployeeId,
    string? DepartmentName,
    string ClientStatus);

public interface IEmployeeInfoProvider
{
    Task<LinkedEmployeeInfo?> GetByUserAsync(
        Guid userId, string? employeeCode, CancellationToken ct = default);
}
