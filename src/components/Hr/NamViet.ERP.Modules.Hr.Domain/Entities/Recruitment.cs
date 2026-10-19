using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Hr.Domain.Entities;

public enum CandidateStatus
{
    New,                // CV mới
    Screening,          // Đang xem xét
    InterviewScheduled, // Đã hẹn PV
    Interview1,         // PV vòng 1
    Interview2,         // PV vòng 2
    InterviewPassed,    // Đạt PV
    Rejected,           // PV không đạt / loại
    Offered,            // Gửi offer
    OfferDeclined,      // Từ chối nhận việc
    Accepted,           // Đã tuyển
    Probation,          // Thử việc
    Official,           // Chính thức
    Cancelled           // Đã hủy (nội bộ)
}

public enum RecruitmentSource
{
    Facebook, Zalo, JobPortal24h, TopCV, Glints, Internal, Referral, Other
}

/// <summary>Vị trí tuyển dụng – dùng cho form submit CV và kế hoạch tuyển</summary>
public class RecruitmentPosition : BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public string? JobDescription { get; set; }     // Mô tả công việc
    public bool IsActive { get; set; } = true;

    public Department? Department { get; set; }
    public ICollection<Candidate> Candidates { get; set; } = [];
}

/// <summary>Kế hoạch tuyển dụng (theo kỳ)</summary>
public class RecruitmentPlan : BaseEntity
{
    public int Id { get; set; }
    public int PositionId { get; set; }
    public int DepartmentId { get; set; }
    public int Headcount { get; set; }
    public string? Reason { get; set; }
    public string? Period { get; set; }             // VD: Q2-2026 / Đợt tuyển
    public DateOnly? OpenDate { get; set; }
    public DateOnly? DeadlineDate { get; set; }
    public string Status { get; set; } = "Draft";  // Draft/Active/Closed

    public RecruitmentPosition Position { get; set; } = null!;
    public ICollection<Candidate> Candidates { get; set; } = [];
}

/// <summary>Ứng viên – nguồn từ form public hrnamviet.com/cv</summary>
public class Candidate : BaseEntity
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly BirthDate { get; set; }
    public string? Address { get; set; }
    public Education Education { get; set; }
    public int? PositionId { get; set; }
    public int? RecruitmentPlanId { get; set; }
    public string? Experience { get; set; }
    public RecruitmentSource Source { get; set; }
    public string? CvFileUrl { get; set; }
    public string? CvFileName { get; set; }
    public CandidateStatus Status { get; set; } = CandidateStatus.New;
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
    public string? WorkflowData { get; set; }

    public RecruitmentPosition? Position { get; set; }
    public RecruitmentPlan? RecruitmentPlan { get; set; }
    public ICollection<CandidateStage> Stages { get; set; } = [];
}

/// <summary>Vòng phỏng vấn của ứng viên</summary>
public class CandidateStage : BaseEntity
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public string Stage { get; set; } = string.Empty;          // CV/Interview1/Interview2/Offer
    public string? Result { get; set; }                         // Pass/Fail/Pending
    public string? Interviewer { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? Note { get; set; }

    public Candidate Candidate { get; set; } = null!;
}

/// <summary>Theo dõi onboarding nhân viên mới</summary>
public class Onboard : BaseEntity
{
    public int Id { get; set; }
    public int? CandidateId { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly StartDate { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Checklist { get; set; }                      // JSON checklist

    public Employee Employee { get; set; } = null!;
}

/// <summary>Chi phí tuyển dụng theo kênh / kỳ</summary>
public class RecruitmentCost : BaseEntity
{
    public int Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? PositionName { get; set; }
    public decimal Amount { get; set; }
    public string Period { get; set; } = string.Empty;
    public int? DurationDays { get; set; }
    public int? PaymentYear { get; set; }
    public int? PaymentMonth { get; set; }
    public string? Note { get; set; }
}

/// <summary>Ngân sách tuyển dụng (một dòng cấu hình theo năm).</summary>
public class RecruitmentBudget : BaseEntity
{
    public int Id { get; set; }
    public int Year { get; set; }
    public decimal TotalAllocated { get; set; }
}
