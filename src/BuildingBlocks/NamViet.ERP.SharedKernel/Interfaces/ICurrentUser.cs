namespace NamViet.ERP.SharedKernel.Interfaces;

public interface ICurrentUser
{
    Guid Id { get; }
    string Email { get; }
    string Name { get; }
    string EmployeeCode { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}
