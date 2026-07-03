using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.SharedKernel.Authorization;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Tests.Authorization;

public class PermissionDataScopeTests
{
    [Fact]
    public void IsOwnScopeOnly_when_user_has_own_without_all()
    {
        var user = new TestCurrentUser(["hrm.employees.view:own"]);
        Assert.True(PermissionDataScope.IsOwnScopeOnly(
            user,
            EmployeeAccessService.ViewAllPermission,
            EmployeeAccessService.ViewOwnPermission));
    }

    [Fact]
    public void IsOwnScopeOnly_false_when_user_has_all()
    {
        var user = new TestCurrentUser(["hrm.employees.view:all", "hrm.employees.view:own"]);
        Assert.False(PermissionDataScope.IsOwnScopeOnly(
            user,
            EmployeeAccessService.ViewAllPermission,
            EmployeeAccessService.ViewOwnPermission));
    }
}

public class EmployeeAccessServiceTests
{
    [Fact]
    public async Task CanViewEmployeeAsync_allows_all_scope_for_any_employee()
    {
        var service = new EmployeeAccessService(new StubEmployeeInfoProvider(null));
        var user = new TestCurrentUser(["hrm.employees.view:all"], employeeId: 10);

        Assert.True(await service.CanViewEmployeeAsync(user, 99));
    }

    [Fact]
    public async Task CanViewEmployeeAsync_allows_own_scope_only_for_linked_employee()
    {
        var service = new EmployeeAccessService(new StubEmployeeInfoProvider(42));
        var user = new TestCurrentUser(["hrm.employees.view:own"], employeeId: 42);

        Assert.True(await service.CanViewEmployeeAsync(user, 42));
        Assert.False(await service.CanViewEmployeeAsync(user, 43));
    }

    [Fact]
    public async Task CanViewEmployeeAsync_denies_without_permissions()
    {
        var service = new EmployeeAccessService(new StubEmployeeInfoProvider(42));
        var user = new TestCurrentUser([], employeeId: 42);

        Assert.False(await service.CanViewEmployeeAsync(user, 42));
    }
}

internal sealed class TestCurrentUser(IReadOnlyList<string> permissions, int? employeeId = null) : ICurrentUser
{
    public Guid Id { get; } = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    public string Email => "staff@namviet.vn";
    public string Name => "Staff User";
    public string EmployeeCode => "NV-042";
    public int? EmployeeId { get; } = employeeId;
    public IReadOnlyList<string> Roles => ["Staff"];
    public IReadOnlyList<string> Permissions { get; } = permissions;
    public bool IsAuthenticated => true;
    public bool IsInRole(string role) => Roles.Contains(role);
    public bool HasPermission(string permission) => Permissions.Contains(permission);
}

internal sealed class StubEmployeeInfoProvider(int? employeeId) : IEmployeeInfoProvider
{
    public Task<LinkedEmployeeInfo?> GetByUserAsync(Guid userId, string? employeeCode, CancellationToken ct = default)
    {
        if (!employeeId.HasValue) return Task.FromResult<LinkedEmployeeInfo?>(null);
        return Task.FromResult<LinkedEmployeeInfo?>(
            new LinkedEmployeeInfo(employeeId.Value, "HR", "Đang làm"));
    }
}
