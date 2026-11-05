using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Api.Controllers.Wms;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;
using System.Security.Claims;
using Xunit;

namespace NamViet.ERP.Tests.Warehouse;

public class OpeningStockAndSupplierImportTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly FakeSupplierGroupService _groupsService;
    private readonly TestCurrentUser _currentUser;
    private readonly SuppliersController _suppliersController;
    private readonly StockController _stockController;

    public OpeningStockAndSupplierImportTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);

        _groupsService = new FakeSupplierGroupService();
        _currentUser = new TestCurrentUser
        {
            Id = Guid.NewGuid(),
            Name = "Quản trị viên",
            Email = "admin@mayzer.com",
            Roles = ["Admin"],
            Permissions = ["admin.users.view:all", "wms.suppliers.view:all", "wms.suppliers.create", "wms.stock.view:all"]
        };

        _suppliersController = new SuppliersController(_db, _groupsService, _currentUser);
        _stockController = new StockController(_db, new FakeInventoryDomainService(), _currentUser);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public void DownloadOpeningBalanceTemplate_ReturnsExcelFile()
    {
        // Act
        var result = _stockController.DownloadOpeningBalanceTemplate();

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
        Assert.Equal("Mau_Nhap_Ton_Kho_Dau_Ky.xlsx", fileResult.FileDownloadName);
        Assert.NotEmpty(fileResult.FileContents);
    }

    [Fact]
    public void DownloadSupplierTemplate_ReturnsExcelFile()
    {
        // Act
        var result = _suppliersController.DownloadExcelTemplate();

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
        Assert.Equal("Mau_Nhap_NhaCungCap.xlsx", fileResult.FileDownloadName);
        Assert.NotEmpty(fileResult.FileContents);
    }

    [Fact]
    public async Task CreateSupplier_WithOnboardingFields_SavesAndReturnsFields()
    {
        // Arrange
        var req = new UpsertSupplierRequest(
            SupplierCode: null,
            SupplierName: "Công ty TNHH Bơm Grundfos Việt Nam",
            ContactPhone: "0912345678",
            MaxDebtDays: 60,
            PaymentTerms: "Net 60",
            CreditLimit: 1_000_000_000m,
            TaxCode: "0301122334",
            SupplierType: "Nhà sản xuất",
            Scope: "Quốc tế",
            Website: "https://grundfos.com");

        // Act
        var result = await _suppliersController.Create(req, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SupplierDto>(okResult.Value);
        Assert.Equal("Nhà sản xuất", dto.SupplierType);
        Assert.Equal("Quốc tế", dto.Scope);
        Assert.Equal("https://grundfos.com", dto.Website);
        Assert.Equal("0301122334", dto.TaxCode);

        var inDb = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == dto.Id);
        Assert.NotNull(inDb);
        Assert.Equal("Nhà sản xuất", inDb.SupplierType);
        Assert.Equal("Quốc tế", inDb.Scope);
        Assert.Equal("https://grundfos.com", inDb.Website);
    }

    private class FakeInventoryDomainService : IInventoryDomainService
    {
        public Task<string> GetGeneralWarehouseIdAsync(CancellationToken ct = default) => Task.FromResult("KHO-TONG");
        public Task<decimal> GetProjectAvailableQtyAsync(string projectCode, string materialCode, string? excludeStockDefectTicketCode = null, CancellationToken ct = default) => Task.FromResult(0m);
        public Task<IReadOnlyDictionary<string, decimal>> GetPendingStockDefectQtyByMaterialAsync(string projectCode, string? excludeStockDefectTicketCode = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<string, decimal>>(new Dictionary<string, decimal>());
        public Task<IReadOnlyList<PendingStockDefectHold>> ListPendingStockDefectHoldsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PendingStockDefectHold>>([]);
        public Task<ReservationResult> CreateReservationAsync(string warehouseId, string projectCode, string materialCode, decimal requestedQty, string sourceTicketCode, CancellationToken ct = default) => Task.FromResult(new ReservationResult(requestedQty, 0));
        public Task FulfillReservationsAsync(string sourceTicketCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task CancelReservationsAsync(string sourceTicketCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task CreateProjectImportPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task CancelProjectImportPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task CreateProjectExportPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task CancelProjectExportPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyApprovedImportAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyApprovedExportAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task CreateProjectExportReturnPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task CancelProjectExportReturnPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyApprovedExportReturnAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyDirectIssueAsync(WarehouseTransaction importTransaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsurePendingDirectExportAsync(WarehouseTransaction importTransaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task HealPendingDirectExportsAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyTransferAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyReturnToGeneralAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyCrossProjectTransferAsync(WarehouseTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyProjectCloseTransferAsync(string projectCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyStocktakeAdjustmentAsync(string projectCode, string materialCode, decimal diffQty, CancellationToken ct = default) => Task.CompletedTask;
        public Task<decimal> GetAvailableQtyAsync(string warehouseId, string materialCode, CancellationToken ct = default) => Task.FromResult(0m);
        public Task EnsureGeneralWarehouseAsync(string? warehouseId = null, CancellationToken ct = default) => Task.CompletedTask;
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
        public string Name { get; set; } = "Quản trị viên";
        public string Email { get; set; } = "admin@mayzer.com";
        public string EmployeeCode => "EMP-ADMIN";
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
