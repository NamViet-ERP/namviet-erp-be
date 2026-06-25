using NamViet.ERP.Api.Application.Dtos.Admin;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Identity.Domain.Entities;

namespace NamViet.ERP.Api.Application.Services.Admin;

public interface IAdminUserQueryService
{
    Task<AdminUserPagedResponse> ListAsync(AdminUserListQuery query, CancellationToken ct = default);

    Task<AdminUserDto?> GetByIdAsync(string id, string source, CancellationToken ct = default);

    Task<AdminUserListSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    Task<AdminUserDto> MapEmployeeRowAsync(Employee employee, ApplicationUser linked, CancellationToken ct);

    Task<AdminUserDto> MapManualRowAsync(
        ApplicationUser user,
        CancellationToken ct,
        string? department = null,
        string? position = null);
}
