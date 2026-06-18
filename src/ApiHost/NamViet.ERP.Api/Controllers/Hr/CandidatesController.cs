using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Application.Mappers.Hr;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Api.Infrastructure.RateLimiting;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/candidates")]
[Authorize(Roles = "Admin,HR")]
public class CandidatesController(
    HrDbContext db,
    IWebHostEnvironment env,
    ILogger<CandidatesController> logger,
    IValidator<CandidateDto> createValidator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CandidatePagedResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? position = null,
        [FromQuery] string? source = null,
        [FromQuery] string? status = null,
        [FromQuery] string? hrResponsible = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var baseQuery = db.Candidates.Include(c => c.Position)
            .Where(c => c.DeletedAt == null);

        var totalUnfiltered = await baseQuery.CountAsync(ct);
        var uniquePositionCount = await baseQuery
            .Where(c => c.Position != null && c.Position.Name != "")
            .Select(c => c.Position!.Name)
            .Distinct()
            .CountAsync(ct);

        var query = baseQuery;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c =>
                c.FullName.Contains(term) ||
                c.Phone.Contains(term) ||
                c.Email.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(position))
        {
            var pos = position.Trim();
            query = query.Where(c => c.Position != null && c.Position.Name == pos);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            var src = CandidateMapper.MapSource(source);
            query = query.Where(c => c.Source == src);
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<CandidateStatus>(status, true, out var candidateStatus))
        {
            query = query.Where(c => c.Status == candidateStatus);
        }

        if (!string.IsNullOrWhiteSpace(hrResponsible))
        {
            var hr = hrResponsible.Trim();
            var jsonNeedle = JsonSerializer.Serialize(new { hrResponsible = hr }, CandidateMapper.WorkflowJson);
            query = query.Where(c =>
                c.WorkflowData != null && EF.Functions.JsonContains(c.WorkflowData, jsonNeedle));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.AppliedAt)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new CandidatePagedResponse(
            items.Select(c => CandidateMapper.ToDto(c, logger)).ToList(),
            total,
            page,
            pageSize,
            totalUnfiltered,
            uniquePositionCount));
    }

    [HttpPost("repair-workflow")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> RepairWorkflow(CancellationToken ct)
    {
        var count = await CandidateWorkflowRepair.RepairAllAsync(db, logger, ct);
        return Ok(new { repairedCount = count });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CandidateDto>> GetById(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var cid)) return NotFound();

        var candidate = await db.Candidates.Include(x => x.Position)
            .FirstOrDefaultAsync(x => x.Id == cid, ct);
        if (candidate is null) return NotFound();

        return Ok(CandidateMapper.ToDto(candidate, logger));
    }

    [HttpGet("{id}/cv")]
    public async Task<IActionResult> DownloadCv(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var cid)) return NotFound();

        var candidate = await db.Candidates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cid && x.DeletedAt == null, ct);
        if (candidate is null || string.IsNullOrWhiteSpace(candidate.CvFileUrl))
            return NotFound("Không tìm thấy file CV.");

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var relative = candidate.CvFileUrl.TrimStart('/');
        if (relative.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            relative = relative["uploads/".Length..];

        var fullPath = Path.Combine(webRoot, "uploads", relative.Replace('/', Path.DirectorySeparatorChar));
        fullPath = Path.GetFullPath(fullPath);

        var uploadsRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads"))
            + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Đường dẫn file không hợp lệ.");
        if (!System.IO.File.Exists(fullPath))
            return NotFound("File CV không tồn tại trên máy chủ.");

        var downloadName = string.IsNullOrWhiteSpace(candidate.CvFileName)
            ? Path.GetFileName(fullPath)
            : candidate.CvFileName;

        return PhysicalFile(fullPath, CandidateMapper.ResolveCvContentType(downloadName), downloadName);
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicWrite)]
    public async Task<ActionResult<CandidateDto>> Create([FromBody] CandidateDto dto, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);

        var candidate = new Candidate { AppliedAt = DateTime.UtcNow };
        await CandidateMapper.ApplyFromDtoAsync(candidate, dto, db, ct);
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync(ct);

        candidate.WorkflowData = JsonSerializer.Serialize(
            CandidateMapper.LockAppliedAt(dto, candidate), CandidateMapper.WorkflowJson);
        await db.SaveChangesAsync(ct);

        return Ok(CandidateMapper.ToDto(candidate, logger));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CandidateDto>> Update(string id, [FromBody] CandidateDto dto, CancellationToken ct)
    {
        if (!int.TryParse(id, out var cid)) return NotFound();

        var candidate = await db.Candidates.FirstOrDefaultAsync(x => x.Id == cid, ct);
        if (candidate is null) return NotFound();

        await CandidateMapper.ApplyFromDtoAsync(candidate, dto, db, ct);
        candidate.WorkflowData = JsonSerializer.Serialize(
            CandidateMapper.LockAppliedAt(dto, candidate), CandidateMapper.WorkflowJson);
        candidate.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(CandidateMapper.ToDto(candidate, logger));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var cid)) return NotFound();

        var candidate = await db.Candidates.FindAsync([cid], ct);
        if (candidate is null) return NotFound();

        candidate.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
