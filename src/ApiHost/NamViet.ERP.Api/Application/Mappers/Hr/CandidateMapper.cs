using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Mappers.Hr;

public static class CandidateMapper
{
    public static readonly JsonSerializerOptions WorkflowJson = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public static CandidateDto FromPublicApplication(PublicCandidateApplicationRequest request) =>
        new(
            "", request.FullName, request.Phone, request.Email, request.Gender, request.Address,
            request.BirthDate, request.Education, request.School, request.Major, request.Position,
            request.Experience, request.Source, request.HrResponsible?.Trim() ?? "", request.CvFileName ?? "", request.CvFileSize ?? "",
            "", null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            Status: "New", CvFileUrl: request.CvFileUrl);

    public static async Task ApplyFromDtoAsync(
        Candidate entity, CandidateDto dto, HrDbContext db, CancellationToken ct)
    {
        entity.FullName = dto.FullName;
        entity.Phone = dto.Phone;
        entity.Email = dto.Email;
        entity.Gender = dto.Gender == "Nữ" ? Gender.Female : Gender.Male;
        entity.BirthDate = ParseBirthDate(dto.BirthDate);
        entity.Address = dto.Address;
        entity.Education = MapEducation(dto.Education);
        entity.Experience = dto.Experience;
        entity.Source = MapSource(dto.Source);

        if (!string.IsNullOrWhiteSpace(dto.Position))
        {
            var pos = await db.RecruitmentPositions
                .FirstOrDefaultAsync(p => p.Name == dto.Position && p.DeletedAt == null, ct);
            entity.PositionId = pos?.Id;
        }
        else
        {
            entity.PositionId = null;
        }

        if (!string.IsNullOrWhiteSpace(dto.CvFileName)) entity.CvFileName = dto.CvFileName;
        if (!string.IsNullOrWhiteSpace(dto.CvFileUrl)) entity.CvFileUrl = dto.CvFileUrl;

        if (!string.IsNullOrWhiteSpace(dto.Status) &&
            Enum.TryParse<CandidateStatus>(dto.Status, true, out var parsedStatus))
        {
            entity.Status = parsedStatus;
        }
    }

    public static string ToClientStatus(CandidateStatus status) => status.ToString();

    public static CandidateStatus ParseClientStatus(string? value) =>
        Enum.TryParse<CandidateStatus>(value, true, out var parsed) ? parsed : CandidateStatus.New;

    public static CandidateDto ApplyStatusTransition(CandidateDto current, UpdateCandidateStatusRequest request)
    {
        var next = current with
        {
            Status = request.Status,
            RecruitmentPlanId = request.RecruitmentPlanId,
        };

        return request.Status switch
        {
            "InterviewScheduled" or "Interview1" or "Interview2" => next with
            {
                InterviewDate = request.InterviewDate ?? next.InterviewDate,
                InterviewTime = request.InterviewTime ?? next.InterviewTime,
                Evaluator = request.Evaluator ?? next.Evaluator,
                EvaluationResult = "",
            },
            "InterviewPassed" => next with
            {
                EvaluationResult = "Đạt",
                InterviewDate = request.InterviewDate ?? next.InterviewDate,
                InterviewTime = request.InterviewTime ?? next.InterviewTime,
                Evaluator = request.Evaluator ?? next.Evaluator,
            },
            "Offered" => next with
            {
                OfferStatus = "Đồng ý",
                OfferSalary = request.OfferSalary ?? next.OfferSalary,
                OfferRejectReason = request.OfferNote ?? next.OfferRejectReason,
                StartDate = request.StartDate ?? next.StartDate,
            },
            "Accepted" or "Probation" or "Official" => next with
            {
                OfferStatus = "Đồng ý",
                StartDate = request.StartDate ?? next.StartDate,
                OnboardingAttended = request.Status == "Accepted" ? "Đến" : next.OnboardingAttended,
            },
            "Rejected" => next with
            {
                EvaluationResult = "Không đạt",
                OfferRejectReason = request.RejectReason ?? next.OfferRejectReason,
            },
            "OfferDeclined" => next with
            {
                OfferStatus = "Từ chối",
                OfferRejectReason = request.RejectReason ?? next.OfferRejectReason,
            },
            "Screening" => next with
            {
                ScreeningType = string.IsNullOrWhiteSpace(next.ScreeningType) ? "Lọc" : next.ScreeningType,
            },
            _ => next,
        };
    }

