namespace NamViet.ERP.Api.Application.Dtos.Hr;

public record DepartmentChildDto(
    int Id,
    string Code,
    string Name,
    bool IsActive,
    int EmployeeCount);

public record DepartmentDto(
    int Id,
    string Code,
    string Name,
    int? ParentId,
    string? Block,
    bool IsActive,
    int EmployeeCount,
    IReadOnlyList<DepartmentChildDto> Children);

public record DepartmentMemberDto(
    int Id,
    string Code,
    string FullName,
    string? Email,
    string? Phone,
    string Status,
    string? PositionName);
