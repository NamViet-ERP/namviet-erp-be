using System.Reflection;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Authorization;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Tests.Warehouse;

public class ProjectsControllerAuthorizationTests
{
    private class TestCurrentUser : ICurrentUser
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Procurement Staff";
        public string Email { get; set; } = "procurement@namviet.vn";
        public string EmployeeCode => "NV-CU01";
        public int? EmployeeId => 10;
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles { get; set; } = [];
        public IReadOnlyList<string> Permissions { get; set; } = [];
        public ClaimsPrincipal Principal => new();

        public bool IsInRole(string role) => Roles.Contains(role);
        public bool HasPermission(string permission) => Permissions.Contains(permission);
        public bool HasAllPermissions(params string[] permissions) => permissions.All(p => Permissions.Contains(p));
        public bool HasAnyPermission(params string[] permissions) => permissions.Any(p => Permissions.Contains(p));
    }

    [Theory]
    [InlineData(nameof(ProjectsController.GetAll))]
    [InlineData(nameof(ProjectsController.GetById))]
    [InlineData(nameof(ProjectsController.GetPersonnel))]
    public void Project_read_actions_allow_purchasing_plan_permissions(string actionName)
    {
        var method = typeof(ProjectsController).GetMethod(actionName);
        Assert.NotNull(method);

        var anyAttr = method.GetCustomAttribute<RequireAnyPermissionAttribute>();
        Assert.NotNull(anyAttr);

        // Verify policy contains both wms.projects.view:all and purchasing plan permissions
        var policy = anyAttr.Policy;
        Assert.NotNull(policy);
        Assert.Contains("wms.projects.view:all", policy);
        Assert.Contains("purchasing.plans.create", policy);
        Assert.Contains("purchasing.plans.view", policy);
    }

    [Theory]
    [InlineData("Kho hàng", true)]
    [InlineData("Kho hang", true)]
    [InlineData("Thủ kho", true)]
    [InlineData("Cung ứng", true)]
    [InlineData("Cung ung", true)]
    [InlineData("Procurement", true)]
    [InlineData("Procurement_Lead", true)]
    [InlineData("Admin", true)]
    [InlineData("Hệ thống", true)]
    [InlineData("Kế toán", true)]
    [InlineData("khohang@namviet.com", true)]
    [InlineData("admin@namviet.com", true)]
    [InlineData("Nguyễn Văn Khoa", false)]
    [InlineData("Trần Thị Mai An", false)]
    [InlineData("Lê Hoàng Long", false)]
    [InlineData("Phạm Quốc Bảo", false)]
    public void IsSystemRoleAccount_identifies_system_accounts_correctly(string name, bool expectedIsSystem)
    {
        var actual = ProjectsController.IsSystemRoleAccount(name);
        Assert.Equal(expectedIsSystem, actual);
    }

    [Fact]
    public async Task ProjectWarehouseAccessService_grants_unrestricted_scope_for_procurement_users()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new WarehouseDbContext(options);

        var currentUser = new TestCurrentUser
        {
            Roles = [RolePermissionTemplates.ProcurementRole]
        };

        var service = new ProjectWarehouseAccessService(db, currentUser);

        Assert.False(service.IsWarehouseKeeper);
        var scopedCodes = await service.GetScopedProjectCodesOrNullAsync(CancellationToken.None);
        Assert.Null(scopedCodes);

        var canOperate = await service.CanOperateOnProjectAsync("DA-2026-001", CancellationToken.None);
        Assert.True(canOperate);
    }
}
