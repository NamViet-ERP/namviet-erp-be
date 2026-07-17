using NamViet.ERP.Api.Application.Mappers.Hr;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Modules.Hr.Domain.Entities;

namespace NamViet.ERP.Tests.Hr;

public class CandidateMapperTests
{
    [Theory]
    [InlineData("Facebook", RecruitmentSource.Facebook)]
    [InlineData("TopCV", RecruitmentSource.TopCV)]
    [InlineData("Unknown", RecruitmentSource.Other)]
    public void MapSource_maps_known_values(string input, RecruitmentSource expected)
    {
        Assert.Equal(expected, CandidateMapper.MapSource(input));
    }

    [Theory]
    [InlineData("Đại học", Education.University)]
    [InlineData("THPT", Education.HighSchool)]
    [InlineData("", Education.Other)]
    public void MapEducation_maps_known_values(string input, Education expected)
    {
        Assert.Equal(expected, CandidateMapper.MapEducation(input));
    }

    [Fact]
    public void ResolveCvContentType_returns_pdf_for_pdf_extension()
    {
        Assert.Equal("application/pdf", CandidateMapper.ResolveCvContentType("cv.pdf"));
    }

    [Fact]
    public void BuildFallbackDto_maps_linked_recruitment_plan()
    {
        var entity = new Candidate
        {
            Id = 12,
            FullName = "Ứng viên",
            RecruitmentPlanId = 7,
            RecruitmentPlan = new RecruitmentPlan
            {
                Id = 7,
                Period = "Q3-2026",
                Position = new RecruitmentPosition { Name = "Kỹ sư" },
            },
        };

        var dto = CandidateMapper.BuildFallbackDto(entity);

        Assert.Equal("7", dto.RecruitmentPlanId);
        Assert.Equal("Kỹ sư - Q3-2026", dto.RecruitmentPlanLabel);
    }

    [Fact]
    public void Public_application_cannot_supply_an_internal_recruitment_plan()
    {
        var request = new PublicCandidateApplicationRequest(
            "Ứng viên", "0900000000", "candidate@example.com", "Nam", "Hà Nội",
            "01/01/2000", "Đại học", "", "", "Kỹ sư", "2", "TopCV");

        var dto = CandidateMapper.FromPublicApplication(request);

        Assert.Null(dto.RecruitmentPlanId);
        Assert.Null(dto.RecruitmentPlanLabel);
    }
}
