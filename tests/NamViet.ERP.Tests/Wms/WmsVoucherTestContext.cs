using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Services;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Tests.Wms;

internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid Id { get; init; } = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    public string Email => "keeper@namviet.vn";
    public string Name => "Test Keeper";
    public string EmployeeCode => "NV-TEST";
    public int? EmployeeId => null;
    public IReadOnlyList<string> Roles => ["Warehouse_Keeper"];
    public IReadOnlyList<string> Permissions => [];
    public bool IsAuthenticated => true;
    public bool IsInRole(string role) => Roles.Contains(role);
    public bool HasPermission(string permission) => Permissions.Contains(permission);
}

internal sealed class WmsVoucherTestContext : IDisposable
{
    public WarehouseDbContext Db { get; }
    public InventoryDomainService Inventory { get; }
    public FakeCurrentUser CurrentUser { get; } = new();
    public ImportApplicationService Imports { get; }
    public ExportApplicationService Exports { get; }
    public TransferApplicationService Transfers { get; }

    public WmsVoucherTestContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"wms-voucher-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        Db = new WarehouseDbContext(options);
        Inventory = new InventoryDomainService(Db);
        Imports = new ImportApplicationService(Db, Inventory, CurrentUser);
        Exports = new ExportApplicationService(Db, Inventory, CurrentUser);
        Transfers = new TransferApplicationService(Db, Inventory, CurrentUser);
        SeedBaseline();
    }

    public void SeedProjectStock(string materialCode, decimal actualQty)
    {
        var existing = Db.ProjectInventories
            .FirstOrDefault(p => p.ProjectCode == "DA-TEST" && p.MaterialCode == materialCode);
        if (existing is null)
        {
            Db.ProjectInventories.Add(new ProjectInventory
            {
                ProjectCode = "DA-TEST",
                MaterialCode = materialCode,
                ActualImportQty = actualQty,
            });
        }
        else
        {
            existing.ActualImportQty = actualQty;
        }
        Db.SaveChanges();
    }

    private void SeedBaseline()
    {
        Db.WmsWarehouses.Add(new WmsWarehouse
        {
            Id = WmsWarehouseIds.General,
            Code = "KHO-TONG",
            Name = "Kho tổng",
            Group = "Kho tổng",
            Address = "A",
            Branch = "HCM",
            Manager = "Keeper",
            Status = "active",
        });
        Db.WmsSettings.Add(new WmsSetting
        {
            Key = WmsSettingsKeys.GeneralWarehouseId,
            Value = WmsWarehouseIds.General,
        });
        Db.Projects.Add(new Project
        {
            ProjectCode = "DA-TEST",
            ProjectName = "Dự án test",
            PmName = "PM",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = ProjectStatus.Active,
        });
        Db.Materials.Add(new Material
        {
            MaterialCode = "MAT-001",
            MaterialName = "Vật tư test",
            CategoryId = 1,
            Unit = "Cái",
        });
        Db.WarehouseInventories.Add(new WarehouseInventory
        {
            WarehouseId = WmsWarehouseIds.General,
            MaterialCode = "MAT-001",
            Qty = 80m,
        });
        Db.SaveChanges();
    }

    public static ImportItemDto ImportLine(decimal qty) =>
        new("MAT-001", "MAT-001", "Vật tư test", qty, "Cái", 1000m);

    public static ExportItemDto ExportLine(decimal qty) =>
        new("MAT-001", "MAT-001", "Vật tư test", qty, "Cái");

    public static TransferItemDto TransferLine(decimal qty) =>
        new("MAT-001", "MAT-001", "Vật tư test", qty, "Cái");

    public void Dispose() => Db.Dispose();
}
