using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Application;
using NamViet.ERP.Api.Application.Mappers.Hr;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Api.Infrastructure.RateLimiting;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/candidates")]
[Authorize]
public class CandidatesController(
    HrDbContext db,
    IFileStorage storage,
    ILogger<CandidatesController> logger,
    IValidator<CandidateDto> createValidator,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("hrm.candidates.view:all")]
    public async Task<ActionResult<CandidatePagedResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? position = null,
        [FromQuery] string? source = null,
        [FromQuery] string? status = null,
        [FromQuery] string? hrResponsible = null,
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var baseQuery = db.Candidates
            .Include(c => c.Position)
            .Include(c => c.RecruitmentPlan)
                .ThenInclude(p => p!.Position)
            .Where(c => c.DeletedAt == null);

        var totalUnfiltered = await baseQuery.CountAsync(ct);
        var newCvCount = await baseQuery.CountAsync(c => c.Status == CandidateStatus.New, ct);
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

        var fromUtc = UtcDateRange.Start(dateFrom);
        if (fromUtc.HasValue)
            query = query.Where(c => c.AppliedAt >= fromUtc.Value);

        var toUtc = UtcDateRange.ExclusiveEnd(dateTo);
        if (toUtc.HasValue)
            query = query.Where(c => c.AppliedAt < toUtc.Value);

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
            uniquePositionCount,
            newCvCount));
    }

    [HttpPost("repair-workflow")]
    [RequirePermission("admin.users.create")]
    public async Task<ActionResult<object>> RepairWorkflow(CancellationToken ct)
    {
        var count = await CandidateWorkflowRepair.RepairAllAsync(db, logger, ct);
        return Ok(new { repairedCount = count });
    }

    [HttpGet("{id}")]
    [RequirePermission("hrm.candidates.view:all")]
    public async Task<ActionResult<CandidateDto>> GetById(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var cid)) return NotFound();

        var candidate = await db.Candidates
            .Include(x => x.Position)
            .Include(x => x.RecruitmentPlan)
                .ThenInclude(p => p!.Position)
            .FirstOrDefaultAsync(x => x.Id == cid, ct);
        if (candidate is null) return NotFound();

        return Ok(CandidateMapper.ToDto(candidate, logger));
    }

    [HttpGet("{id}/cv")]
    [RequirePermission("hrm.candidates.view:all")]
    public async Task<IActionResult> DownloadCv(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var cid)) return NotFound();

        var candidate = await db.Candidates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cid && x.DeletedAt == null, ct);
        if (candidate is null || string.IsNullOrWhiteSpace(candidate.CvFileUrl))
            return NotFound("Không tìm thấy file CV.");

        var stored = storage.Get(candidate.CvFileUrl);
        if (stored is null)
            return NotFound("File CV không tồn tại trên máy chủ.");

        var downloadName = string.IsNullOrWhiteSpace(candidate.CvFileName)
            ? Path.GetFileName(stored.FullPath)
            : candidate.CvFileName;

        return PhysicalFile(stored.FullPath, CandidateMapper.ResolveCvContentType(downloadName), downloadName);
    }

    [HttpPost]
    [RequirePermission("hrm.candidates.create")]
    public async Task<ActionResult<CandidateDto>> Create([FromBody] CandidateDto dto, CancellationToken ct)
        => await CreateCandidateAsync(dto, forceNewStatus: false, ct);

    [HttpPost("apply")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicWrite)]
    public async Task<ActionResult<CandidateDto>> Apply(
        [FromBody] PublicCandidateApplicationRequest request,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.CvFileUrl)
            && !request.CvFileUrl.StartsWith("hr/candidates/", StringComparison.Ordinal))
            return BadRequest("Khóa lưu trữ CV không hợp lệ.");

        return await CreateCandidateAsync(
            CandidateMapper.FromPublicApplication(request),
            forceNewStatus: true,
            ct);
    }

    private async Task<ActionResult<CandidateDto>> CreateCandidateAsync(
        CandidateDto dto,
        bool forceNewStatus,
        CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);
        if (!forceNewStatus && !string.IsNullOrWhiteSpace(dto.RecruitmentPlanId))
        {
            var canAssignPlan = await authorizationService.AuthorizeAsync(
                User,
                PermissionPolicies.Name("hrm.candidates.update"));
            if (!canAssignPlan.Succeeded) return Forbid();
        }

        var candidate = new Candidate { AppliedAt = DateTime.UtcNow };
        await CandidateMapper.ApplyFromDtoAsync(candidate, dto, db, ct);
        var planError = await AssignRecruitmentPlanAsync(candidate, dto.RecruitmentPlanId, ct);
        if (planError is not null) return BadRequest(planError);
        if (forceNewStatus) candidate.Status = CandidateStatus.New;

        var workflowDto = forceNewStatus ? dto with { Status = "New" } : dto;
        candidate.WorkflowData = JsonSerializer.Serialize(
            CandidateMapper.LockAppliedAt(workflowDto, candidate), CandidateMapper.WorkflowJson);
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync(ct);

        return Ok(CandidateMapper.ToDto(candidate, logger));
    }

    [HttpPut("{id}")]
    [RequirePermission("hrm.candidates.update")]
    public async Task<ActionResult<CandidateDto>> Update(string id, [FromBody] CandidateDto dto, CancellationToken ct)
    {
        if (!int.TryParse(id, out var cid)) return NotFound();

        var candidate = await db.Candidates
            .Include(x => x.RecruitmentPlan)
                .ThenInclude(p => p!.Position)
            .FirstOrDefaultAsync(x => x.Id == cid, ct);
        if (candidate is null) return NotFound();

        await CandidateMapper.ApplyFromDtoAsync(candidate, dto, db, ct);
        var planError = await AssignRecruitmentPlanAsync(candidate, dto.RecruitmentPlanId, ct);
        if (planError is not null) return BadRequest(planError);
        candidate.WorkflowData = JsonSerializer.Serialize(
            CandidateMapper.LockAppliedAt(dto, candidate), CandidateMapper.WorkflowJson);
        candidate.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(CandidateMapper.ToDto(candidate, logger));
    }

    [HttpPatch("{id}/status")]
    [RequirePermission("hrm.candidates.update")]
    public async Task<ActionResult<CandidateDto>> UpdateStatus(
        string id,
        [FromBody] UpdateCandidateStatusRequest request,
        CancellationToken ct)
    {
        if (!int.TryParse(id, out var cid)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Status)) return BadRequest("Trạng thái không được để trống.");

        var candidate = await db.Candidates
            .Include(x => x.Position)
            .Include(x => x.RecruitmentPlan)
                .ThenInclude(p => p!.Position)
            .FirstOrDefaultAsync(x => x.Id == cid && x.DeletedAt == null, ct);
        if (candidate is null) return NotFound();

        var planError = await AssignRecruitmentPlanAsync(candidate, request.RecruitmentPlanId, ct);
        if (planError is not null) return BadRequest(planError);
        var currentDto = CandidateMapper.ToDto(candidate, logger);
        var nextDto = CandidateMapper.ApplyStatusTransition(currentDto, request);
        candidate.Status = CandidateMapper.ParseClientStatus(nextDto.Status);
        candidate.WorkflowData = JsonSerializer.Serialize(
            CandidateMapper.LockAppliedAt(nextDto, candidate), CandidateMapper.WorkflowJson);
        candidate.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(CandidateMapper.ToDto(candidate, logger));
    }

    [HttpDelete("{id}")]
    [RequirePermission("hrm.candidates.delete")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var cid)) return NotFound();

        var candidate = await db.Candidates.FindAsync([cid], ct);
        if (candidate is null) return NotFound();

        candidate.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<string?> AssignRecruitmentPlanAsync(
        Candidate candidate,
        string? recruitmentPlanId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recruitmentPlanId))
        {
            candidate.RecruitmentPlanId = null;
            candidate.RecruitmentPlan = null;
            return null;
        }

        if (!int.TryParse(recruitmentPlanId, out var planId))
            return "Mã kế hoạch tuyển dụng không hợp lệ.";

        var plan = await db.RecruitmentPlans
            .Include(p => p.Position)
            .FirstOrDefaultAsync(p => p.Id == planId && p.DeletedAt == null, ct);
        if (plan is null)
            return "Kế hoạch tuyển dụng không tồn tại hoặc đã bị xóa.";

        var validationError = CandidateRecruitmentPlanPolicy.Validate(candidate, plan);
        if (validationError is not null)
            return validationError;

        candidate.RecruitmentPlanId = plan.Id;
        candidate.RecruitmentPlan = plan;
        return null;
    }
}
