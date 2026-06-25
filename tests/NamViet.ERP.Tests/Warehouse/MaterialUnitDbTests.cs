using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Tests.Warehouse;

[Trait("Category", "Integration")]
public class MaterialUnitDbTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5434;Database=namviet_erp;Username=erp_admin;Password=change_me_strong";

    private static async Task<bool> CanConnectToPostgresAsync()
    {
        try
        {
            var options = new DbContextOptionsBuilder<WarehouseDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;
            await using var db = new WarehouseDbContext(options);
            return await db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task Update_material_unit_toggle_active_persists()
    {
        if (!await CanConnectToPostgresAsync())
            return;

        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new WarehouseDbContext(options);
        var unit = await db.MaterialUnits.FirstOrDefaultAsync(u => u.Name == "Bộ");
        if (unit is null)
        {
            db.MaterialUnits.Add(new MaterialUnit
            {
                Name = "Bộ",
                SortOrder = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            unit = await db.MaterialUnits.FirstAsync(u => u.Name == "Bộ");
        }

        var previous = unit.IsActive;
        unit.IsActive = !previous;
        unit.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await using var verify = new WarehouseDbContext(options);
        var saved = await verify.MaterialUnits.AsNoTracking().FirstAsync(u => u.Id == unit.Id);
        Assert.Equal(!previous, saved.IsActive);

        saved.IsActive = previous;
        await verify.MaterialUnits.Where(u => u.Id == unit.Id).ExecuteUpdateAsync(s =>
            s.SetProperty(u => u.IsActive, previous).SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
    }
}
