using System.Text.Json;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Modules.Hr.Domain.Entities;

using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;



namespace NamViet.ERP.Api.Controllers.Hr;



[ApiController]

[Route("api/candidates")]

[Authorize(Roles = "Admin,HR")]

public class CandidatesController(HrDbContext db, IWebHostEnvironment env, ILogger<CandidatesController> logger) : ControllerBase

{

    private static readonly JsonSerializerOptions WorkflowJson = new()

    {

        PropertyNameCaseInsensitive = true,

        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

    };



    [HttpGet]

    public async Task<ActionResult<FeCandidatePagedResult>> List(

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

            var src = MapSource(source);

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

            var jsonNeedle = JsonSerializer.Serialize(new { hrResponsible = hr }, WorkflowJson);

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



        return Ok(new FeCandidatePagedResult(

            items.Select(ToDto).ToList(),

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

    public async Task<ActionResult<FeCandidateDto>> GetById(string id, CancellationToken ct)

    {

        if (!int.TryParse(id, out var cid)) return NotFound();

        var c = await db.Candidates.Include(x => x.Position)

            .FirstOrDefaultAsync(x => x.Id == cid, ct);

        if (c is null) return NotFound();

        return Ok(ToDto(c));

    }



    [HttpGet("{id}/cv")]

    public async Task<IActionResult> DownloadCv(string id, CancellationToken ct)

    {

        if (!int.TryParse(id, out var cid)) return NotFound();

        var c = await db.Candidates.AsNoTracking()

            .FirstOrDefaultAsync(x => x.Id == cid && x.DeletedAt == null, ct);

        if (c is null || string.IsNullOrWhiteSpace(c.CvFileUrl)) return NotFound("Không tìm thấy file CV.");



        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

        var relative = c.CvFileUrl.TrimStart('/');

        if (relative.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))

            relative = relative["uploads/".Length..];

        var fullPath = Path.Combine(webRoot, "uploads", relative.Replace('/', Path.DirectorySeparatorChar));

        fullPath = Path.GetFullPath(fullPath);

        var uploadsRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads"))

            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))

            return BadRequest("Đường dẫn file không hợp lệ.");

        if (!System.IO.File.Exists(fullPath)) return NotFound("File CV không tồn tại trên máy chủ.");



        var downloadName = string.IsNullOrWhiteSpace(c.CvFileName)

            ? Path.GetFileName(fullPath)

            : c.CvFileName;

        var contentType = ResolveCvContentType(downloadName);

        return PhysicalFile(fullPath, contentType, downloadName);

    }



    [HttpPost]

    [AllowAnonymous]

    public async Task<ActionResult<FeCandidateDto>> Create([FromBody] FeCandidateDto dto, CancellationToken ct)

    {

        var c = new Candidate { AppliedAt = DateTime.UtcNow };

        await ApplyCandidateFromDto(c, dto, db, ct);

        db.Candidates.Add(c);

        await db.SaveChangesAsync(ct);

        c.WorkflowData = JsonSerializer.Serialize(LockAppliedAt(dto, c), WorkflowJson);

        await db.SaveChangesAsync(ct);

        return Ok(ToDto(c));

    }



    [HttpPut("{id}")]

    public async Task<ActionResult<FeCandidateDto>> Update(string id, [FromBody] FeCandidateDto dto, CancellationToken ct)

    {

        if (!int.TryParse(id, out var cid)) return NotFound();

        var c = await db.Candidates.FirstOrDefaultAsync(x => x.Id == cid, ct);

        if (c is null) return NotFound();

        await ApplyCandidateFromDto(c, dto, db, ct);

        c.WorkflowData = JsonSerializer.Serialize(LockAppliedAt(dto, c), WorkflowJson);

        c.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(ToDto(c));

    }



    [HttpDelete("{id}")]

    public async Task<IActionResult> Delete(string id, CancellationToken ct)

    {

        if (!int.TryParse(id, out var cid)) return NotFound();

        var c = await db.Candidates.FindAsync([cid], ct);

        if (c is null) return NotFound();

        c.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return NoContent();

    }



    private static async Task ApplyCandidateFromDto(

        Candidate c, FeCandidateDto dto, HrDbContext db, CancellationToken ct)

