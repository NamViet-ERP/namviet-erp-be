using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Tests.Warehouse;

[Trait("Category", "Integration")]
public class MaterialUnitDbTests
{
    public sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NAMVIET_TEST_POSTGRES")))
                Skip = "Set NAMVIET_TEST_POSTGRES to an isolated migrated test database.";
        }
    }

    [PostgresFact]
    public async Task Update_material_unit_toggle_active_persists()
    {
        var connectionString = Environment.GetEnvironmentVariable("NAMVIET_TEST_POSTGRES")!;
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new WarehouseDbContext(options);
        var unit = new MaterialUnit
        {
            Name = "test-" + Guid.NewGuid().ToString("N"),
            SortOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.MaterialUnits.Add(unit);
        await db.SaveChangesAsync();
        try
        {
            unit.IsActive = false;
            unit.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await using var verify = new WarehouseDbContext(options);
            var saved = await verify.MaterialUnits.AsNoTracking().FirstAsync(u => u.Id == unit.Id);
            Assert.False(saved.IsActive);
        }
        finally
        {
            await db.MaterialUnits.Where(u => u.Id == unit.Id).ExecuteDeleteAsync();
        }
    }
}
