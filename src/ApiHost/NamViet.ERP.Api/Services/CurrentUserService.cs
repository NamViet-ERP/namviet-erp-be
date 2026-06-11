using System.Security.Claims;
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

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
