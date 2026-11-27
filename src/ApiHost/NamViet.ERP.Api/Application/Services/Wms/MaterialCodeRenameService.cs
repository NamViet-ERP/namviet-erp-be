using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Wms;

public sealed record MaterialUpdateFields(
    string MaterialName,
    int CategoryId,
    string Unit,
    string? Origin,
    string? ImageUrl,
    string? TechnicalSpec,
    string? GeneralTechnicalSpec,
    string? QualityStandard,
    decimal? CeilingPrice,
    string? AbcClass,
    bool IsConsumable,
    DateTime UpdatedAt);

public interface IMaterialCodeRenameService
{
    Task RenameAndUpdateAsync(Material existing, string newCode, MaterialUpdateFields fields, CancellationToken ct = default);
}

public sealed class MaterialCodeRenameService(WarehouseDbContext db) : IMaterialCodeRenameService
{
    public async Task RenameAndUpdateAsync(
        Material existing,
        string newCode,
        MaterialUpdateFields fields,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existing);
        var oldCode = existing.MaterialCode;

        if (db.Database.IsRelational())
        {
            await RenameRelationalAsync(existing, oldCode, newCode, fields, ct);
            return;
        }

        await RenameInMemoryAsync(existing, oldCode, newCode, fields, ct);
    }

    private async Task RenameRelationalAsync(
        Material existing,
        string oldCode,
        string newCode,
        MaterialUpdateFields fields,
        CancellationToken ct)
    {
        db.Entry(existing).State = EntityState.Detached;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE warehouse.materials
                SET "MaterialCode" = {newCode},
                    "MaterialName" = {fields.MaterialName},
                    "CategoryId" = {fields.CategoryId},
                    "Unit" = {fields.Unit},
                    "Origin" = {fields.Origin},
                    "ImageUrl" = {fields.ImageUrl},
                    "TechnicalSpec" = {fields.TechnicalSpec},
                    "GeneralTechnicalSpec" = {fields.GeneralTechnicalSpec},
                    "QualityStandard" = {fields.QualityStandard},
                    "CeilingPrice" = {fields.CeilingPrice},
                    "AbcClass" = {fields.AbcClass},
                    "IsConsumable" = {fields.IsConsumable},
                    "UpdatedAt" = {fields.UpdatedAt}
                WHERE "MaterialCode" = {oldCode} AND "DeletedAt" IS NULL
                """, ct);

            if (affected == 0)
            {
                throw new KeyNotFoundException($"Không tìm thấy vật tư '{oldCode}'.");
            }

            // 9 non-FK logical reference tables + activity_events (11 FK tables cascade via PostgreSQL ON UPDATE CASCADE).
            await db.ConsumableQuotas.IgnoreQueryFilters()
                .Where(x => x.MaterialCode == oldCode)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MaterialCode, newCode), ct);

            await db.ProjectBoms.IgnoreQueryFilters()
                .Where(x => x.MaterialCode == oldCode)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MaterialCode, newCode), ct);

            await db.PurchasePlanLines.IgnoreQueryFilters()
                .Where(x => x.MaterialCode == oldCode && (x.LineType == null || x.LineType == PurchasePlanLineTypes.Material))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MaterialCode, newCode), ct);

            await db.PurchaseWarranties.IgnoreQueryFilters()
                .Where(x => x.MaterialCode == oldCode)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MaterialCode, newCode), ct);

            await db.PurchaseEstimateLines.IgnoreQueryFilters()
                .Where(x => x.MaterialCode == oldCode)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MaterialCode, newCode), ct);

            await db.PurchaseRfqQuotes.IgnoreQueryFilters()
                .Where(x => x.MaterialCode == oldCode)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MaterialCode, newCode), ct);

            await db.PurchaseSellPrices.IgnoreQueryFilters()
                .Where(x => x.MaterialCode == oldCode)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MaterialCode, newCode), ct);

            await db.PurchaseItemEstimatePrices.IgnoreQueryFilters()
                .Where(x => x.MaterialCode == oldCode)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MaterialCode, newCode), ct);

            await db.PurchaseDefectItems.IgnoreQueryFilters()
                .Where(x => x.MaterialCode == oldCode)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MaterialCode, newCode), ct);

            await db.ActivityEvents.IgnoreQueryFilters()
                .Where(x => x.EntityType == "material" && x.EntityCode == oldCode)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.EntityCode, newCode), ct);

            await tx.CommitAsync(ct);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            await tx.RollbackAsync(ct);
            throw new DomainRuleException($"Mã vật tư '{newCode}' đã tồn tại trong hệ thống.");
        }
    }

    private async Task RenameInMemoryAsync(
        Material existing,
        string oldCode,
        string newCode,
        MaterialUpdateFields fields,
        CancellationToken ct)
    {
        var createdAt = existing.CreatedAt;
        var isActive = existing.IsActive;
        var deletedAt = existing.DeletedAt;

        // Clear ChangeTracker before removing the old principal so InMemory DeleteBehavior.Cascade/Restrict
        // does not cascade-delete or reject tracked dependents before their FK codes are updated.
        db.ChangeTracker.Clear();

        var toRemove = await db.Materials.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.MaterialCode == oldCode && x.DeletedAt == null, ct);
        if (toRemove is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy vật tư '{oldCode}'.");
        }

        db.Materials.Remove(toRemove);
        await db.SaveChangesAsync(ct);

        var replacement = new Material
        {
            MaterialCode = newCode,
            MaterialName = fields.MaterialName,
            CategoryId = fields.CategoryId,
            Unit = fields.Unit,
            Origin = fields.Origin,
            ImageUrl = fields.ImageUrl,
            TechnicalSpec = fields.TechnicalSpec,
            GeneralTechnicalSpec = fields.GeneralTechnicalSpec,
            QualityStandard = fields.QualityStandard,
            CeilingPrice = fields.CeilingPrice,
            AbcClass = fields.AbcClass,
            IsConsumable = fields.IsConsumable,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = fields.UpdatedAt,
            DeletedAt = deletedAt,
        };

        db.Materials.Add(replacement);
        await db.SaveChangesAsync(ct);

        // 9 FK-backed entities
        foreach (var item in await db.ProjectInventories.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var item in await db.WarehouseInventories.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var item in await db.WarehouseTransactionItems.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var item in await db.InventoryReservations.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var item in await db.ProjectMaterialLoans.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var item in await db.StocktakeItems.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var item in await db.PurchaseOrderItems.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var item in await db.ConversionItems.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var item in await db.ProductBarcodes.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;

        // 9 non-FK logical reference entities + ActivityEvents (plus shadow MaterialCode1 on ConsumableQuotas / ProjectBoms if populated)
        foreach (var item in await db.ConsumableQuotas.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
        {
            item.MaterialCode = newCode;
            SyncShadowMaterialCode(item, oldCode, newCode);
        }
        foreach (var item in await db.ProjectBoms.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
        {
            item.MaterialCode = newCode;
            SyncShadowMaterialCode(item, oldCode, newCode);
        }
        foreach (var item in await db.PurchasePlanLines.IgnoreQueryFilters()
                     .Where(x => x.MaterialCode == oldCode && (x.LineType == null || x.LineType == PurchasePlanLineTypes.Material))
                     .ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var item in await db.PurchaseWarranties.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var item in await db.PurchaseEstimateLines.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var item in await db.PurchaseRfqQuotes.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var item in await db.PurchaseSellPrices.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var item in await db.PurchaseItemEstimatePrices.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var item in await db.PurchaseDefectItems.IgnoreQueryFilters().Where(x => x.MaterialCode == oldCode).ToListAsync(ct))
            item.MaterialCode = newCode;
        foreach (var evt in await db.ActivityEvents.IgnoreQueryFilters()
                     .Where(x => x.EntityType == "material" && x.EntityCode == oldCode)
                     .ToListAsync(ct))
            evt.EntityCode = newCode;

        await db.SaveChangesAsync(ct);
    }

    private void SyncShadowMaterialCode(object entity, string oldCode, string newCode)
    {
        var entry = db.Entry(entity);
        var shadowProp = entry.Metadata.FindProperty("MaterialCode1");
        if (shadowProp is not null
            && entry.Property("MaterialCode1").CurrentValue is string currentShadow
            && currentShadow == oldCode)
        {
            entry.Property("MaterialCode1").CurrentValue = newCode;
        }
    }
}
