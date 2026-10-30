using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NamViet.ERP.Api.Application.Dtos.Admin;
using NamViet.ERP.Api.Application.Services.Admin;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Api.Controllers.Admin;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Modules.Hr.Infrastructure.Services;
using NamViet.ERP.Modules.Identity.Application.Abstractions;
using NamViet.ERP.Modules.Identity.Application.Commands;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Identity.Infrastructure.Handlers;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Services;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Tests.Admin;

public sealed class AccountRoleValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateSystem_missing_requested_role_does_not_create_or_downgrade_account(bool hasLegacyEmployeeRole)
    {
        await using var provider = CreateServices();
        await using var scope = provider.CreateAsyncScope();
        await AddRolesAsync(scope.ServiceProvider, hasLegacyEmployeeRole ? ["Employee"] : []);

        var result = await scope.ServiceProvider.GetRequiredService<AdminUsersController>()
            .CreateSystem(SystemRequest("hr"), default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Provision_missing_requested_role_leaves_employee_unlinked_and_creates_no_account(bool hasLegacyEmployeeRole)
    {
        await using var provider = CreateServices();
        await using var scope = provider.CreateAsyncScope();
        await AddRolesAsync(scope.ServiceProvider, hasLegacyEmployeeRole ? ["Employee"] : []);
        var employee = await AddEmployeeAsync(scope.ServiceProvider);

        var result = await scope.ServiceProvider.GetRequiredService<AdminUsersController>()
            .Provision(new(employee.Id.ToString(), "hr", "Test-only-password123!"), default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        var hr = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        hr.ChangeTracker.Clear();
        Assert.Null((await hr.Employees.SingleAsync()).UserId);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Register_missing_requested_role_does_not_create_or_downgrade_account(bool hasLegacyEmployeeRole)
    {
        await using var provider = CreateServices();
        await using var scope = provider.CreateAsyncScope();
        await AddRolesAsync(scope.ServiceProvider, hasLegacyEmployeeRole ? ["Employee"] : []);

        await Assert.ThrowsAsync<DomainRuleException>(() => scope.ServiceProvider.GetRequiredService<RegisterHandler>()
            .Handle(new("SYS-TEST", "Test account", "register@example.test", "Test-only-password123!", null, "HR"), default));

        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.ToListAsync());
    }

    [Fact]
    public async Task Existing_requested_role_is_assigned_by_all_creation_paths()
    {
        await using var provider = CreateServices();
        await using var scope = provider.CreateAsyncScope();
        await AddRolesAsync(scope.ServiceProvider, ["HR"]);
        var controller = scope.ServiceProvider.GetRequiredService<AdminUsersController>();
        var employee = await AddEmployeeAsync(scope.ServiceProvider);

        Assert.IsType<OkObjectResult>((await controller.CreateSystem(SystemRequest("hr"), default)).Result);
        Assert.IsType<OkObjectResult>((await controller.Provision(new(employee.Id.ToString(), "hr", "Test-only-password123!"), default)).Result);
        await scope.ServiceProvider.GetRequiredService<RegisterHandler>().Handle(
            new("SYS-REGISTER", "Registered account", "register@example.test", "Test-only-password123!", null, "HR"), default);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var users = await userManager.Users.ToListAsync();
        Assert.Equal(3, users.Count);
        foreach (var user in users)
            Assert.Equal(["HR"], await userManager.GetRolesAsync(user));
        Assert.NotNull(employee.UserId);
    }

    private static CreateSystemAccountRequest SystemRequest(string role) =>
        new("System test", "system@example.test", null, null, null, null, role, "Test-only-password123!");

    private static async Task AddRolesAsync(IServiceProvider services, string[] roles)
    {
        var manager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var role in roles)
            Assert.True((await manager.CreateAsync(new ApplicationRole { Name = role, IsActive = true })).Succeeded);
    }

    private static async Task<Employee> AddEmployeeAsync(IServiceProvider services)
    {
        var hr = services.GetRequiredService<HrDbContext>();
        var employee = new Employee
        {
            Code = "NV-TEST", FullName = "Employee test", Email = "employee@example.test",
            BirthDate = new DateOnly(1990, 1, 1), JoinDate = new DateOnly(2026, 1, 1),
            Gender = Gender.Other, Education = Education.Other, Status = EmployeeStatus.Active,
        };
        hr.Employees.Add(employee);
        await hr.SaveChangesAsync();
        return employee;
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        var database = Guid.NewGuid().ToString("N");
        services.AddLogging();
        services.AddDbContext<IdentityDbContext>(options => options.UseInMemoryDatabase($"identity-{database}"));
        services.AddDbContext<HrDbContext>(options => options.UseInMemoryDatabase($"hr-{database}"));
        services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>().AddEntityFrameworkStores<IdentityDbContext>();
        services.AddScoped<IEmployeeUserLinkService, EmployeeUserLinkService>();
        services.AddScoped<IEmployeeAccountLinkSynchronizer, EmployeeAccountLinkSynchronizer>();
        services.AddScoped<IRefreshTokenRevoker, RefreshTokenRevoker>();
        services.AddScoped<IAdminUserQueryService, AdminUserQueryService>();
        services.AddScoped<AdminUsersController>();
        services.AddScoped<RegisterHandler>();
        return services.BuildServiceProvider();
    }
}
