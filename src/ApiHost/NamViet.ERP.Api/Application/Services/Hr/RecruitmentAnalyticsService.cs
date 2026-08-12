using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Application;
using NamViet.ERP.Api.Application.Mappers.Hr;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Hr;

public interface IRecruitmentAnalyticsService
{
    Task<RecruitmentKpiDto> GetKpiAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RecruitmentTrendDto>> GetTrendAsync(int months = 6, CancellationToken ct = default);
    Task<IReadOnlyList<RecruitmentSourceStatDto>> GetBySourceAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RecruitmentPositionStatDto>> GetByPositionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RecruitmentCostSummaryDto>> GetCostBySourceAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RecruitmentCalendarEventDto>> GetCalendarAsync(
        DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<RecruitmentReportSummaryDto> GetReportSummaryAsync(
        DateOnly? dateFrom, DateOnly? dateTo, CancellationToken ct = default);
    byte[] BuildReportWorkbook(RecruitmentReportSummaryDto summary);
}

public sealed class RecruitmentAnalyticsService(HrDbContext db, ILogger<RecruitmentAnalyticsService> logger)
    : IRecruitmentAnalyticsService
{
    public async Task<RecruitmentKpiDto> GetKpiAsync(CancellationToken ct = default)
    {
        var statusCounts = await db.Candidates.AsNoTracking()
            .Where(c => c.DeletedAt == null)
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        int Count(CandidateStatus s) => statusCounts.GetValueOrDefault(s);

        var interviewStatuses = new[]
        {
            CandidateStatus.InterviewScheduled,
            CandidateStatus.Interview1,
            CandidateStatus.Interview2,
            CandidateStatus.InterviewPassed,
        };
        var interviewCount = interviewStatuses.Sum(Count);

        var plans = await db.RecruitmentPlans.AsNoTracking().Where(p => p.Status == "Active").ToListAsync(ct);
        var totalHeadcount = plans.Sum(p => p.Headcount);
        var planIds = plans.Select(p => p.Id).ToList();
        var hiredStatuses = new[]
        {
            CandidateStatus.Accepted,
            CandidateStatus.Probation,
            CandidateStatus.Official,
        };
        var totalHired = await db.Candidates.AsNoTracking()
            .Where(c => c.DeletedAt == null
                && hiredStatuses.Contains(c.Status)
                && c.RecruitmentPlanId != null
                && planIds.Contains(c.RecruitmentPlanId.Value))
            .CountAsync(ct);

        var scheduledCandidates = await db.Candidates.AsNoTracking()
            .Where(c => c.DeletedAt == null && c.WorkflowData != null)
            .ToListAsync(ct);
        var interviewsScheduled = CountScheduledInterviews(scheduledCandidates);
        var interviewedCount = interviewCount
            + Count(CandidateStatus.Offered)
            + Count(CandidateStatus.Accepted)
            + Count(CandidateStatus.Probation)
            + Count(CandidateStatus.Official);

        return new RecruitmentKpiDto(
            statusCounts.Values.Sum(),
            Count(CandidateStatus.New),
            Count(CandidateStatus.Screening),
            interviewCount,
            Count(CandidateStatus.Offered),
            Count(CandidateStatus.Accepted),
            Count(CandidateStatus.Rejected),
            totalHeadcount,
            totalHired,
            Math.Max(0, totalHeadcount - totalHired),
            interviewsScheduled,
            plans.Count,
            interviewedCount);
    }

    public async Task<IReadOnlyList<RecruitmentTrendDto>> GetTrendAsync(int months = 6, CancellationToken ct = default)
    {
        var monthCount = months;
        var now = DateTime.UtcNow;
        var firstMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(-(monthCount - 1));
        var endMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(1);
        var firstDate = DateOnly.FromDateTime(firstMonth);
        var endDate = DateOnly.FromDateTime(endMonth);

        var applicationRows = await db.Candidates.AsNoTracking()
            .Where(c => c.DeletedAt == null && c.AppliedAt >= firstMonth && c.AppliedAt < endMonth)
            .GroupBy(c => new { c.AppliedAt.Year, c.AppliedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(ct);
        var applicationMap = applicationRows.ToDictionary(x => (x.Year, x.Month), x => x.Count);

        var hiredStatuses = new[]
        {
            CandidateStatus.Accepted,
            CandidateStatus.Probation,
            CandidateStatus.Official,
        };
        var hireRows = await db.Onboards.AsNoTracking()
            .Where(o => o.CandidateId != null && o.StartDate >= firstDate && o.StartDate < endDate)
            .Join(
                db.Candidates.AsNoTracking().Where(c => hiredStatuses.Contains(c.Status)),
                onboard => onboard.CandidateId,
                candidate => (int?)candidate.Id,
                (onboard, candidate) => new { onboard.StartDate, CandidateId = candidate.Id })
            .GroupBy(row => new { row.StartDate.Year, row.StartDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Count = g.Select(row => row.CandidateId).Distinct().Count(),
            })
            .ToListAsync(ct);
        var hireMap = hireRows.ToDictionary(x => (x.Year, x.Month), x => x.Count);

        var result = new List<RecruitmentTrendDto>();

        for (var i = monthCount - 1; i >= 0; i--)
        {
            var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i);
            var key = (start.Year, start.Month);
            result.Add(new RecruitmentTrendDto(
                start.ToString("MM/yyyy"),
                applicationMap.GetValueOrDefault(key),
                hireMap.GetValueOrDefault(key)));
        }

        return result;
    }

    public async Task<IReadOnlyList<RecruitmentSourceStatDto>> GetBySourceAsync(CancellationToken ct = default)
    {
        var rows = await db.Candidates.AsNoTracking()
            .Where(c => c.DeletedAt == null)
            .GroupBy(c => c.Source)
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        return rows.Select(r => new RecruitmentSourceStatDto(MapSourceLabel(r.Source), r.Count)).ToList();
    }

    public async Task<IReadOnlyList<RecruitmentPositionStatDto>> GetByPositionAsync(CancellationToken ct = default)
    {
        var rows = await db.Candidates.AsNoTracking()
            .Where(c => c.DeletedAt == null && c.Position != null)
            .GroupBy(c => c.Position!.Name)
            .Select(g => new { Position = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(12)
            .ToListAsync(ct);

        return rows.Select(r => new RecruitmentPositionStatDto(r.Position, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<RecruitmentCostSummaryDto>> GetCostBySourceAsync(CancellationToken ct = default)
    {
        var costs = await db.RecruitmentCosts.AsNoTracking()
            .Where(c => c.DeletedAt == null)
            .GroupBy(c => c.Source)
            .Select(g => new { Source = g.Key, Total = g.Sum(c => c.Amount) })
            .ToListAsync(ct);
        var hiredStatuses = new[]
        {
            CandidateStatus.Accepted,
            CandidateStatus.Probation,
            CandidateStatus.Official,
        };
        var hiredBySource = await db.Candidates.AsNoTracking()
            .Where(c => c.DeletedAt == null && hiredStatuses.Contains(c.Status))
            .GroupBy(c => c.Source)
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var hiredMap = hiredBySource.ToDictionary(
            x => MapSourceLabel(x.Source),
            x => x.Count);

        return costs
            .Select(cost =>
            {
                var source = cost.Source.Trim();
                var hires = hiredMap.GetValueOrDefault(source, 0);
                decimal? perHire = hires > 0 ? Math.Round(cost.Total / hires, 0) : null;
                return new RecruitmentCostSummaryDto(source, cost.Total, hires, perHire);
            })
            .OrderByDescending(s => s.TotalAmount)
            .ToList();
    }

    public async Task<IReadOnlyList<RecruitmentCalendarEventDto>> GetCalendarAsync(
        DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var startDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = (to ?? startDate.AddDays(6)).AddDays(1);
        var rangeStart = UtcDateRange.Start(startDate)!.Value;
        var rangeEnd = UtcDateRange.Start(endDate)!.Value;

        var candidates = await db.Candidates.AsNoTracking()
            .Include(c => c.Position)
            .Where(c => c.DeletedAt == null)
            .Where(c => c.Status == CandidateStatus.InterviewScheduled
                || c.Status == CandidateStatus.Interview1
                || c.Status == CandidateStatus.Interview2
                || c.Status == CandidateStatus.InterviewPassed
                || c.WorkflowData != null)
            .ToListAsync(ct);

        var events = new List<RecruitmentCalendarEventDto>();

        foreach (var candidate in candidates)
        {
            var dto = CandidateMapper.ToDto(candidate, logger);
            var interviewDate = ParseInterviewDate(dto.InterviewDate);
            if (!interviewDate.HasValue) continue;

            var day = interviewDate.Value.Date;
            if (day < rangeStart || day >= rangeEnd) continue;

            events.Add(new RecruitmentCalendarEventDto(
                candidate.Id.ToString(),
                dto.FullName,
                dto.Phone,
                dto.Email,
                dto.Position,
                dto.Source,
                dto.AppliedAt,
                dto.Status,
                FormatCalendarInterviewDate(dto.InterviewDate ?? day.ToString("dd/MM/yyyy"), dto.InterviewTime),
                dto.InterviewTime,
                dto.Evaluator,
                dto.HrResponsible));
        }

        return events
            .OrderBy(e => ParseInterviewDate(e.InterviewDate) ?? DateTime.MaxValue)
            .ThenBy(e => e.FullName)
            .ToList();
    }

    public async Task<RecruitmentReportSummaryDto> GetReportSummaryAsync(
        DateOnly? dateFrom, DateOnly? dateTo, CancellationToken ct = default)
    {
        var query = db.Candidates.AsNoTracking().Where(c => c.DeletedAt == null);

        var fromUtc = UtcDateRange.Start(dateFrom);
        if (fromUtc.HasValue)
            query = query.Where(c => c.AppliedAt >= fromUtc.Value);

        var toUtc = UtcDateRange.ExclusiveEnd(dateTo);
        if (toUtc.HasValue)
            query = query.Where(c => c.AppliedAt < toUtc.Value);

        var interviewStatuses = new[]
        {
            CandidateStatus.InterviewScheduled,
            CandidateStatus.Interview1,
            CandidateStatus.Interview2,
            CandidateStatus.InterviewPassed,
            CandidateStatus.Offered,
            CandidateStatus.Accepted,
            CandidateStatus.Probation,
            CandidateStatus.Official,
        };

        var hiredStatuses = new[]
        {
            CandidateStatus.Accepted,
            CandidateStatus.Probation,
            CandidateStatus.Official,
        };
        var sourceRows = await query
            .GroupBy(c => c.Source)
            .Select(g => new
            {
                Source = g.Key,
                Cv = g.Count(),
                Interviews = g.Count(c => interviewStatuses.Contains(c.Status)),
                Hired = g.Count(c => hiredStatuses.Contains(c.Status)),
            })
            .ToListAsync(ct);
        var bySource = sourceRows
            .Select(row => new RecruitmentReportSourceRowDto(
                MapSourceLabel(row.Source),
                row.Cv,
                row.Interviews,
                row.Hired,
                row.Cv > 0 ? Math.Round((double)row.Hired / row.Cv * 100, 1) : 0))
            .OrderByDescending(r => r.CvCount)
            .ToList();

        // EF Core cannot translate GroupBy(c => c.Position!.Name) over optional nav + soft-delete filter.
        // Aggregate PositionId in SQL, then resolve names in memory.
        var positionIdRows = await query
            .Where(c => c.PositionId != null)
            .GroupBy(c => c.PositionId!.Value)
            .Select(g => new { PositionId = g.Key, Cv = g.Count() })
            .ToListAsync(ct);

        var positionIds = positionIdRows.Select(r => r.PositionId).ToList();
        var positionNames = await db.RecruitmentPositions
            .AsNoTracking()
            .Where(p => positionIds.Contains(p.Id) && p.DeletedAt == null)
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var byPosition = positionIdRows
            .Select(row => new RecruitmentReportPositionRowDto(
                positionNames.TryGetValue(row.PositionId, out var name) ? name : "(Không rõ vị trí)",
                row.Cv))
            .OrderByDescending(r => r.CvCount)
            .Take(15)
            .ToList();

        return new RecruitmentReportSummaryDto(bySource, byPosition);
    }

    public byte[] BuildReportWorkbook(RecruitmentReportSummaryDto summary)
    {
        using var workbook = new XLWorkbook();

        var sourceSheet = workbook.Worksheets.Add("Theo nguồn");
        sourceSheet.Cell(1, 1).Value = "Nguồn";
        sourceSheet.Cell(1, 2).Value = "Số CV";
        sourceSheet.Cell(1, 3).Value = "PV/Offer";
        sourceSheet.Cell(1, 4).Value = "Đã tuyển";
        sourceSheet.Cell(1, 5).Value = "Tỷ lệ (%)";
        sourceSheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var item in summary.BySource)
        {
            sourceSheet.Cell(row, 1).Value = item.Source;
            sourceSheet.Cell(row, 2).Value = item.CvCount;
            sourceSheet.Cell(row, 3).Value = item.InterviewCount;
            sourceSheet.Cell(row, 4).Value = item.HiredCount;
            sourceSheet.Cell(row, 5).Value = item.ConversionRate;
            row++;
        }
        sourceSheet.Columns().AdjustToContents();

        var positionSheet = workbook.Worksheets.Add("Theo vị trí");
        positionSheet.Cell(1, 1).Value = "Vị trí";
        positionSheet.Cell(1, 2).Value = "Số CV";
        positionSheet.Row(1).Style.Font.Bold = true;

        row = 2;
        foreach (var item in summary.ByPosition)
        {
            positionSheet.Cell(row, 1).Value = item.Position;
            positionSheet.Cell(row, 2).Value = item.CvCount;
            row++;
        }
        positionSheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private int CountScheduledInterviews(IReadOnlyList<Candidate> candidates)
    {
        var now = DateTime.UtcNow.Date;
        var weekEnd = now.AddDays(7);
        return candidates.Count(c =>
        {
            var dto = CandidateMapper.ToDto(c, logger);
            var date = ParseInterviewDate(dto.InterviewDate);
            return date.HasValue && date.Value.Date >= now && date.Value.Date < weekEnd;
        });
    }

    private static DateTime? ParseInterviewDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var iso))
            return iso;

        var m = Regex.Match(trimmed, @"^(\d{2})/(\d{2})/(\d{4})(?:[ T](\d{2}):(\d{2}))?");
        if (m.Success
            && int.TryParse(m.Groups[1].Value, out var d)
            && int.TryParse(m.Groups[2].Value, out var mo)
            && int.TryParse(m.Groups[3].Value, out var y))
        {
            var hour = m.Groups[4].Success && int.TryParse(m.Groups[4].Value, out var h) ? h : 0;
            var minute = m.Groups[5].Success && int.TryParse(m.Groups[5].Value, out var mi) ? mi : 0;
            return new DateTime(y, mo, d, hour, minute, 0, DateTimeKind.Local);
        }

        return null;
    }

    private static string FormatCalendarInterviewDate(string? date, string? time)
    {
        var d = date?.Trim();
        if (string.IsNullOrEmpty(d)) return "";

        if (Regex.IsMatch(d, @"[ T]\d{1,2}:\d{2}"))
            return d;

        var t = time?.Trim();
        if (string.IsNullOrEmpty(t))
            return d;

        return $"{d} {t}";
    }

    private static string MapSourceLabel(RecruitmentSource source) => source switch
    {
        RecruitmentSource.Facebook => "Facebook",
        RecruitmentSource.Zalo => "Zalo",
        RecruitmentSource.JobPortal24h => "24h - ứng tuyển",
        RecruitmentSource.TopCV => "TopCV",
        RecruitmentSource.Glints => "Glints",
        RecruitmentSource.Internal => "Nội bộ",
        RecruitmentSource.Referral => "Giới thiệu",
        _ => "Khác",
    };
}
