namespace NamViet.ERP.Api.Application.Dtos.Admin;

public record AdminUserDto(
    string Id,
    string ToggleId,
    string? AccountUserId,
    string Source,
    bool HasAccount,
    string EmployeeCode,
    string FullName,
    string Email,
    string Phone,
    string Department,
    string Position,
    string Role,
    string RoleCode,
    string StatusLabel,
    bool IsActive,
    bool CanToggle,
    bool CanProvision,
    string CreatedAt);

public record AdminUserHrProfileDto(
    string? Gender,
    string? BirthDate,
    string? PersonalPhone,
    string? PersonalEmail,
    string? HrStatus,
    string? JoinDate);

public record AdminUserDetailDto(AdminUserDto User, AdminUserHrProfileDto? HrProfile);

public record UpdateAdminUserRoleRequest(string Role);

public record ProvisionAccountRequest(string EmployeeId, string Role, string Password);

public record CreateSystemAccountRequest(
    string Name,
    string Email,
    string? Phone,
    string? EmployeeCode,
    string? Department,
    string? Position,
    string Role,
    string Password);

public record UpdateAdminUserStatusRequest(bool Enabled);

public record ResetAdminPasswordRequest(string Password);

public record AdminUserListSummaryDto(int Total, int Pending, int Active, int Inactive);

public record AdminUserPagedResponse(
    IReadOnlyList<AdminUserDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    AdminUserListSummaryDto Summary);

public record AdminRoleDto(string Code, string Label, bool IsActive);

public record CreateAdminRoleRequest(string Label);

public record UpdateAdminRoleRequest(string Label);
