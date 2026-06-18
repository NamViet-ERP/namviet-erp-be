using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Modules.Hr.Infrastructure.Services;

public sealed class EmployeeUserLinkService(HrDbContext db) : IEmployeeUserLinkService
{
    public async Task<int> EnsureDepartmentAsync(string code, string name, CancellationToken ct = default)
    {
        var dept = await db.Departments.FirstOrDefaultAsync(d => d.Code == code, ct);
        if (dept is not null) return dept.Id;

        dept = new Department { Code = code, Name = name };
        db.Departments.Add(dept);
        await db.SaveChangesAsync(ct);
        return dept.Id;
    }

    public async Task UpsertFromUserAsync(EmployeeUserLinkRequest request, CancellationToken ct = default)
    {
        var departmentId = await EnsureDepartmentAsync(
            request.DepartmentCode, request.DepartmentName, ct);

        var employee = await db.Employees.FirstOrDefaultAsync(
            e => e.UserId == request.UserId || e.Code == request.EmployeeCode, ct);

        var joinDate = DateOnly.TryParseExact(request.JoinedDate, "dd/MM/yyyy", out var parsedJoin)
            ? parsedJoin
            : DateOnly.FromDateTime(DateTime.UtcNow);

        if (employee is null)
        {
            db.Employees.Add(new Employee
            {
                Code = request.EmployeeCode,
                UserId = request.UserId,
                FullName = request.FullName,
                Email = request.Email,
                Phone = request.Phone,
                DepartmentId = departmentId,
                JoinDate = joinDate,
                BirthDate = new DateOnly(1990, 1, 1),
                Gender = Gender.Other,
                Education = Education.Other,
                Status = EmployeeStatus.Active,
            });
        }
        else
        {
            employee.UserId = request.UserId;
            employee.FullName = request.FullName;
            employee.Email = request.Email;
            employee.Phone = request.Phone;
            employee.JoinDate = joinDate;
            employee.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
