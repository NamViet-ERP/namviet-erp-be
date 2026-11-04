using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;
using System.Security.Claims;
using Xunit;

namespace NamViet.ERP.Tests.Purchasing;

public class SupplierTierEvaluationTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly FakeSupplierGroupService _groupsService;
    private readonly TestCurrentUser _currentUser;
    private readonly SuppliersController _controller;

    public SupplierTierEvaluationTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);

        _groupsService = new FakeSupplierGroupService();
        _currentUser = new TestCurrentUser
        {
            Id = Guid.NewGuid(),
            Name = "Chuyên viên Mua hàng",
            Email = "buyer@mayzer.com"
        };

        _controller = new SuppliersController(_db, _groupsService, _currentUser);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task SubmitTierEvaluation_WithScoreAbove80_PromotesSupplierFromDToC()
    {
        // Arrange
        var supplier = new Supplier
        {
            SupplierCode = "NCC-0001",
            SupplierName = "Công ty TNHH Thép Việt Nhật",
            Rating = SupplierGrades.D,
            ApprovalStatus = SupplierApprovalStatuses.Approved,
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        // 40 price + 20 profile + 15 payment + 10 service = 85 points (>= 80)
        var request = new SubmitTierEvaluationRequest(
            PriceScore: 40m,
            ProfileScore: 20m,
            PaymentScore: 15m,
            ServiceScore: 10m,
            TargetGrade: "C",
            Reason: "Đáp ứng đầy đủ hồ sơ năng lực và báo giá cạnh tranh.",
            DetailsJson: "{\"price\":40,\"profile\":20,\"payment\":15,\"service\":10}"
        );

        // Act
        var result = await _controller.SubmitTierEvaluation(supplier.Id, request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SupplierTierEvaluationDto>(okResult.Value);
        Assert.Equal(85m, dto.TotalScore);
        Assert.Equal(SupplierGrades.D, dto.OldGrade);
        Assert.Equal(SupplierGrades.C, dto.NewGrade);

        var updatedSupplier = await _db.Suppliers.FindAsync(supplier.Id);
        Assert.NotNull(updatedSupplier);
        Assert.Equal(SupplierGrades.C, updatedSupplier.Rating);

        var evaluations = await _db.SupplierTierEvaluations.Where(e => e.SupplierId == supplier.Id).ToListAsync();
        Assert.Single(evaluations);
        Assert.Equal(85m, evaluations[0].TotalScore);
    }

    [Fact]
    public async Task SubmitTierEvaluation_WithScoreBelow80_AndExplicitTargetGrade_ReturnsBadRequest()
    {
        // Arrange
        var supplier = new Supplier
        {
            SupplierCode = "NCC-0002",
            SupplierName = "Công ty Cổ phần Bơm Công Nghiệp",
            Rating = SupplierGrades.D,
            ApprovalStatus = SupplierApprovalStatuses.Approved,
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        // Total = 30 + 10 + 10 + 5 = 55 points (< 80)
        var request = new SubmitTierEvaluationRequest(
            PriceScore: 30m,
            ProfileScore: 10m,
            PaymentScore: 10m,
            ServiceScore: 5m,
            TargetGrade: "C",
            Reason: "Chưa đủ điều kiện nhưng xin thăng hạng."
        );

        // Act
        var result = await _controller.SubmitTierEvaluation(supplier.Id, request, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);

        var unchangedSupplier = await _db.Suppliers.FindAsync(supplier.Id);
        Assert.NotNull(unchangedSupplier);
        Assert.Equal(SupplierGrades.D, unchangedSupplier.Rating);
    }

    [Fact]
    public async Task SubmitTierEvaluation_WithScoreBelow80_WithoutTargetGrade_KeepsOldGrade()
    {
        // Arrange
        var supplier = new Supplier
        {
            SupplierCode = "NCC-0003",
            SupplierName = "Đại lý Van và Ống Thép Hà Nội",
            Rating = SupplierGrades.D,
            ApprovalStatus = SupplierApprovalStatuses.Approved,
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        // Total = 30 + 10 + 10 + 5 = 55 points (< 80)
        var request = new SubmitTierEvaluationRequest(
            PriceScore: 30m,
            ProfileScore: 10m,
            PaymentScore: 10m,
            ServiceScore: 5m,
            TargetGrade: null,
            Reason: "Đánh giá định kỳ mức đạt yêu cầu cơ bản."
        );

        // Act
        var result = await _controller.SubmitTierEvaluation(supplier.Id, request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SupplierTierEvaluationDto>(okResult.Value);
        Assert.Equal(55m, dto.TotalScore);
        Assert.Equal(SupplierGrades.D, dto.OldGrade);
        Assert.Equal(SupplierGrades.D, dto.NewGrade);

        var unchangedSupplier = await _db.Suppliers.FindAsync(supplier.Id);
        Assert.NotNull(unchangedSupplier);
        Assert.Equal(SupplierGrades.D, unchangedSupplier.Rating);
    }

    [Fact]
    public async Task GetGradeHistory_ReturnsAllEvaluationRecords()
    {
        // Arrange
        var supplier = new Supplier
        {
            SupplierCode = "NCC-0004",
            SupplierName = "Công ty TNHH Cáp Điện Nam Hải",
            Rating = SupplierGrades.C,
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        _db.SupplierTierEvaluations.AddRange(
            new SupplierTierEvaluation
            {
                SupplierId = supplier.Id,
                OldGrade = SupplierGrades.D,
                NewGrade = SupplierGrades.C,
                TotalScore = 85m,
                PriceScore = 40m,
                ProfileScore = 20m,
                PaymentScore = 15m,
                ServiceScore = 10m,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
            },
            new SupplierTierEvaluation
            {
                SupplierId = supplier.Id,
                OldGrade = SupplierGrades.C,
                NewGrade = SupplierGrades.B,
                TotalScore = 95m,
                PriceScore = 50m,
                ProfileScore = 20m,
                PaymentScore = 15m,
                ServiceScore = 10m,
                CreatedAt = DateTime.UtcNow,
            }
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _controller.GetGradeHistory(supplier.Id, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<SupplierTierEvaluationDto>>(okResult.Value);
        Assert.Equal(2, list.Count);
        Assert.Equal(95m, list[0].TotalScore); // Most recent first
    }

    [Fact]
    public async Task GetSourcingKpi_CalculatesSummaryMetricsCorrectly()
    {
        // Arrange
        var s1 = new Supplier { SupplierCode = "NCC-0010", SupplierName = "NCC 10", Rating = SupplierGrades.D, CreatedAt = DateTime.UtcNow };
        var s2 = new Supplier { SupplierCode = "NCC-0011", SupplierName = "NCC 11", Rating = SupplierGrades.C, CreatedAt = DateTime.UtcNow };
        var s3 = new Supplier { SupplierCode = "NCC-0012", SupplierName = "NCC 12", Rating = SupplierGrades.B, CreatedAt = DateTime.UtcNow.AddMonths(-2) };
        _db.Suppliers.AddRange(s1, s2, s3);
        await _db.SaveChangesAsync();

        _db.SupplierTierEvaluations.Add(new SupplierTierEvaluation
        {
            SupplierId = s2.Id,
            OldGrade = SupplierGrades.D,
            NewGrade = SupplierGrades.C,
            TotalScore = 82m,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _controller.GetSourcingKpi(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var kpi = Assert.IsType<SourcingKpiSummaryDto>(okResult.Value);
        Assert.Equal(3, kpi.TotalSuppliers);
        Assert.Equal(2, kpi.TotalNewThisMonth);
        Assert.Equal(1, kpi.TotalPromotedThisMonth);
        Assert.Equal(1, kpi.GradeDCount);
        Assert.Equal(1, kpi.GradeCCount);
        Assert.Equal(1, kpi.GradeBCount);
        Assert.Equal(50m, kpi.PromotionRatePercent); // 1 promoted / 2 new = 50%
    }

    private class FakeSupplierGroupService : ISupplierGroupService
    {
        public Task<SupplierGroupPagedResponse> ListAsync(string? search, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(new SupplierGroupPagedResponse([], 0, page, pageSize, 0));

        public Task<SupplierGroupDto> GetAsync(int id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<SupplierGroupDto> CreateAsync(UpsertSupplierGroupRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<SupplierGroupDto> UpdateAsync(int id, UpsertSupplierGroupRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task DeleteAsync(int id, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<SupplierGroupMemberDto>> ListMembersAsync(int groupId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SupplierGroupMemberDto>>([]);

        public Task<IReadOnlyList<SupplierGroupMemberDto>> SetMembersAsync(int groupId, SetSupplierGroupMembersRequest request, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SupplierGroupMemberDto>>([]);

        public Task<IReadOnlyList<SupplierGroupRefDto>> ListGroupsOfSupplierAsync(int supplierId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SupplierGroupRefDto>>([]);

        public Task<IReadOnlyList<SupplierGroupRefDto>> SetGroupsOfSupplierAsync(int supplierId, SetSupplierGroupsRequest request, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SupplierGroupRefDto>>([]);
    }

    private class TestCurrentUser : ICurrentUser
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Test User";
        public string Email { get; set; } = "test@namviet.vn";
        public string EmployeeCode => "EMP-001";
        public int? EmployeeId => null;
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles { get; set; } = [];
        public IReadOnlyList<string> Permissions { get; set; } = [];
        public ClaimsPrincipal Principal => new();

        public bool IsInRole(string role) => Roles.Contains(role);
        public bool HasPermission(string permission) => true;
        public bool HasAllPermissions(params string[] permissions) => true;
        public bool HasAnyPermission(params string[] permissions) => true;
    }
}
