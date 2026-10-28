namespace NamViet.ERP.Api.Application.Dtos.Hr;

public record EmployeeDto(
    string Id,
    string? Code,
    string FullName,
    string Gender,
    string BirthDate,
    string JoinDate,
    string? OfficialDate,
    string Status,
    string? ResignDate,
    string? Segment,
    string? Branch,
    string? Department,
    string? Position,
    string? PersonalEmail,
    string? PersonalPhone,
    string? PermanentAddress,
    string? TemporaryAddress,
    string? Education,
    string? School,
    string? Major,
    string? BankAccountName,
    string? BankAccountNumber,
    string? BankName,
    string? BankBranch,
    string? HadSocialInsurance,
    string? SiNumber,
    string? CccdNumber,
    string? CccdIssueDate,
    string? CccdIssuePlace,
    string? CmndNumber,
    string? CmndIssueDate,
    string? CmndIssuePlace,
    string? MaritalStatus,
    string? TaxCode,
    string? PhotoUrl,
    string? CccdFrontUrl,
    string? CccdBackUrl,
    string? DegreePhotoUrl,
    string? LinkedAccountStatus = null,
    int? DepartmentId = null);

public record FileUploadResponse(string Url, string FileName, long FileSize);

public record CreateEmployeeOnboardingInviteRequest(DateTimeOffset? ExpiresAt = null);

public record EmployeeOnboardingInviteDto(
    int Id,
    DateTime ExpiresAt,
    DateTime? ConsumedAt,
    DateTime? RevokedAt,
    Guid CreatedByUserId,
    int? ConsumedEmployeeId,
    string? Token = null);

public record EmployeeOnboardingInviteValidationDto(bool Valid, DateTime? ExpiresAt);

public record CandidateDto(
    string Id,
    string FullName,
    string Phone,
    string Email,
    string Gender,
    string Address,
    string BirthDate,
    string Education,
    string School,
    string Major,
    string Position,
    string Experience,
    string Source,
    string HrResponsible,
    string CvFileName,
    string CvFileSize,
    string AppliedAt,
    string? ScreeningType,
    string? TargetPosition,
    string? TargetDepartment,
    string? PreScreenResult,
    string? ContactResult,
    string? InterviewDate,
    string? InterviewAttended,
    string? Evaluator,
    string? EvaluationResult,
    string? OfferStatus,
    string? OfferRejectReason,
    string? StartDate,
    string? OnboardingAttended,
    string? OnboardingRejectReason,
    string? InterviewTime = null,
    string? OfferSalary = null,
    string Status = "New",
    string? CvFileUrl = null,
    string? RecruitmentPlanId = null,
    string? RecruitmentPlanLabel = null);

public record PublicCandidateApplicationRequest(
    string FullName,
    string Phone,
    string Email,
    string Gender,
    string Address,
    string BirthDate,
    string Education,
    string School,
    string Major,
    string Position,
    string Experience,
    string Source,
    string? CvFileName = null,
    string? CvFileSize = null,
    string? CvFileUrl = null,
    string? HrResponsible = null);

public record UpdateCandidateStatusRequest(
    string Status,
    string? InterviewDate = null,
    string? InterviewTime = null,
    string? Evaluator = null,
    string? StartDate = null,
    string? OfferSalary = null,
    string? OfferNote = null,
    string? RejectReason = null,
    string? RecruitmentPlanId = null);

public record RecruitmentPlanDto(
    string Id,
    string PositionName,
    string DepartmentName,
    int Headcount,
    int HiredCount,
    int RemainingCount,
    string? Reason,
    string? Period,
    string? OpenDate,
    string? DeadlineDate,
    string Status,
    int? DepartmentId = null);

public record CreateRecruitmentPlanRequest(
    string PositionName,
    string? DepartmentName,
    int Headcount,
    string? Reason,
    string? Period,
    string? OpenDate,
    string? DeadlineDate,
    string Status = "Active",
    int? DepartmentId = null);

public record UpdateRecruitmentPlanRequest(
    string PositionName,
    string? DepartmentName,
    int Headcount,
    string? Reason,
    string? Period,
    string? OpenDate,
    string? DeadlineDate,
    string Status,
    int? DepartmentId = null);

public record CandidatePagedResponse(
    IReadOnlyList<CandidateDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalUnfiltered,
    int UniquePositionCount,
    int NewCvCount = 0)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public record RecruitmentPositionDto(string Name, bool IsActive);

// Endpoint public (form ứng tuyển): chỉ trả tên, không lộ userId nội bộ.
public record HrStaffPublicDto(string Name);

public record RecruitmentKpiDto(
    int TotalCandidates,
    int NewCount,
    int ScreeningCount,
    int InterviewCount,
    int OfferedCount,
    int AcceptedCount,
    int RejectedCount,
    int TotalHeadcount,
    int TotalHired,
    int TotalRemaining,
    int InterviewsScheduled,
    int OpenPlanCount,
    int InterviewedCount);

public record RecruitmentTrendDto(string Month, int Applications, int Hires);

public record RecruitmentSourceStatDto(string Source, int Count);

public record RecruitmentPositionStatDto(string Position, int Count);

public record RecruitmentCalendarEventDto(
    string CandidateId,
    string FullName,
    string Phone,
    string Email,
    string Position,
    string Source,
    string AppliedAt,
    string Status,
    string InterviewDate,
    string? InterviewTime,
    string? Evaluator,
    string? HrResponsible);

public record RecruitmentCostDto(
    string Id,
    string Source,
    decimal Amount,
    string Period,
    string? PositionName,
    int? DurationDays,
    int? PaymentYear,
    int? PaymentMonth,
    string? Note);

public record CreateRecruitmentCostRequest(
    string Source,
    decimal Amount,
    string Period,
    string? PositionName = null,
    int? DurationDays = null,
    int? PaymentYear = null,
    int? PaymentMonth = null,
    string? Note = null);

public record UpdateRecruitmentCostRequest(
    string Source,
    decimal Amount,
    string Period,
    string? PositionName = null,
    int? DurationDays = null,
    int? PaymentYear = null,
    int? PaymentMonth = null,
    string? Note = null);

public record RecruitmentCostSummaryDto(
    string Source,
    decimal TotalAmount,
    int HireCount,
    decimal? CostPerHire);

public record RecruitmentBudgetDto(
    int Year,
    decimal TotalAllocated,
    decimal TotalUsed,
    decimal Remaining);

public record UpdateRecruitmentBudgetRequest(
    int Year,
    decimal TotalAllocated);

public record RecruitmentDateRangeQuery(
    DateOnly? DateFrom,
    DateOnly? DateTo);

public record RecruitmentTrendQuery(int Months);

public record RecruitmentCostListQuery(
    string? Source,
    string? Position,
    int? Year,
    int? Month);

public record RecruitmentYearQuery(int? Year);

public record RecruitmentReportSourceRowDto(
    string Source,
    int CvCount,
    int InterviewCount,
    int HiredCount,
    double ConversionRate);

public record RecruitmentReportPositionRowDto(string Position, int CvCount);

public record RecruitmentReportSummaryDto(
    IReadOnlyList<RecruitmentReportSourceRowDto> BySource,
    IReadOnlyList<RecruitmentReportPositionRowDto> ByPosition);
