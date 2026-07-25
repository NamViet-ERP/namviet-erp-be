using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Api.Application.Validators;
using NamViet.ERP.Api.Middleware;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Authorization;

namespace NamViet.ERP.Tests.Hr;

public class RecruitmentP1P2Tests
{
    [Theory]
    [InlineData(0, 2026, 7)]
    [InlineData(20, 1999, 7)]
    [InlineData(20, 2026, 13)]
    [InlineData(4000, 2026, 7)]
    public void Cost_validator_rejects_invalid_duration_year_or_month(
        int durationDays,
        int paymentYear,
        int paymentMonth)
    {
        var request = new CreateRecruitmentCostRequest(
            "TopCV",
            1_000_000m,
            "07/2026",
            DurationDays: durationDays,
            PaymentYear: paymentYear,
            PaymentMonth: paymentMonth);

        var result = new CreateRecruitmentCostRequestValidator().Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Cost_validator_rejects_month_without_year()
    {
        var request = new CreateRecruitmentCostRequest(
            "TopCV",
            1_000_000m,
            "07/2026",
            PaymentMonth: 7);

        var result = new CreateRecruitmentCostRequestValidator().Validate(request);

        Assert.Contains(result.Errors, error => error.PropertyName == "PaymentMonth");
    }

    [Fact]
    public void Plan_validator_rejects_clamped_or_defaulted_values()
    {
        var request = new CreateRecruitmentPlanRequest(
            "",
            "Kỹ thuật",
            0,
            null,
            null,
            "not-a-date",
            "2026-06-01",
            "Unknown");

        var result = new CreateRecruitmentPlanRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Headcount");
        Assert.Contains(result.Errors, error => error.PropertyName == "Status");
        Assert.Contains(result.Errors, error => error.PropertyName == "OpenDate");
    }

    [Fact]
    public void Budget_validator_rejects_out_of_range_year_and_amount()
    {
        var request = new UpdateRecruitmentBudgetRequest(
            1900,
            MoneyValueLimits.Numeric18Scale2Max + 1);

        var result = new UpdateRecruitmentBudgetRequestValidator().Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Date_range_validator_rejects_reversed_range()
    {
        var request = new RecruitmentDateRangeQuery(
            new DateOnly(2026, 7, 2),
            new DateOnly(2026, 7, 1));

        var result = new RecruitmentDateRangeQueryValidator().Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Kpi_counts_only_hired_candidates_linked_to_active_plans()
    {
        var options = new DbContextOptionsBuilder<HrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new HrDbContext(options);
        var firstPlan = new RecruitmentPlan { PositionId = 1, DepartmentId = 1, Headcount = 2, Status = "Active" };
        var secondPlan = new RecruitmentPlan { PositionId = 1, DepartmentId = 1, Headcount = 2, Status = "Active" };
        db.RecruitmentPlans.AddRange(firstPlan, secondPlan);
        await db.SaveChangesAsync();
        db.Candidates.AddRange(
            CandidateWithStatus(CandidateStatus.Accepted, firstPlan.Id),
            CandidateWithStatus(CandidateStatus.Probation, firstPlan.Id),
            CandidateWithStatus(CandidateStatus.Official, secondPlan.Id),
            CandidateWithStatus(CandidateStatus.Accepted),
            CandidateWithStatus(CandidateStatus.Rejected, firstPlan.Id));
        await db.SaveChangesAsync();
        var service = new RecruitmentAnalyticsService(
            db,
            NullLogger<RecruitmentAnalyticsService>.Instance);

        var result = await service.GetKpiAsync();

        Assert.Equal(3, result.TotalHired);
        Assert.Equal(2, result.OpenPlanCount);
        Assert.Equal(1, result.TotalRemaining);
    }

    [Fact]
    public async Task Exception_middleware_does_not_leak_invalid_operation_message()
    {
        const string sensitiveMessage = "database row 42 is in secret state";
        var middleware = new ExceptionMiddleware(
            _ => throw new InvalidOperationException(sensitiveMessage),
            NullLogger<ExceptionMiddleware>.Instance,
            new TestHostEnvironment());
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, context.Response.StatusCode);
        Assert.DoesNotContain(sensitiveMessage, body.RootElement.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("hrm.insurance.manage")]
    [InlineData("hrm.leaves.approve")]
    [InlineData("wms.transactions.create")]
    [InlineData("wms.transactions.approve")]
    public void Migrated_controller_permissions_are_catalogued(string permission)
    {
        Assert.True(PermissionCatalog.IsValid(permission));
    }

    [Fact]
    public void Legacy_specialist_roles_receive_replacement_permissions()
    {
        Assert.Contains(
            "wms.transactions.approve",
            RolePermissionTemplates.ForIdentityRole(RolePermissionTemplates.AccountantRole));
        Assert.Contains(
            "wms.transactions.create",
            RolePermissionTemplates.ForIdentityRole(RolePermissionTemplates.ProcurementRole));
        Assert.Contains(
            "hrm.leaves.approve",
            RolePermissionTemplates.ForIdentityRole(RolePermissionTemplates.BodRole));
    }

    [Fact]
    public void Candidate_plan_policy_rejects_position_mismatch_and_deleted_plan()
    {
        var candidate = CandidateWithStatus(CandidateStatus.Accepted);
        candidate.PositionId = 10;

        var mismatched = new RecruitmentPlan { Id = 1, PositionId = 11 };
        Assert.Contains("không phù hợp", CandidateRecruitmentPlanPolicy.Validate(candidate, mismatched));

        var deleted = new RecruitmentPlan { Id = 2, PositionId = 10, DeletedAt = DateTime.UtcNow };
        Assert.Contains("đã bị xóa", CandidateRecruitmentPlanPolicy.Validate(candidate, deleted));
    }

    private static Candidate CandidateWithStatus(CandidateStatus status, int? recruitmentPlanId = null) =>
        new()
        {
            FullName = status.ToString(),
            Phone = "0900000000",
            Email = $"{status.ToString().ToLowerInvariant()}@example.com",
            PositionId = 1,
            RecruitmentPlanId = recruitmentPlanId,
            Status = status,
        };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
