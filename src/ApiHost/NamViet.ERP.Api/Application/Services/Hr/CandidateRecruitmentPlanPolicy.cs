using NamViet.ERP.Modules.Hr.Domain.Entities;

namespace NamViet.ERP.Api.Application.Services.Hr;

public static class CandidateRecruitmentPlanPolicy
{
    public static string? Validate(Candidate candidate, RecruitmentPlan plan)
    {
        if (plan.DeletedAt is not null)
            return "Kế hoạch tuyển dụng không tồn tại hoặc đã bị xóa.";

        if (candidate.PositionId != plan.PositionId)
            return $"Kế hoạch tuyển dụng không phù hợp với vị trí ứng tuyển \"{candidate.Position?.Name ?? "chưa xác định"}\".";

        return null;
    }
}
