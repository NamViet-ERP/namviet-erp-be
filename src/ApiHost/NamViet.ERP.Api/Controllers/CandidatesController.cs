using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers;

public record CreateCandidateRequest(
    string FullName, string Phone, string Email,
    string Gender, string BirthDate, string? Address,
    string Education, int? PositionId, string? Experience,
    string Source, string? CvFileName, string? CvFileUrl, string? Note);

[ApiController]
[Route("api/hr/candidates")]
public class CandidatesController(HrDbContext db) : ControllerBase
{
    // Public endpoint – ứng viên tự nộp CV không cần đăng nhập
    [HttpPost("apply")]
    [AllowAnonymous]
    public async Task<IActionResult> Apply([FromBody] CreateCandidateRequest request, CancellationToken ct)
    {
        if (!DateOnly.TryParse(request.BirthDate, out var dob))
            return BadRequest("Ngày sinh không hợp lệ.");

        var candidate = new Candidate
        {
            FullName = request.FullName,
            Phone = request.Phone,
            Email = request.Email,
            Gender = Enum.TryParse<Gender>(request.Gender, true, out var g) ? g : Gender.Other,
            BirthDate = dob,
            Address = request.Address,
            Education = Enum.TryParse<Education>(request.Education, true, out var e) ? e : Education.Other,
            PositionId = request.PositionId,
            Experience = request.Experience,
            Source = Enum.TryParse<RecruitmentSource>(request.Source, true, out var src) ? src : RecruitmentSource.Other,
            CvFileName = request.CvFileName,
            CvFileUrl = request.CvFileUrl,
            Note = request.Note,
            Status = CandidateStatus.New,
            AppliedAt = DateTime.UtcNow
        };
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync(ct);
        return Ok(new { candidate.Id, message = "Hồ sơ ứng tuyển đã được tiếp nhận thành công!" });
    }

    [HttpGet]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var query = db.Candidates.Include(c => c.Position).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.FullName.Contains(search) || c.Phone.Contains(search) || c.Email.Contains(search));
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CandidateStatus>(status, out var s))
            query = query.Where(c => c.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(c => c.AppliedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(PagedResult<Candidate>.Create(items, total, page, pageSize));
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] string newStatus, CancellationToken ct)
    {
        var candidate = await db.Candidates.FindAsync([id], ct);
        if (candidate is null) return NotFound();
        if (!Enum.TryParse<CandidateStatus>(newStatus, true, out var status))
            return BadRequest("Trạng thái không hợp lệ.");
        candidate.Status = status;
        candidate.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
