using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Modules.Hr.Infrastructure.Services;

public sealed class EmployeeInfoProvider(HrDbContext db) : IEmployeeInfoProvider
{
    public async Task<LinkedEmployeeInfo?> GetByUserAsync(
        Guid userId, string? employeeCode, CancellationToken ct = default)
    {
        var employee = await db.Employees
            .Include(e => e.Department)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.DeletedAt == null && (e.UserId == userId || e.Code == employeeCode),
                ct);

        if (employee is null) return null;

        return new LinkedEmployeeInfo(
            employee.Id,
            employee.Department?.Name,
            ToClientStatus(employee.Status));
    }

    private static string ToClientStatus(EmployeeStatus status) => status switch
    {
        EmployeeStatus.Resigned => "Nghỉ việc",
        EmployeeStatus.OnLeave => "Thai sản",
        _ => "Đang làm",
    };
}
