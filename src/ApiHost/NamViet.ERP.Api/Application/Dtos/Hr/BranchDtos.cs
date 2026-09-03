namespace NamViet.ERP.Api.Application.Dtos.Hr;

public record BranchDto(
    int Id,
    string Code,
    string Name,
    bool IsActive,
    int EmployeeCount);

public record UpsertBranchRequest(string Code, string Name);
