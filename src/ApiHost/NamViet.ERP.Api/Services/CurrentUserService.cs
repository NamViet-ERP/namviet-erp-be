using System.Security.Claims;
using NamViet.ERP.SharedKernel.Authorization;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Services;

public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid Id => Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User?.FindFirstValue("sub"), out var id) ? id : Guid.Empty;

    public string Email => User?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    public string Name => User?.FindFirstValue("name") ?? string.Empty;
    public string EmployeeCode => User?.FindFirstValue("employee_code") ?? string.Empty;

    public int? EmployeeId =>
        int.TryParse(User?.FindFirstValue("employee_id"), out var id) ? id : null;

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public IReadOnlyList<string> Permissions =>
        User?.FindAll(PermissionCatalog.ClaimType).Select(c => c.Value).ToList() ?? [];

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
    public bool HasPermission(string permission) =>
        User?.HasClaim(PermissionCatalog.ClaimType, permission) ?? false;
}
