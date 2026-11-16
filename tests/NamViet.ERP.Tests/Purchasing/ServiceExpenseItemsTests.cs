using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Application.Validators;
using NamViet.ERP.Api.Controllers.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Tests.Wms;
using Xunit;

namespace NamViet.ERP.Tests.Purchasing;

public class ServiceExpenseItemsTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly FakeCurrentUser _user = new();
    private readonly PurchasePlanService _planService;
    private readonly ServiceExpenseItemsController _controller;

    public ServiceExpenseItemsTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"service-items-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new WarehouseDbContext(options);

        _planService = new PurchasePlanService(
            _db,
            new PurchasePlanCodeGenerator(_db),
            _user,
            new FakeActivityEventWriter(),
            new CreatePurchasePlanRequestValidator(),
            new UpdatePurchasePlanRequestValidator());

        _controller = new ServiceExpenseItemsController(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Create_ValidRequest_CreatesItemSuccessfully()
    {
        var req = new CreateServiceExpenseItemRequest(
            Code: "DV-VC-01",
            Name: "Chi phí cẩu tháp vận chuyển",
            Type: "service",
            Unit: "Chuyến",
            ReferencePrice: 3500000m,
            Description: "Thuê xe cẩu vận chuyển",
            IsActive: true,
            SortOrder: 1);

        var actionResult = await _controller.Create(req, CancellationToken.None);
        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var dto = Assert.IsType<ServiceExpenseItemDto>(createdResult.Value);

        Assert.Equal("DV-VC-01", dto.Code);
        Assert.Equal("Chi phí cẩu tháp vận chuyển", dto.Name);
        Assert.Equal("service", dto.Type);
        Assert.Equal("Chuyến", dto.Unit);
        Assert.Equal(3500000m, dto.ReferencePrice);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsConflict()
    {
        _db.ServiceExpenseItems.Add(new ServiceExpenseItem
        {
            Code = "DV-VC-01",
            Name = "Chi phí vận chuyển",
            Type = "service",
            Unit = "Chuyến",
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var req = new CreateServiceExpenseItemRequest(
            Code: "DV-VC-01",
            Name: "Trùng mã vận chuyển",
            Type: "service",
            Unit: "Chuyến",
            ReferencePrice: null,
            Description: null);

        var result = await _controller.Create(req, CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_ValidRequest_UpdatesItem()
    {
        var item = new ServiceExpenseItem
        {
            Code = "CP-TK-01",
            Name = "Tiếp khách",
            Type = "expense",
            Unit = "Lần",
            ReferencePrice = 1000000m,
            IsActive = true
        };
        _db.ServiceExpenseItems.Add(item);
        await _db.SaveChangesAsync();

        var req = new UpdateServiceExpenseItemRequest(
            Code: "CP-TK-01",
            Name: "Chi phí tiếp khách và quan hệ đối tác",
            Type: "expense",
            Unit: "Lần",
            ReferencePrice: 2000000m,
            Description: "Cập nhật định mức",
            IsActive: true,
            SortOrder: 2);

        var result = await _controller.Update(item.Id, req, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ServiceExpenseItemDto>(okResult.Value);

        Assert.Equal("Chi phí tiếp khách và quan hệ đối tác", dto.Name);
        Assert.Equal(2000000m, dto.ReferencePrice);
    }

    [Fact]
    public async Task ToggleActive_FlipsStatus()
    {
        var item = new ServiceExpenseItem
        {
            Code = "DV-TEST-01",
            Name = "Dịch vụ test",
            Type = "service",
            Unit = "Gói",
            IsActive = true
        };
        _db.ServiceExpenseItems.Add(item);
        await _db.SaveChangesAsync();

        var result = await _controller.ToggleActive(item.Id, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ServiceExpenseItemDto>(okResult.Value);

        Assert.False(dto.IsActive);
    }

    [Fact]
    public async Task Delete_WhenReferencedInPurchasePlan_ReturnsConflict()
    {
        var item = new ServiceExpenseItem
        {
            Code = "DV-KD-01",
            Name = "Kiểm định áp lực",
            Type = "service",
            Unit = "Lần",
            IsActive = true
        };
        _db.ServiceExpenseItems.Add(item);

        _db.PurchasePlanLines.Add(new PurchasePlanLine
        {
            PlanCode = "KHMH-0001",
            LineNo = 1,
            LineType = "service",
            MaterialCode = "DV-KD-01",
            ProductName = "Kiểm định áp lực",
            Unit = "Lần",
            Quantity = 1
        });
        await _db.SaveChangesAsync();

        var result = await _controller.Delete(item.Id, CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Delete_WhenNotReferenced_Succeeds()
    {
        var item = new ServiceExpenseItem
        {
            Code = "DV-DELETE-01",
            Name = "Dịch vụ xóa",
            Type = "service",
            Unit = "Lần",
            IsActive = true
        };
        _db.ServiceExpenseItems.Add(item);
        await _db.SaveChangesAsync();

        var result = await _controller.Delete(item.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task List_FilterByTypeAndSearch_ReturnsFiltered()
    {
        _db.ServiceExpenseItems.AddRange(
            new ServiceExpenseItem { Code = "DV-01", Name = "Dịch vụ kiểm tra", Type = "service", Unit = "Lần" },
            new ServiceExpenseItem { Code = "DV-02", Name = "Dịch vụ cẩu hàng", Type = "service", Unit = "Chuyến" },
            new ServiceExpenseItem { Code = "CP-01", Name = "Chi phí tiếp khách", Type = "expense", Unit = "Lần" });
        await _db.SaveChangesAsync();

        var res = await _controller.List(page: 1, pageSize: 10, type: "service", ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(res.Result);
        var paged = Assert.IsType<ServiceExpenseItemPagedResponse>(ok.Value);

        Assert.Equal(2, paged.Total);
        Assert.All(paged.Items, x => Assert.Equal("service", x.Type));
    }

    [Fact]
    public async Task CreatePlanAsync_WhenLineHasServiceExpenseItemCode_AcceptsWithoutMaterialInWarehouse()
    {
        _db.Projects.Add(new Project { ProjectCode = "DA-EPC-01", ProjectName = "Dự án EPC Nam Việt" });
        _db.ServiceExpenseItems.Add(new ServiceExpenseItem
        {
            Code = "DV-VC-01",
            Name = "Chi phí cẩu tháp vận chuyển",
            Type = "service",
            Unit = "Chuyến",
            ReferencePrice = 3500000m,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var req = new CreatePurchasePlanRequest(
            ProjectCode: "DA-EPC-01",
            Note: "Kế hoạch có dịch vụ",
            Lines:
            [
                new CreatePurchasePlanLineRequest(
                    MaterialCode: "DV-VC-01",
                    ProductName: null, // Should default from service item
                    Unit: null, // Should default from service item
                    Quantity: 2,
                    BudgetUnitPrice: 3500000m,
                    BudgetAmount: 7000000m,
                    LineType: null) // Should auto-assign to service
            ]);

        var detail = await _planService.CreateAsync(req, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Single(detail.Lines);
        var line = detail.Lines[0];
        Assert.Equal("DV-VC-01", line.MaterialCode);
        Assert.Equal("Chi phí cẩu tháp vận chuyển", line.ProductName);
        Assert.Equal("Chuyến", line.Unit);
        Assert.Equal("service", line.LineType);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(7000000m, line.BudgetAmount);
    }
}