    public static CandidateDto LockAppliedAt(CandidateDto dto, Candidate entity) =>
        dto with
        {
            Id = entity.Id.ToString(),
            AppliedAt = ResolveAppliedAtText(entity),
            Status = ToClientStatus(entity.Status),
            CvFileUrl = dto.CvFileUrl ?? entity.CvFileUrl,
            RecruitmentPlanId = entity.RecruitmentPlanId?.ToString(),
            RecruitmentPlanLabel = BuildRecruitmentPlanLabel(entity.RecruitmentPlan),
        };

    public static CandidateDto BuildFallbackDto(Candidate entity) =>
        new(entity.Id.ToString(), entity.FullName, entity.Phone, entity.Email,
            entity.Gender == Gender.Female ? "Nữ" : "Nam", entity.Address ?? "",
            entity.BirthDate.ToString("dd/MM/yyyy"), entity.Education.ToString(), "", "",
            entity.Position?.Name ?? "", entity.Experience ?? "", ToClientSource(entity.Source), "",
            entity.CvFileName ?? "", "", ResolveAppliedAtText(entity),
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            ToClientStatus(entity.Status),
            entity.CvFileUrl,
            entity.RecruitmentPlanId?.ToString(),
            BuildRecruitmentPlanLabel(entity.RecruitmentPlan));

    public static CandidateDto ToDto(Candidate entity, ILogger logger)
    {
        var appliedAt = ResolveAppliedAtText(entity);

        if (!string.IsNullOrEmpty(entity.WorkflowData))
        {
            try
            {
                var dto = JsonSerializer.Deserialize<CandidateDto>(entity.WorkflowData, WorkflowJson)!;
                return dto with
                {
                    Id = entity.Id.ToString(),
                    AppliedAt = appliedAt,
                    Status = ToClientStatus(entity.Status),
                    CvFileUrl = dto.CvFileUrl ?? entity.CvFileUrl,
                    RecruitmentPlanId = entity.RecruitmentPlanId?.ToString(),
                    RecruitmentPlanLabel = BuildRecruitmentPlanLabel(entity.RecruitmentPlan),
                };
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Invalid workflow JSON for candidate {CandidateId}, using fallback DTO", entity.Id);
            }
        }

        return BuildFallbackDto(entity);
    }

    public static string? BuildRecruitmentPlanLabel(RecruitmentPlan? plan)
    {
        if (plan is null) return null;

        var position = plan.Position?.Name?.Trim();
        var period = plan.Period?.Trim();
        if (!string.IsNullOrEmpty(position) && !string.IsNullOrEmpty(period))
            return $"{position} - {period}";
        if (!string.IsNullOrEmpty(position))
            return position;
        if (!string.IsNullOrEmpty(period))
            return period;
        return $"Kế hoạch #{plan.Id}";
    }

    public static string ResolveCvContentType(string fileName)
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

    public static Education MapEducation(string? value) => value?.Trim() switch
    {
        "Đại học" or "University" => Education.University,
        "Cao đẳng" or "College" => Education.College,
        "THPT" or "HighSchool" => Education.HighSchool,
        _ => Education.Other,
    };

    public static RecruitmentSource MapSource(string? value) => value?.Trim() switch
    {
        "Facebook" => RecruitmentSource.Facebook,
        "Zalo" => RecruitmentSource.Zalo,
        "24h - lọc" or "24h - ứng tuyển" or "JobPortal24h" => RecruitmentSource.JobPortal24h,
        "TopCV" => RecruitmentSource.TopCV,
        "Glints" => RecruitmentSource.Glints,
        "Nội bộ" or "Internal" => RecruitmentSource.Internal,
        "Giới thiệu" or "Referral" => RecruitmentSource.Referral,
        _ => RecruitmentSource.Other,
    };

    public static string ToClientSource(RecruitmentSource source) => source switch
    {
        RecruitmentSource.JobPortal24h => "24h - ứng tuyển",
        RecruitmentSource.Internal => "Nội bộ",
        RecruitmentSource.Referral => "Giới thiệu",
        RecruitmentSource.Other => "Khác",
        _ => source.ToString(),
    };

    private static DateOnly ParseBirthDate(string value)
    {
        if (DateOnly.TryParse(value, out var iso)) return iso;

        var m = Regex.Match(value, @"^(\d{2})/(\d{2})/(\d{4})$");
        if (m.Success && DateOnly.TryParse($"{m.Groups[3]}-{m.Groups[2]}-{m.Groups[1]}", out var vi))
            return vi;

        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

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

    private static DateTime ResolveAppliedAt(Candidate entity)
    {
        if (entity.AppliedAt.Year >= 2000) return entity.AppliedAt;
        return entity.CreatedAt.Year >= 2000 ? entity.CreatedAt : DateTime.UtcNow;
    }

    private static string ResolveAppliedAtText(Candidate entity) =>
        FormatAppliedAt(ResolveAppliedAt(entity));
}
