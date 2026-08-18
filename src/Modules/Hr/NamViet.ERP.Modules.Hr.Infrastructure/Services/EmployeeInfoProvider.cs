using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Modules.Hr.Infrastructure.Services;

public sealed class EmployeeInfoProvider(
    HrDbContext db,
    IEmployeeUserLinkService links) : IEmployeeInfoProvider
{
    public async Task<LinkedEmployeeInfo?> GetByUserAsync(
        Guid userId, string? employeeCode, CancellationToken ct = default)
    {
        var employee = await FindByUserIdAsync(userId, ct);
        if (employee is null && !string.IsNullOrWhiteSpace(employeeCode))
        {
            await links.AttachMissingUserIdsAsync(
                new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
                {
                    [employeeCode] = userId,
                },
                ct);
            employee = await FindByUserIdAsync(userId, ct);
        }

        if (employee is null) return null;

        return new LinkedEmployeeInfo(
            employee.Id,
            employee.Department?.Name,
            ToClientStatus(employee.Status));
    }

    private Task<Employee?> FindByUserIdAsync(Guid userId, CancellationToken ct) =>
        db.Employees
            .Include(e => e.Department)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.DeletedAt == null && e.UserId == userId, ct);

    private static string ToClientStatus(EmployeeStatus status) => status switch
    {
        EmployeeStatus.Resigned => "Nghỉ việc",
        EmployeeStatus.OnLeave => "Thai sản",
        _ => "Đang làm",
    };
}
