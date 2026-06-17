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
    string? DegreePhotoUrl);

public record FileUploadResponse(string Url, string FileName, long FileSize);

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
    string? CvFileUrl = null);

public record CandidatePagedResponse(
    IReadOnlyList<CandidateDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalUnfiltered,
    int UniquePositionCount)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public record RecruitmentPositionDto(string Name, bool IsActive);

public record HrStaffPublicDto(string Id, string Name);
