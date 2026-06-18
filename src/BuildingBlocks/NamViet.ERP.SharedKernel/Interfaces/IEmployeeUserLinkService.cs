namespace NamViet.ERP.SharedKernel.Interfaces;

/// <summary>
/// Links Identity users to HR employee records (seed/provisioning flows).
/// </summary>
public record EmployeeUserLinkRequest(
    Guid UserId,
    string EmployeeCode,
    string FullName,
    string? Email,
    string? Phone,
    string? JoinedDate,
    string DepartmentCode,
    string DepartmentName);

public interface IEmployeeUserLinkService
{
    Task<int> EnsureDepartmentAsync(string code, string name, CancellationToken ct = default);

    Task UpsertFromUserAsync(EmployeeUserLinkRequest request, CancellationToken ct = default);
}
