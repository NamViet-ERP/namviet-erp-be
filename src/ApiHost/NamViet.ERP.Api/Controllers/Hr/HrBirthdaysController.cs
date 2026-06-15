using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/hr/birthdays")]
[Authorize]
public class HrBirthdaysController(HrDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BirthdayDto>>> List([FromQuery] int month = 6, CancellationToken ct = default)
    {
        var employees = await db.Employees.Where(e => e.DeletedAt == null).Include(e => e.Department).ToListAsync(ct);
        var result = employees
            .Where(e => e.BirthDate.Month == month)
            .Select(e => new BirthdayDto(e.FullName, e.Department?.Name ?? "", e.BirthDate.Day))
            .ToList();
        return Ok(result);
    }
}