    {

        c.FullName = dto.FullName;

        c.Phone = dto.Phone;

        c.Email = dto.Email;

        c.Gender = dto.Gender == "Nữ" ? Gender.Female : Gender.Male;

        c.BirthDate = ParseBirthDate(dto.BirthDate);

        c.Address = dto.Address;

        c.Education = MapEducation(dto.Education);

        c.Experience = dto.Experience;

        c.Source = MapSource(dto.Source);



        if (!string.IsNullOrWhiteSpace(dto.Position))

        {

            var pos = await db.RecruitmentPositions

                .FirstOrDefaultAsync(p => p.Name == dto.Position && p.DeletedAt == null, ct);

            c.PositionId = pos?.Id;

        }

        else

        {

            c.PositionId = null;

        }



        if (!string.IsNullOrWhiteSpace(dto.CvFileName)) c.CvFileName = dto.CvFileName;

        if (!string.IsNullOrWhiteSpace(dto.CvFileUrl)) c.CvFileUrl = dto.CvFileUrl;

    }



    private static DateOnly ParseBirthDate(string value)

    {

        if (DateOnly.TryParse(value, out var iso)) return iso;

        var m = System.Text.RegularExpressions.Regex.Match(value, @"^(\d{2})/(\d{2})/(\d{4})$");

        if (m.Success && DateOnly.TryParse($"{m.Groups[3]}-{m.Groups[2]}-{m.Groups[1]}", out var vi))

            return vi;

        return DateOnly.FromDateTime(DateTime.UtcNow);

    }



    private static Education MapEducation(string? value) => value?.Trim() switch

    {

        "Đại học" or "University" => Education.University,

        "Cao đẳng" or "College" => Education.College,

        "THPT" or "HighSchool" => Education.HighSchool,

        _ => Education.Other,

    };



    private static RecruitmentSource MapSource(string? value) => value?.Trim() switch

    {

        "Facebook" => RecruitmentSource.Facebook,

        "Zalo" => RecruitmentSource.Zalo,

        "24h - lọc" or "24h - ứng tuyển" or "JobPortal24h" => RecruitmentSource.JobPortal24h,

        "TopCV" => RecruitmentSource.TopCV,

        "Glints" => RecruitmentSource.Glints,

        _ => RecruitmentSource.Other,

    };



    private static string ResolveCvContentType(string fileName)

    {

        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        return ext switch

        {

            ".pdf" => "application/pdf",

            ".doc" => "application/msword",

            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",

            _ => "application/octet-stream",

        };

    }



    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    private static TimeZoneInfo ResolveVietnamTimeZone()

    {

        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }

        catch (TimeZoneNotFoundException)

        {

            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        }

    }

    private static DateTime AsUtc(DateTime value) =>

        value.Kind switch

        {

            DateTimeKind.Utc => value,

            DateTimeKind.Local => value.ToUniversalTime(),

            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),

        };

    private static string FormatAppliedAt(DateTime dt)

    {

        var local = TimeZoneInfo.ConvertTimeFromUtc(AsUtc(dt), VietnamTimeZone);

        return local.ToString("dd/MM/yyyy HH:mm");

    }



    private static DateTime ResolveAppliedAt(Candidate c)

    {

        if (c.AppliedAt.Year >= 2000) return c.AppliedAt;

        return c.CreatedAt.Year >= 2000 ? c.CreatedAt : DateTime.UtcNow;

    }



    private static string ResolveAppliedAtText(Candidate c) => FormatAppliedAt(ResolveAppliedAt(c));



    private static FeCandidateDto LockAppliedAt(FeCandidateDto dto, Candidate c) =>

        dto with

        {

            Id = c.Id.ToString(),

            AppliedAt = ResolveAppliedAtText(c),

            CvFileUrl = dto.CvFileUrl ?? c.CvFileUrl,

        };


    private static FeCandidateDto BuildFallbackDto(Candidate c) =>
        new(c.Id.ToString(), c.FullName, c.Phone, c.Email,
            c.Gender == Gender.Female ? "Nữ" : "Nam", c.Address ?? "",
            c.BirthDate.ToString("dd/MM/yyyy"), c.Education.ToString(), "", "",
            c.Position?.Name ?? "", c.Experience ?? "", c.Source.ToString(), "",
            c.CvFileName ?? "", "", ResolveAppliedAtText(c),
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            c.CvFileUrl);

    private static FeCandidateDto ToDto(Candidate c)

    {

        var appliedAt = ResolveAppliedAtText(c);

        if (!string.IsNullOrEmpty(c.WorkflowData))

        {

            try

            {

                var dto = JsonSerializer.Deserialize<FeCandidateDto>(c.WorkflowData, WorkflowJson)!;

                return dto with

                {

                    Id = c.Id.ToString(),

                    AppliedAt = appliedAt,

                    CvFileUrl = dto.CvFileUrl ?? c.CvFileUrl,

                };

            }

            catch { /* fall through */ }

        }

        return BuildFallbackDto(c);

    }

}

