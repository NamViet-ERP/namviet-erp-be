using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers;

/// <summary>
/// Internal HR admin API: paged list, contracts, appointments.
/// Uses structured request DTOs — not consumed by the current React FE (which uses <see cref="Hr.EmployeesController"/>).
/// </summary>
[ApiController]
[Route("api/hr/employees")]
[Authorize]
public class HrAdminEmployeesController(
    HrDbContext db,
    ICurrentUser currentUser,
    IEmployeeAccessService employeeAccess,
    IEmployeeAccountLinkSynchronizer accountLinks) : ControllerBase
{
    [HttpGet]
    [RequirePermission("hrm.employees.view:all")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? departmentId = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var query = db.Employees
            .Include(e => e.Department)
            .Include(e => e.Position)
            .Where(e => e.DeletedAt == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(e => e.FullName.Contains(search) || e.Code.Contains(search)
                || (e.Phone != null && e.Phone.Contains(search))
                || (e.Email != null && e.Email.Contains(search)));
        if (departmentId.HasValue)
            query = query.Where(e => e.DepartmentId == departmentId.Value);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<EmployeeStatus>(status, out var s))
            query = query.Where(e => e.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(e => e.Code)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new
            {
                e.Id, e.Code, e.FullName, Gender = e.Gender.ToString(),
                e.Phone, e.Email, e.DepartmentId, DepartmentName = e.Department != null ? e.Department.Name : null,
                e.PositionId, PositionName = e.Position != null ? e.Position.Name : null,
                Status = e.Status.ToString(), JoinDate = e.JoinDate, e.PhotoUrl
            })
            .ToListAsync(ct);

        return Ok(PagedResult<object>.Create(items, total, page, pageSize));
    }

    [HttpGet("{id:int}")]
    [RequireAnyPermission("hrm.employees.view:all", "hrm.employees.view:own")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        if (!await employeeAccess.CanViewEmployeeAsync(currentUser, id, ct))
            return NotFound();

        var e = await db.Employees
            .Include(x => x.Department)
            .Include(x => x.Position)
            .Include(x => x.Manager)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (e is null) return NotFound();
        return Ok(MapDetail(e));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest req, CancellationToken ct)
    {
        if (await db.Employees.AnyAsync(e => e.Code == req.Code && e.DeletedAt == null, ct))
            return Conflict($"Mã nhân viên '{req.Code}' đã tồn tại.");

        if (!DateOnly.TryParse(req.BirthDate, out var dob))
            return BadRequest("Ngày sinh không hợp lệ.");
        if (!DateOnly.TryParse(req.JoinDate, out var joinDate))
            return BadRequest("Ngày vào làm không hợp lệ.");

        var emp = new Employee
        {
            Code = req.Code,
            FullName = req.FullName,
            Gender = Enum.TryParse<Gender>(req.Gender, true, out var g) ? g : Gender.Other,
            BirthDate = dob,
            Phone = req.Phone,
            Email = req.Email,
            Cccd = req.Cccd,
            CccdDate = DateOnly.TryParse(req.CccdDate, out var cd) ? cd : null,
            CccdPlace = req.CccdPlace,
            AddressPermanent = req.AddressPermanent,
            AddressCurrent = req.AddressCurrent,
            DepartmentId = req.DepartmentId,
            PositionId = req.PositionId,
            ManagerId = req.ManagerId,
            JoinDate = joinDate,
            Education = Enum.TryParse<Education>(req.Education, true, out var edu) ? edu : Education.Other,
            BankAccount = req.BankAccount,
            BankName = req.BankName,
            TaxCode = req.TaxCode,
            SiNumber = req.SiNumber,
            PhotoUrl = req.PhotoUrl
        };
        db.Employees.Add(emp);
        await db.SaveChangesAsync(ct);
        await accountLinks.SynchronizeAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = emp.Id }, new { emp.Id, emp.Code });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEmployeeRequest req, CancellationToken ct)
    {
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.DeletedAt == null, ct);
        if (emp is null) return NotFound();

        emp.FullName = req.FullName;
        emp.Phone = req.Phone;
        emp.Email = req.Email;
        emp.Cccd = req.Cccd;
        emp.AddressPermanent = req.AddressPermanent;
        emp.AddressCurrent = req.AddressCurrent;
        emp.DepartmentId = req.DepartmentId;
        emp.PositionId = req.PositionId;
        emp.ManagerId = req.ManagerId;
        emp.Status = Enum.TryParse<EmployeeStatus>(req.Status, true, out var s) ? s : emp.Status;
        emp.BankAccount = req.BankAccount;
        emp.BankName = req.BankName;
        emp.TaxCode = req.TaxCode;
        emp.SiNumber = req.SiNumber;
        emp.PhotoUrl = req.PhotoUrl;
        emp.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.DeletedAt == null, ct);
        if (emp is null) return NotFound();
        emp.DeletedAt = DateTime.UtcNow;
        emp.Status = EmployeeStatus.Terminated;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:int}/contracts")]
    [RequireAnyPermission("hrm.employees.view:all", "hrm.employees.view:own")]
    public async Task<IActionResult> GetContracts(int id, CancellationToken ct)
    {
        if (!await employeeAccess.CanViewEmployeeAsync(currentUser, id, ct))
            return NotFound();

        var items = await db.LaborContracts
            .Where(c => c.EmployeeId == id)
            .OrderByDescending(c => c.StartDate)
            .Select(c => new
            {
                c.Id, c.ContractNo, Type = c.Type.ToString(), Status = c.Status.ToString(),
                c.StartDate, c.EndDate, c.Salary, c.Allowance, c.SignedDate, c.FileUrl
            })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("{id:int}/contracts")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> AddContract(int id, [FromBody] CreateContractRequest req, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == id && e.DeletedAt == null, ct))
            return NotFound();
        if (!DateOnly.TryParse(req.StartDate, out var start))
            return BadRequest("Ngày bắt đầu không hợp lệ.");

        var contract = new LaborContract
        {
            EmployeeId = id,
            ContractNo = req.ContractNo,
            Type = Enum.TryParse<ContractType>(req.Type, true, out var t) ? t : ContractType.Fixed12,
            StartDate = start,
            EndDate = DateOnly.TryParse(req.EndDate, out var end) ? end : null,
            Salary = req.Salary,
            Allowance = req.Allowance,
            FileUrl = req.FileUrl,
            Status = ContractStatus.Active
        };
        db.LaborContracts.Add(contract);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetContracts), new { id }, new { contract.Id });
    }

    [HttpGet("{id:int}/appointments")]
    [RequireAnyPermission("hrm.employees.view:all", "hrm.employees.view:own")]
    public async Task<IActionResult> GetAppointments(int id, CancellationToken ct)
    {
        if (!await employeeAccess.CanViewEmployeeAsync(currentUser, id, ct))
            return NotFound();

        var items = await db.Appointments
            .Where(a => a.EmployeeId == id)
            .OrderByDescending(a => a.EffectiveDate)
            .Select(a => new
            {
                a.Id, a.OldPositionId, a.NewPositionId,
                a.DecisionNo, a.EffectiveDate, a.Note
            })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("{id:int}/appointments")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> AddAppointment(int id, [FromBody] CreateAppointmentRequest req, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == id && e.DeletedAt == null, ct))
            return NotFound();
        if (!DateOnly.TryParse(req.EffectiveDate, out var eff))
            return BadRequest("Ngày hiệu lực không hợp lệ.");
        if (!await db.Positions.AnyAsync(p => p.Id == req.NewPositionId && p.DeletedAt == null, ct))
            return BadRequest($"Chức vụ ID={req.NewPositionId} không tồn tại.");

        var appt = new Appointment
        {
            EmployeeId = id,
            OldPositionId = req.OldPositionId,
            NewPositionId = req.NewPositionId,
            DecisionNo = req.DecisionNo,
            EffectiveDate = eff,
            Note = req.Note
        };
        db.Appointments.Add(appt);

        var emp = await db.Employees.FindAsync([id], ct);
        if (emp is not null) { emp.PositionId = req.NewPositionId; emp.UpdatedAt = DateTime.UtcNow; }

        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetAppointments), new { id }, new { appt.Id });
    }

    private static object MapDetail(Employee e) => new
    {
        e.Id, e.Code, e.FullName,
        Gender = e.Gender.ToString(),
        BirthDate = e.BirthDate,
        e.Phone, e.Email, e.Cccd, e.CccdDate, e.CccdPlace,
        e.AddressPermanent, e.AddressCurrent,
        e.DepartmentId, DepartmentName = e.Department?.Name,
        e.PositionId, PositionName = e.Position?.Name,
        ManagerName = e.Manager?.FullName,
        Status = e.Status.ToString(),
        Education = e.Education.ToString(),
        JoinDate = e.JoinDate,
        e.BankAccount, e.BankName, e.TaxCode, e.SiNumber, e.PhotoUrl
    };
}
