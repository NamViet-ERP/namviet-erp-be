using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Tests.Wms;

public class StocktakeWorkflowTests : IDisposable
{
    private readonly WarehouseDbContext _db;

    public StocktakeWorkflowTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"wms-stocktake-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new WarehouseDbContext(options);
        SeedBaseline();
    }

    [Fact]
    public void GenerateVoucherCode_uses_kk_prefix_with_four_digits()
    {
        Assert.Equal("KK-0001", Stocktake.GenerateVoucherCode(1));
        Assert.Equal("KK-0042", Stocktake.GenerateVoucherCode(42));
    }

    [Fact]
    public async Task Create_payload_without_voucher_code_persists_generated_code()
    {
        var dto = NewCreateDto();
        var count = await _db.Stocktakes.CountAsync();
        var voucherCode = string.IsNullOrWhiteSpace(dto.VoucherCode)
            ? Stocktake.GenerateVoucherCode(count + 1)
            : dto.VoucherCode!;

        var stocktake = new Stocktake
        {
            Id = $"st-{Guid.NewGuid():N}"[..10],
            VoucherCode = voucherCode,
            VoucherDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedById = Guid.NewGuid(),
            CreatedByName = "Test Keeper",
            Status = dto.Status,
            WarehouseId = dto.WarehouseId,
            WarehouseName = dto.WarehouseName,
            Condition = dto.Condition,
            IsFrozen = dto.IsFrozen,
            Note = dto.Note ?? "",
            Items =
            [
                new StocktakeItem
                {
                    MaterialCode = "MAT-001",
                    Sku = "MAT-001",
                    Name = "Vật tư test",
                    SystemQty = 0m,
                    ActualQty = 7m,
                    Diff = 7m,
                    Reason = "Thừa do đối tác bàn giao thêm",
                },
            ],
        };
        foreach (var item in stocktake.Items) item.StocktakeId = stocktake.Id;

        _db.Stocktakes.Add(stocktake);
        await _db.SaveChangesAsync();

        var saved = await _db.Stocktakes.SingleAsync();
        Assert.Equal("KK-0001", saved.VoucherCode);
        Assert.True(saved.IsFrozen);
        Assert.Equal("pending", saved.Status);
        Assert.Single(saved.Items);
        Assert.Equal(7m, saved.Items.First().ActualQty);
        Assert.Equal("Thừa do đối tác bàn giao thêm", saved.Items.First().Reason);
    }

    private static CreateStocktakeDto NewCreateDto() =>
        new(
            VoucherCode: null,
            Date: "08/07/2026",
            CreatedBy: null,
            Status: "pending",
            WarehouseId: "DA-TEST",
            WarehouseName: "Dự án test",
            Condition: "counting",
            IsFrozen: true,
            Note: "",
            Items:
            [
                new StocktakeItemDto("MAT-001", "MAT-001", "Vật tư test", 0m, 7m, 7m, "Thừa do đối tác bàn giao thêm"),
            ]);

    private void SeedBaseline()
    {
        _db.Projects.Add(new Project
        {
            ProjectCode = "DA-TEST",
            ProjectName = "Dự án test",
            PmName = "PM",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = ProjectStatus.Active,
        });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
