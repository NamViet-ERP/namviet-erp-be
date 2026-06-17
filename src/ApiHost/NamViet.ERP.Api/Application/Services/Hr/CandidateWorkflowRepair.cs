using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Hr;

public static class CandidateWorkflowRepair
{
    private static readonly JsonSerializerOptions WorkflowJson = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task<int> RepairAllAsync(
        HrDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        var candidates = await db.Candidates.Include(c => c.Position)
            .Where(c => c.DeletedAt == null)
            .ToListAsync(ct);

        var count = 0;
        foreach (var c in candidates.Where(NeedsWorkflowAppliedAtRepair))
        {
            RepairWorkflowAppliedAt(c);
            count++;
        }

        if (count > 0)
            await db.SaveChangesAsync(ct);

        logger.LogInformation("Candidate workflow repair completed: {RepairedCount} record(s)", count);
        return count;
    }

    private static bool NeedsWorkflowAppliedAtRepair(Candidate c)
    {
        if (string.IsNullOrWhiteSpace(c.WorkflowData)) return true;

        try
        {
            var dto = JsonSerializer.Deserialize<CandidateDto>(c.WorkflowData, WorkflowJson);
            return dto is null || string.IsNullOrWhiteSpace(dto.AppliedAt);
        }
        catch
        {
            return true;
        }
    }

    private static void RepairWorkflowAppliedAt(Candidate c)
    {
        CandidateDto dto;
        if (!string.IsNullOrWhiteSpace(c.WorkflowData))
        {
            try
            {
                dto = JsonSerializer.Deserialize<CandidateDto>(c.WorkflowData, WorkflowJson)
                    ?? BuildFallbackDto(c);
            }
            catch
            {
                dto = BuildFallbackDto(c);
            }
        }
        else
        {
            dto = BuildFallbackDto(c);
        }

        c.WorkflowData = JsonSerializer.Serialize(LockAppliedAt(dto, c), WorkflowJson);
    }

    private static CandidateDto LockAppliedAt(CandidateDto dto, Candidate c) =>
        dto with
        {
            Id = c.Id.ToString(),
            AppliedAt = ResolveAppliedAtText(c),
            CvFileUrl = dto.CvFileUrl ?? c.CvFileUrl,
        };

    private static CandidateDto BuildFallbackDto(Candidate c) =>
        new(c.Id.ToString(), c.FullName, c.Phone, c.Email,
            c.Gender == Gender.Female ? "Nữ" : "Nam", c.Address ?? "",
            c.BirthDate.ToString("dd/MM/yyyy"), c.Education.ToString(), "", "",
            c.Position?.Name ?? "", c.Experience ?? "", c.Source.ToString(), "",
            c.CvFileName ?? "", "", ResolveAppliedAtText(c),
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            c.CvFileUrl);

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
}
