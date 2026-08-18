using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Modules.Hr.Infrastructure.Services;

namespace NamViet.ERP.Tests.Hr;

public class EmployeeUserLinkServiceTests
{
    [Fact]
    public async Task AttachMissingUserIds_sets_userid_when_code_matches()
    {
        await using var db = CreateDb();
        var employee = AddEmployee(db, "NV-0134");
        await db.SaveChangesAsync();
        var userId = Guid.NewGuid();
        var sut = new EmployeeUserLinkService(db);

        var attached = await sut.AttachMissingUserIdsAsync(
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
            {
                ["nv-0134"] = userId,
            });

        Assert.Equal(1, attached);
        Assert.Equal(userId, employee.UserId);
    }

    [Fact]
    public async Task AttachMissingUserIds_does_not_overwrite_existing_userid()
    {
        await using var db = CreateDb();
        var existing = Guid.NewGuid();
        var employee = AddEmployee(db, "NV-0001", existing);
        await db.SaveChangesAsync();
        var sut = new EmployeeUserLinkService(db);

        var attached = await sut.AttachMissingUserIdsAsync(
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
            {
                ["NV-0001"] = Guid.NewGuid(),
            });

        Assert.Equal(0, attached);
        Assert.Equal(existing, employee.UserId);
    }

    [Fact]
    public async Task AttachMissingUserIds_skips_userid_already_taken()
    {
        await using var db = CreateDb();
        var taken = Guid.NewGuid();
        AddEmployee(db, "NV-0001", taken);
        var unlinked = AddEmployee(db, "NV-0002");
        await db.SaveChangesAsync();
        var sut = new EmployeeUserLinkService(db);

        var attached = await sut.AttachMissingUserIdsAsync(
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
            {
                ["NV-0002"] = taken,
            });

        Assert.Equal(0, attached);
        Assert.Null(unlinked.UserId);
    }

    [Fact]
    public async Task GetByUser_attaches_missing_userid_then_reads_by_userid_only()
    {
        await using var db = CreateDb();
        var employee = AddEmployee(db, "NV-0134");
        await db.SaveChangesAsync();
        var userId = Guid.NewGuid();
        var links = new EmployeeUserLinkService(db);
        var sut = new EmployeeInfoProvider(db, links);

        var linked = await sut.GetByUserAsync(userId, "NV-0134");

        Assert.NotNull(linked);
        Assert.Equal(employee.Id, linked.EmployeeId);
        Assert.Equal(userId, employee.UserId);
    }

    [Fact]
    public async Task GetByUser_does_not_match_code_when_userid_belongs_to_another_login()
    {
        await using var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var otherLogin = Guid.NewGuid();
        var employee = AddEmployee(db, "NV-0134", ownerId);
        await db.SaveChangesAsync();
        var sut = new EmployeeInfoProvider(db, new EmployeeUserLinkService(db));

        var linked = await sut.GetByUserAsync(otherLogin, "NV-0134");

        Assert.Null(linked);
        Assert.Equal(ownerId, employee.UserId);
    }

    private static HrDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<HrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HrDbContext(options);
    }

    private static Employee AddEmployee(HrDbContext db, string code, Guid? userId = null)
    {
        var employee = new Employee
        {
            Code = code,
            FullName = code,
            UserId = userId,
            BirthDate = new DateOnly(1990, 1, 1),
            JoinDate = new DateOnly(2020, 1, 1),
            Gender = Gender.Other,
            Education = Education.Other,
            Status = EmployeeStatus.Active,
        };
        db.Employees.Add(employee);
        return employee;
    }
}
