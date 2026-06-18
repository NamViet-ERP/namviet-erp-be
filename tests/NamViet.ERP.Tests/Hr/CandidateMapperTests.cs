using NamViet.ERP.Api.Application.Mappers.Hr;
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
}
