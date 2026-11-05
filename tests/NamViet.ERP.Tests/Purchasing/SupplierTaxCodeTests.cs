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

public class SupplierTaxCodeTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly FakeSupplierGroupService _groupsService;
    private readonly TestCurrentUser _currentUser;
    private readonly SuppliersController _controller;

    public SupplierTaxCodeTests()
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
    public async Task Create_WithTaxCode_SavesAndReturnsTaxCode()
    {
        // Arrange
        var req = new UpsertSupplierRequest(
            SupplierCode: null,
            SupplierName: "Công ty Cổ phần Thép Pomina",
            ContactPhone: "0912345678",
            MaxDebtDays: 45,
            PaymentTerms: "Net 45",
            CreditLimit: 500_000_000m,
            TaxCode: "0302345678");

        // Act
        var result = await _controller.Create(req, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SupplierDto>(okResult.Value);
        Assert.Equal("0302345678", dto.TaxCode);

        var inDb = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == dto.Id);
        Assert.NotNull(inDb);
        Assert.Equal("0302345678", inDb.TaxCode);
    }

    [Fact]
    public async Task Update_WithTaxCode_UpdatesTaxCodeSuccessfully()
    {
        // Arrange
        var supplier = new Supplier
        {
            SupplierCode = "NCC-0002",
            SupplierName = "Công ty TNHH Ống Thép Hòa Phát",
            ContactPhone = "0987654321",
            TaxCode = "0100123456",
            Rating = SupplierGrades.D,
            ApprovalStatus = SupplierApprovalStatuses.Approved,
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var updateReq = new UpsertSupplierRequest(
            SupplierCode: "NCC-0002",
            SupplierName: "Công ty TNHH Ống Thép Hòa Phát",
            ContactPhone: "0987654321",
            MaxDebtDays: 60,
            PaymentTerms: "Net 60",
            CreditLimit: 1_000_000_000m,
            TaxCode: "0100123456-001");

        // Act
        var updateResult = await _controller.Update(supplier.Id, updateReq, CancellationToken.None);
        Assert.IsType<NoContentResult>(updateResult);

        // Assert
        var getResult = await _controller.GetById(supplier.Id, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(getResult.Result);
        var dto = Assert.IsType<SupplierDto>(okResult.Value);
        Assert.Equal("0100123456-001", dto.TaxCode);
    }

    [Fact]
    public async Task GetAll_FilterBySearchTaxCode_FindsSupplier()
    {
        // Arrange
        var s1 = new Supplier
        {
            SupplierCode = "NCC-0010",
            SupplierName = "Công ty Nhôm Định Hình",
            TaxCode = "0315986604",
            Rating = SupplierGrades.D,
            ApprovalStatus = SupplierApprovalStatuses.Approved,
        };
        var s2 = new Supplier
        {
            SupplierCode = "NCC-0011",
            SupplierName = "Công ty Bulong Ốc Vít",
            TaxCode = "0400998877",
            Rating = SupplierGrades.D,
            ApprovalStatus = SupplierApprovalStatuses.Approved,
        };
        _db.Suppliers.AddRange(s1, s2);
        await _db.SaveChangesAsync();

        // Act - search by partial tax code "5986604"
        var result = await _controller.GetAll(page: 1, pageSize: 20, search: "5986604", ct: CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedResult<SupplierDto>>(okResult.Value);
        Assert.Single(paged.Items);
        Assert.Equal("0315986604", paged.Items[0].TaxCode);
        Assert.Equal("NCC-0010", paged.Items[0].SupplierCode);
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
