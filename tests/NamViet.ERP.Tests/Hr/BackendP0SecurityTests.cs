using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Application.Mappers.Hr;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Api.Application.Validators;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Tests.Hr;

public class BackendP0SecurityTests
{
    [Fact]
    public async Task Invite_token_is_hashed_and_lifecycle_is_enforced()
    {
        await using var db = CreateDb();
        var service = new EmployeeOnboardingInviteService(db);
        var (invite, token) = await service.CreateAsync(Guid.NewGuid(), DateTime.UtcNow.AddHours(1), default);

        Assert.NotEqual(token, invite.TokenHash);
        Assert.Equal(64, invite.TokenHash.Length);
        Assert.NotNull(await service.FindValidAsync(token, default));

        invite.ConsumedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Assert.Null(await service.FindValidAsync(token, default));
    }

    [Fact]
    public async Task Expired_or_revoked_invite_is_invalid()
    {
        await using var db = CreateDb();
        var service = new EmployeeOnboardingInviteService(db);
        var expiredToken = "expired-token";
        db.EmployeeOnboardingInvites.Add(new EmployeeOnboardingInvite
        {
            TokenHash = EmployeeOnboardingInviteService.HashToken(expiredToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedByUserId = Guid.NewGuid(),
        });
        var revokedToken = "revoked-token";
        db.EmployeeOnboardingInvites.Add(new EmployeeOnboardingInvite
        {
            TokenHash = EmployeeOnboardingInviteService.HashToken(revokedToken),
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            RevokedAt = DateTime.UtcNow,
            CreatedByUserId = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        Assert.Null(await service.FindValidAsync(expiredToken, default));
        Assert.Null(await service.FindValidAsync(revokedToken, default));
    }

    [Fact]
    public void Public_candidate_contract_always_maps_to_new()
    {
        var request = new PublicCandidateApplicationRequest(
            "Nguyen Van A", "0900000000", "a@example.com", "Nam", "", "2000-01-01",
            "University", "", "", "Engineer", "1 year", "Referral");

        var dto = CandidateMapper.FromPublicApplication(request);

        Assert.Equal("New", dto.Status);
        Assert.Equal(RecruitmentSource.Referral, CandidateMapper.MapSource(dto.Source));
    }

    [Theory]
    [InlineData("Internal", RecruitmentSource.Internal, "Nội bộ")]
    [InlineData("Nội bộ", RecruitmentSource.Internal, "Nội bộ")]
    [InlineData("Referral", RecruitmentSource.Referral, "Giới thiệu")]
    [InlineData("Giới thiệu", RecruitmentSource.Referral, "Giới thiệu")]
    public void Candidate_source_roundtrips(
        string input, RecruitmentSource expected, string clientValue)
    {
        var mapped = CandidateMapper.MapSource(input);
        Assert.Equal(expected, mapped);
        Assert.Equal(clientValue, CandidateMapper.ToClientSource(mapped));
    }

    [Fact]
    public void Date_only_range_uses_vietnam_calendar_day_as_utc_bounds()
    {
        var date = new DateOnly(2026, 7, 17);
        var start = UtcDateRange.Start(date);
        var end = UtcDateRange.ExclusiveEnd(date);

        Assert.Equal(DateTimeKind.Utc, start!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, end!.Value.Kind);
        // 00:00 VN (UTC+7) = 17:00 ngày trước theo UTC
        Assert.Equal(new DateTime(2026, 7, 16, 17, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 7, 17, 17, 0, 0, DateTimeKind.Utc), end);
        Assert.Equal(TimeSpan.FromHours(24), end.Value - start.Value);
    }

    [Fact]
    public void Numeric_18_2_amount_bounds_are_enforced()
    {
        Assert.False(MoneyValueLimits.IsValidNumeric18Scale2(-1m));
        Assert.True(MoneyValueLimits.IsValidNumeric18Scale2(0m));
        Assert.True(MoneyValueLimits.IsValidNumeric18Scale2(
            MoneyValueLimits.Numeric18Scale2Max));
        Assert.False(MoneyValueLimits.IsValidNumeric18Scale2(
            MoneyValueLimits.Numeric18Scale2Max + 0.01m));
    }

    [Fact]
    public void Upload_validation_requires_extension_mime_and_magic_bytes_to_match()
    {
        var pdf = "%PDF-1.7 test"u8.ToArray();

        Assert.True(HrUploadValidator.IsValid(
            "candidate.pdf", "application/pdf", pdf, cvOnly: true));
        Assert.False(HrUploadValidator.IsValid(
            "candidate.docx", "application/pdf", pdf, cvOnly: true));
        Assert.False(HrUploadValidator.IsValid(
            "candidate.pdf", "application/pdf", "not a pdf"u8.ToArray(), cvOnly: true));
    }

    [Theory]
    [InlineData("hr/onboard/42/session/file.jpg", true)]
    [InlineData("/uploads/hr/onboard/42/session/file.jpg", true)]
    [InlineData("hr/onboard/4/session/file.jpg", false)]
    [InlineData("hr/onboard/42/../43/file.jpg", false)]
    [InlineData("hr/employees/42/file.jpg", false)]
    [InlineData("", false)]
    public void Onboarding_attachment_key_must_belong_to_invite(string key, bool expected)
    {
        Assert.Equal(expected,
            HrStorageKeyPolicy.IsOnboardingAttachmentOwnedByInvite(key, 42));
    }

    [Fact]
    public void Upload_path_segments_are_sanitized()
    {
        Assert.Equal("session-01_safe",
            HrStorageKeyPolicy.SanitizeSegment("../session-01_safe"));
    }

    [Fact]
    public async Task Recruitment_cost_global_filter_excludes_soft_deleted_rows()
    {
        await using var db = CreateDb();
        db.RecruitmentCosts.AddRange(
            new RecruitmentCost { Source = "TopCV", Period = "2026", Amount = 1 },
            new RecruitmentCost
            {
                Source = "Facebook", Period = "2026", Amount = 2, DeletedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var rows = await db.RecruitmentCosts.ToListAsync();

        Assert.Single(rows);
        Assert.Equal("TopCV", rows[0].Source);
    }

    private static HrDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<HrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HrDbContext(options);
    }
}
