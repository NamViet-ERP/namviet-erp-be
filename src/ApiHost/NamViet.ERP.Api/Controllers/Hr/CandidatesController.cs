using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/candidates")]
public class CandidatesController(HrDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<List<FeCandidateDto>>> List(CancellationToken ct)
    {
        var items = await db.Candidates.Include(c => c.Position)
            .Where(c => c.DeletedAt == null).ToListAsync(ct);
        return Ok(items.Select(ToDto).ToList());
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<FeCandidateDto>> GetById(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var cid)) return NotFound();
        var c = await db.Candidates.Include(x => x.Position)
            .FirstOrDefaultAsync(x => x.Id == cid, ct);
        if (c is null) return NotFound();
        return Ok(ToDto(c));
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<FeCandidateDto>> Create([FromBody] FeCandidateDto dto, CancellationToken ct)
    {
        var c = new Candidate
        {
            FullName = dto.FullName,
            Phone = dto.Phone,
            Email = dto.Email,
            Gender = dto.Gender == "Nữ" ? Gender.Female : Gender.Male,
            BirthDate = DateOnly.TryParse(dto.BirthDate, out var bd) ? bd : DateOnly.FromDateTime(DateTime.UtcNow),
            Address = dto.Address,
            Education = Education.University,
            Experience = dto.Experience,
            Source = RecruitmentSource.Other,
            CvFileName = dto.CvFileName,
            AppliedAt = DateTime.UtcNow,
            WorkflowData = JsonSerializer.Serialize(dto)
        };
        db.Candidates.Add(c);
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(c));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<FeCandidateDto>> Update(string id, [FromBody] FeCandidateDto dto, CancellationToken ct)
    {
        if (!int.TryParse(id, out var cid)) return NotFound();
        var c = await db.Candidates.FirstOrDefaultAsync(x => x.Id == cid, ct);
        if (c is null) return NotFound();
        c.FullName = dto.FullName;
        c.Phone = dto.Phone;
        c.WorkflowData = JsonSerializer.Serialize(dto);
        c.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(c));
    }

    [HttpPatch("{id}/advance")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Advance(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var cid)) return NotFound();
        var c = await db.Candidates.FindAsync([cid], ct);
        if (c is null) return NotFound();
        c.Status = c.Status switch
        {
            CandidateStatus.New => CandidateStatus.Screening,
            CandidateStatus.Screening => CandidateStatus.Interview1,
            CandidateStatus.Interview1 => CandidateStatus.Offered,
            CandidateStatus.Offered => CandidateStatus.Accepted,
            _ => c.Status
        };
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var cid)) return NotFound();
        var c = await db.Candidates.FindAsync([cid], ct);
        if (c is null) return NotFound();
        c.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static FeCandidateDto ToDto(Candidate c)
    {
        if (!string.IsNullOrEmpty(c.WorkflowData))
        {
            try { return JsonSerializer.Deserialize<FeCandidateDto>(c.WorkflowData)!; }
            catch { /* fall through */ }
        }
        return new FeCandidateDto(c.Id.ToString(), c.FullName, c.Phone, c.Email,
            c.Gender == Gender.Female ? "Nữ" : "Nam", c.Address ?? "",
            c.BirthDate.ToString("dd/MM/yyyy"), c.Education.ToString(), "", "",
            c.Position?.Name ?? "", c.Experience ?? "", c.Source.ToString(), "",
            c.CvFileName ?? "", "", c.AppliedAt.ToString("dd/MM/yyyy"),
            null, null, null, null, null, null, null, null, null, null, null, null, null, null);
    }
}
