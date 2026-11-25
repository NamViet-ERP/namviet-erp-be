using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
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

namespace NamViet.ERP.Tests.Purchasing;

/// <summary>
/// Phase 1 (Michael Feathers Characterization Tests): locks existing SuppliersController behavior
/// for Excel template/import, tier evaluation, sourcing KPI, and CRUD/blacklist before decomposition.
/// </summary>
public class SuppliersControllerCharacterizationTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly FakeSupplierGroupService _groupsService = new();
    private readonly TestCurrentUser _currentUser = new();
    private readonly SuppliersController _controller;

    public SuppliersControllerCharacterizationTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"sup-char-{Guid.NewGuid():N}")
            .Options;
        _db = new WarehouseDbContext(options);
        _controller = new SuppliersController(_db, _groupsService, _currentUser);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void DownloadExcelTemplate_ContainsExpectedSheetAndColumns()
    {
        var result = _controller.DownloadExcelTemplate();
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("Mau_Nhap_NhaCungCap.xlsx", fileResult.FileDownloadName);

        using var ms = new MemoryStream(fileResult.FileContents);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("DanhSachNCC");
        Assert.NotNull(ws);
        Assert.Equal("Mã NCC (để trống tự sinh)", ws.Cell(1, 1).GetString());
        Assert.Equal("Tên nhà cung cấp", ws.Cell(1, 2).GetString());
        Assert.Equal("Nhóm ngành hàng", ws.Cell(1, 11).GetString());
    }

    [Fact]
    public async Task ImportExcel_StandardTemplate_GeneratesSequentialCodesAssignsGroupsAndSkipsDuplicates()
    {
        _db.Suppliers.Add(new Supplier
        {
            SupplierCode = "NCC-0005",
            SupplierName = "Công ty Tồn Tại",
            TaxCode = "0100000005",
        });
        _db.SupplierGroups.Add(new SupplierGroup { Name = "Hệ ống thép" });
        await _db.SaveChangesAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("DanhSachNCC");
        ws.Cell(1, 1).Value = "Mã NCC (để trống tự sinh)";
        ws.Cell(1, 2).Value = "Tên nhà cung cấp";
        ws.Cell(1, 3).Value = "Mã số thuế";
        ws.Cell(1, 4).Value = "Số điện thoại";
        ws.Cell(1, 5).Value = "Website";
        ws.Cell(1, 6).Value = "Loại hình";
        ws.Cell(1, 7).Value = "Phạm vi";
        ws.Cell(1, 8).Value = "Hạn nợ (ngày)";
        ws.Cell(1, 9).Value = "Hạn mức nợ (tiền)";
        ws.Cell(1, 10).Value = "Điều khoản thanh toán";
        ws.Cell(1, 11).Value = "Nhóm ngành hàng";

        // Row 2: valid with auto-generated code + matching group
        ws.Cell(2, 1).Value = "";
        ws.Cell(2, 2).Value = "Công ty Thép Mới A";
        ws.Cell(2, 3).Value = "0100000011";
        ws.Cell(2, 4).Value = "0912345678";
        ws.Cell(2, 8).Value = 45;
        ws.Cell(2, 9).Value = 200_000_000;
        ws.Cell(2, 11).Value = "Hệ ống thép";

        // Row 3: valid with auto-generated code (must increment cleanly without N+1 collision)
        ws.Cell(3, 1).Value = "";
        ws.Cell(3, 2).Value = "Công ty Thép Mới B";
        ws.Cell(3, 3).Value = "0100000012";

        // Row 4: duplicate name -> skipped with error
        ws.Cell(4, 1).Value = "";
        ws.Cell(4, 2).Value = "Công ty Tồn Tại";
        ws.Cell(4, 3).Value = "0100000099";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var file = new FormFile(ms, 0, ms.Length, "file", "suppliers.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        };

        var res = await _controller.ImportExcel(file, null, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(res.Result);
        var summary = Assert.IsType<SupplierImportResultDto>(ok.Value);

        Assert.Equal(3, summary.TotalRows);
        Assert.Equal(2, summary.ImportedCount);
        Assert.Equal(1, summary.FailedCount);
        Assert.Single(summary.Errors);

        var imported = await _db.Suppliers
            .Where(s => s.SupplierName == "Công ty Thép Mới A" || s.SupplierName == "Công ty Thép Mới B")
            .OrderBy(s => s.SupplierCode)
            .ToListAsync();
        Assert.Equal(2, imported.Count);
        Assert.Equal("NCC-0006", imported[0].SupplierCode);
        Assert.Equal("NCC-0007", imported[1].SupplierCode);

        var memberships = await _db.SupplierGroupMembers
            .Where(m => m.SupplierId == imported[0].Id)
            .ToListAsync();
        Assert.Single(memberships);
    }

    [Fact]
    public async Task GetSourcingKpi_WhenEmptyAndWithMixedGrades_AggregatesAccurately()
    {
        var emptyRes = await _controller.GetSourcingKpi(CancellationToken.None);
        var emptyKpi = Assert.IsType<SourcingKpiSummaryDto>(Assert.IsType<OkObjectResult>(emptyRes.Result).Value);
        Assert.Equal(0, emptyKpi.TotalSuppliers);
        Assert.Equal(0m, emptyKpi.PromotionRatePercent);

        var now = DateTime.UtcNow;
        var sA = new Supplier { SupplierCode = "NCC-0001", SupplierName = "NCC A", Rating = SupplierGrades.A, CreatedAt = now };
        var sOther = new Supplier { SupplierCode = "NCC-0002", SupplierName = "NCC E", Rating = SupplierGrades.E, CreatedAt = now };
        _db.Suppliers.AddRange(sA, sOther);
        await _db.SaveChangesAsync();

        // Same OldGrade and NewGrade must NOT count as a promotion
        _db.SupplierTierEvaluations.Add(new SupplierTierEvaluation
        {
            SupplierId = sA.Id,
            OldGrade = SupplierGrades.A,
            NewGrade = SupplierGrades.A,
            TotalScore = 90m,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync();

        var res = await _controller.GetSourcingKpi(CancellationToken.None);
        var kpi = Assert.IsType<SourcingKpiSummaryDto>(Assert.IsType<OkObjectResult>(res.Result).Value);
        Assert.Equal(2, kpi.TotalSuppliers);
        Assert.Equal(2, kpi.TotalNewThisMonth);
        Assert.Equal(0, kpi.TotalPromotedThisMonth);
        Assert.Equal(1, kpi.GradeACount);
        Assert.Equal(1, kpi.OtherGradeCount);
    }

    [Fact]
    public async Task SubmitTierEvaluation_ClampsScoresAndAutoPromotesWithoutTargetGrade()
    {
        var missingRes = await _controller.SubmitTierEvaluation(
            9999,
            new SubmitTierEvaluationRequest(40, 20, 15, 10, null, null),
            CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(missingRes.Result);

        var supplier = new Supplier
        {
            SupplierCode = "NCC-0008",
            SupplierName = "NCC Auto Ladder",
            Rating = SupplierGrades.C,
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var res = await _controller.SubmitTierEvaluation(
            supplier.Id,
            new SubmitTierEvaluationRequest(80m, 30m, 25m, 15m, null, "Điểm vượt khung phải clamp"),
            CancellationToken.None);
        var dto = Assert.IsType<SupplierTierEvaluationDto>(Assert.IsType<OkObjectResult>(res.Result).Value);

        Assert.Equal(50m, dto.PriceScore);
        Assert.Equal(20m, dto.ProfileScore);
        Assert.Equal(20m, dto.PaymentScore);
        Assert.Equal(10m, dto.ServiceScore);
        Assert.Equal(100m, dto.TotalScore);
        Assert.Equal(SupplierGrades.C, dto.OldGrade);
        Assert.Equal(SupplierGrades.B, dto.NewGrade);
    }

    [Fact]
    public async Task BlacklistAndDelete_LifecycleBehavesAsExpected()
    {
        var s = new Supplier { SupplierCode = "NCC-0009", SupplierName = "NCC Blacklist Test" };
        _db.Suppliers.Add(s);
        await _db.SaveChangesAsync();

        var blRes = await _controller.AddToBlacklist(s.Id, new BlacklistSupplierRequest("Chậm giao hàng nhiều lần"), CancellationToken.None);
        var blDto = Assert.IsType<SupplierDto>(Assert.IsType<OkObjectResult>(blRes.Result).Value);
        Assert.True(blDto.IsBlacklisted);

        var listBl = await _controller.ListBlacklist(CancellationToken.None);
        var blItems = Assert.IsAssignableFrom<IReadOnlyList<SupplierBlacklistDto>>(Assert.IsType<OkObjectResult>(listBl.Result).Value);
        Assert.Single(blItems);

        var unBlRes = await _controller.RemoveFromBlacklist(s.Id, CancellationToken.None);
        var unBlDto = Assert.IsType<SupplierDto>(Assert.IsType<OkObjectResult>(unBlRes.Result).Value);
        Assert.False(unBlDto.IsBlacklisted);

        var delRes = await _controller.Delete(s.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(delRes);
        Assert.Null(await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == s.Id));
    }

    private sealed class FakeSupplierGroupService : ISupplierGroupService
    {
        public Task<SupplierGroupPagedResponse> ListAsync(string? search, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(new SupplierGroupPagedResponse([], 0, page, pageSize, 0));
        public Task<SupplierGroupDto> GetAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SupplierGroupDto> CreateAsync(UpsertSupplierGroupRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SupplierGroupDto> UpdateAsync(int id, UpsertSupplierGroupRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SupplierGroupMemberDto>> ListMembersAsync(int groupId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SupplierGroupMemberDto>>([]);
        public Task<IReadOnlyList<SupplierGroupMemberDto>> SetMembersAsync(int groupId, SetSupplierGroupMembersRequest request, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SupplierGroupMemberDto>>([]);
        public Task<IReadOnlyList<SupplierGroupRefDto>> ListGroupsOfSupplierAsync(int supplierId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SupplierGroupRefDto>>([]);
        public Task<IReadOnlyList<SupplierGroupRefDto>> SetGroupsOfSupplierAsync(int supplierId, SetSupplierGroupsRequest request, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SupplierGroupRefDto>>([]);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        private readonly ClaimsPrincipal _principal = new();

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Test Buyer";
        public string Email { get; set; } = "buyer@mayzer.com";
        public string EmployeeCode => "EMP-01";
        public int? EmployeeId => null;
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles { get; set; } = [];
        public IReadOnlyList<string> Permissions { get; set; } = [];
        public ClaimsPrincipal Principal => _principal;
        public bool IsInRole(string role) => Roles.Contains(role);
        public bool HasPermission(string permission) => true;
        public bool HasAllPermissions(params string[] permissions) => permissions.All(HasPermission);
        public bool HasAnyPermission(params string[] permissions) => permissions.Any(HasPermission);
    }
}
