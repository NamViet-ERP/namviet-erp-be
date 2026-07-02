using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NamViet.ERP.Api.Infrastructure.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/recruitment/positions")]
[Authorize(Roles = "Admin,HR")]
public class RecruitmentPositionsController(HrDbContext db) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicRead)]
    public async Task<ActionResult<List<RecruitmentPositionDto>>> List(CancellationToken ct)
    {
        var items = await db.RecruitmentPositions
            .Where(p => p.DeletedAt == null)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
        return Ok(items.Select(p => new RecruitmentPositionDto(p.Name, p.IsActive)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<RecruitmentPositionDto>> Create([FromBody] RecruitmentPositionDto dto, CancellationToken ct)
    {
        db.RecruitmentPositions.Add(new RecruitmentPosition { Name = dto.Name, IsActive = dto.IsActive });
        await db.SaveChangesAsync(ct);
        return Ok(dto);
    }

    [HttpPatch("{name}/toggle")]
    public async Task<IActionResult> Toggle(string name, CancellationToken ct)
    {
        var p = await db.RecruitmentPositions.FirstOrDefaultAsync(x => x.Name == name, ct);
        if (p is null) return NotFound();
        p.IsActive = !p.IsActive;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> Delete(string name, CancellationToken ct)
    {
        var p = await db.RecruitmentPositions.FirstOrDefaultAsync(x => x.Name == name, ct);
        if (p is null) return NotFound();
        p.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
