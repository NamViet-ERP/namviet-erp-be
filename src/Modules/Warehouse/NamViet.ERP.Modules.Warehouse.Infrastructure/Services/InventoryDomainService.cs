using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Services;

public class InventoryDomainService(WarehouseDbContext db) : IInventoryDomainService
{
    public async Task<string> GetGeneralWarehouseIdAsync(CancellationToken ct = default)
    {
        var setting = await db.WmsSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == WmsSettingsKeys.GeneralWarehouseId, ct);

        if (setting is not null && !string.IsNullOrWhiteSpace(setting.Value))
            return setting.Value.Trim().Trim('"');

        return WmsWarehouseIds.General;
    }

    public async Task<decimal> GetAvailableQtyAsync(
        string warehouseId, string materialCode, CancellationToken ct = default)
    {
        var actual = await db.WarehouseInventories
            .AsNoTracking()
            .Where(i => i.WarehouseId == warehouseId && i.MaterialCode == materialCode)
            .Select(i => i.Qty)
            .FirstOrDefaultAsync(ct);

        var holding = await db.InventoryReservations
            .AsNoTracking()
            .Where(r => r.WarehouseId == warehouseId
                        && r.MaterialCode == materialCode
                        && r.Status == ReservationStatus.Holding)
            .SumAsync(r => r.ReservedQty, ct);

        return actual - holding;
    }

    public async Task<decimal> GetProjectAvailableQtyAsync(
        string projectCode,
        string materialCode,
        string? excludeStockDefectTicketCode = null,
        CancellationToken ct = default)
    {
        var inventory = await db.ProjectInventories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                pi => pi.ProjectCode == projectCode && pi.MaterialCode == materialCode, ct);

        var pending = await GetPendingStockDefectQtyByMaterialAsync(
            projectCode, excludeStockDefectTicketCode, ct);
        pending.TryGetValue(materialCode, out var pendingHl);
        var actual = inventory?.ActualInventoryQty ?? 0m;
        var planned = inventory?.PlannedExportQty ?? 0m;
        return actual - planned - pendingHl;
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetPendingStockDefectQtyByMaterialAsync(
        string projectCode,
        string? excludeStockDefectTicketCode = null,
        CancellationToken ct = default)
    {
        var pending = await db.PurchaseDefects
            .AsNoTracking()
            .Include(d => d.Items)
            .Where(d =>
                d.ProjectCode == projectCode
                && d.SourceType == PurchaseDefectSourceTypes.Stock
                && d.Status == PurchaseDefectStatuses.Pending
                && (excludeStockDefectTicketCode == null || d.TicketCode != excludeStockDefectTicketCode))
            .ToListAsync(ct);

        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var hl in pending)
        {
            foreach (var line in hl.Items)
            {
                if (string.IsNullOrWhiteSpace(line.MaterialCode)) continue;
                map[line.MaterialCode] = map.GetValueOrDefault(line.MaterialCode) + line.Quantity;
            }
        }
        return map;
    }

    public async Task<IReadOnlyList<PendingStockDefectHold>> ListPendingStockDefectHoldsAsync(
        CancellationToken ct = default)
    {
        var pending = await db.PurchaseDefects
            .AsNoTracking()
            .Include(d => d.Items)
            .Where(d =>
                d.SourceType == PurchaseDefectSourceTypes.Stock
                && d.Status == PurchaseDefectStatuses.Pending)
            .ToListAsync(ct);

        var map = new Dictionary<(string Project, string Material), decimal>();
        foreach (var hl in pending)
        {
            foreach (var line in hl.Items)
            {
                if (string.IsNullOrWhiteSpace(line.MaterialCode)) continue;
                var key = (hl.ProjectCode, line.MaterialCode);
                map[key] = map.GetValueOrDefault(key) + line.Quantity;
            }
        }

        return map
            .Select(kv => new PendingStockDefectHold(kv.Key.Project, kv.Key.Material, kv.Value))
            .ToList();
    }

    public async Task<ReservationResult> CreateReservationAsync(
        string warehouseId,
        string projectCode,
        string materialCode,
        decimal requestedQty,
        string sourceTicketCode,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedQty);

        var available = await GetAvailableQtyAsync(warehouseId, materialCode, ct);
        var reservedFromStock = Math.Min(requestedQty, Math.Max(0, available));
        var shortfall = requestedQty - reservedFromStock;

        var projectInventory = await GetOrCreateProjectInventoryAsync(projectCode, materialCode, ct);
        projectInventory.AddPlannedImport(requestedQty);
        if (reservedFromStock > 0)
            projectInventory.AddReserved(reservedFromStock);

        db.InventoryReservations.Add(new InventoryReservation
        {
            WarehouseId = warehouseId,
            ProjectCode = projectCode,
            MaterialCode = materialCode,
            SourceTicketCode = sourceTicketCode,
            ReservedQty = reservedFromStock,
            ExternalQty = shortfall,
            Status = ReservationStatus.Holding
        });

        return new ReservationResult(reservedFromStock, shortfall);
    }

    public async Task FulfillReservationsAsync(string sourceTicketCode, CancellationToken ct = default)
    {
        var reservations = await db.InventoryReservations
            .Where(r => r.SourceTicketCode == sourceTicketCode && r.Status == ReservationStatus.Holding)
            .ToListAsync(ct);

        foreach (var reservation in reservations)
        {
            reservation.Status = ReservationStatus.Fulfilled;
            reservation.FulfilledQty = reservation.ReservedQty;
            reservation.FulfilledAt = DateTime.UtcNow;
            reservation.UpdatedAt = DateTime.UtcNow;

            if (reservation.ReservedQty > 0)
                await AdjustWarehouseQtyAsync(
                    reservation.WarehouseId, reservation.MaterialCode, -reservation.ReservedQty, ct);

            var projectInventory = await GetOrCreateProjectInventoryAsync(
                reservation.ProjectCode, reservation.MaterialCode, ct);

            var fulfilledTotal = reservation.ReservedQty + reservation.ExternalQty;
            projectInventory.ApplyActualImport(fulfilledTotal);
            projectInventory.RemovePlannedImport(fulfilledTotal);
            if (reservation.ReservedQty > 0)
                projectInventory.RemoveReserved(reservation.ReservedQty);
        }
    }

    public async Task CancelReservationsAsync(string sourceTicketCode, CancellationToken ct = default)
    {
        var reservations = await db.InventoryReservations
            .Where(r => r.SourceTicketCode == sourceTicketCode && r.Status == ReservationStatus.Holding)
            .ToListAsync(ct);

        foreach (var reservation in reservations)
        {
            reservation.Status = ReservationStatus.Cancelled;
            reservation.UpdatedAt = DateTime.UtcNow;

            var projectInventory = await GetOrCreateProjectInventoryAsync(
                reservation.ProjectCode, reservation.MaterialCode, ct);
            projectInventory.RemovePlannedImport(reservation.RequestedQty);
            if (reservation.ReservedQty > 0)
                projectInventory.RemoveReserved(reservation.ReservedQty);
        }
    }

    public async Task CreateProjectImportPlannedAsync(
        WarehouseTransaction transaction, CancellationToken ct = default)
    {
        var projectCode = transaction.BeneficiaryProjectCode;
        if (string.IsNullOrWhiteSpace(projectCode))
            return;

        foreach (var item in transaction.Items)
        {
            var projectInventory = await GetOrCreateProjectInventoryAsync(
                projectCode, item.MaterialCode, ct);
            projectInventory.AddPlannedImport(item.Quantity);
        }
    }

    public async Task CancelProjectImportPlannedAsync(
        WarehouseTransaction transaction, CancellationToken ct = default)
    {
        var projectCode = transaction.BeneficiaryProjectCode;
        if (string.IsNullOrWhiteSpace(projectCode))
            return;

        foreach (var item in transaction.Items)
        {
            var projectInventory = await db.ProjectInventories
                .FirstOrDefaultAsync(
                    pi => pi.ProjectCode == projectCode && pi.MaterialCode == item.MaterialCode, ct);
            projectInventory?.RemovePlannedImport(item.Quantity);
        }
    }

    public async Task CreateProjectExportPlannedAsync(
        WarehouseTransaction transaction, CancellationToken ct = default)
    {
        var projectCode = transaction.ProjectCode ?? transaction.BeneficiaryProjectCode;
        if (string.IsNullOrWhiteSpace(projectCode))
            throw new DomainRuleException("Phiếu xuất dự án yêu cầu mã kho dự án.");

        var pendingHl = await GetPendingStockDefectQtyByMaterialAsync(projectCode, null, ct);
        foreach (var item in transaction.Items)
        {
            var projectInventory = await GetOrCreateProjectInventoryAsync(
                projectCode, item.MaterialCode, ct);
            pendingHl.TryGetValue(item.MaterialCode, out var hold);
            var available = projectInventory.ActualInventoryQty - projectInventory.PlannedExportQty - hold;
            if (item.Quantity > available)
                throw new DomainRuleException(
                    $"Vật tư {item.MaterialCode} vượt tồn khả dụng ({available}).");

            projectInventory.AddPlannedExport(item.Quantity);
        }
    }

    public async Task CancelProjectExportPlannedAsync(
        WarehouseTransaction transaction, CancellationToken ct = default)
    {
        var projectCode = transaction.ProjectCode ?? transaction.BeneficiaryProjectCode;
        if (string.IsNullOrWhiteSpace(projectCode))
            return;

        foreach (var item in transaction.Items)
        {
            var projectInventory = await db.ProjectInventories
                .FirstOrDefaultAsync(
                    pi => pi.ProjectCode == projectCode && pi.MaterialCode == item.MaterialCode, ct);
            projectInventory?.RemovePlannedExport(item.Quantity);
        }
    }

    public async Task ApplyApprovedImportAsync(
        WarehouseTransaction transaction, CancellationToken ct = default)
    {
        if (transaction.IsDirectProcurement)
        {
            if (!string.IsNullOrWhiteSpace(transaction.WarehouseId))
                await DeductSourceWarehouseAsync(transaction, ct);
            await ApplyDirectIssueAsync(transaction, ct);
            return;
        }

        var hasReservations = await db.InventoryReservations
            .AnyAsync(r => r.SourceTicketCode == transaction.TicketCode, ct);

        if (hasReservations)
        {
            await FulfillReservationsAsync(transaction.TicketCode, ct);
            return;
        }

        var projectCode = transaction.BeneficiaryProjectCode ?? transaction.ProjectCode;
        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            foreach (var item in transaction.Items)
            {
                var projectInventory = await GetOrCreateProjectInventoryAsync(
                    projectCode, item.MaterialCode, ct);
                projectInventory.ApplyActualImport(item.Quantity);
                var removePlanned = Math.Min(item.Quantity, projectInventory.PlannedImportQty);
                if (removePlanned > 0)
                    projectInventory.RemovePlannedImport(removePlanned);
            }
            return;
        }

        throw new DomainRuleException(
            "Phiếu nhập phải gắn kho dự án. Nhập kho không cộng tồn kho trung tâm.");
    }

    public async Task ApplyApprovedExportAsync(
        WarehouseTransaction transaction, CancellationToken ct = default)
    {
        var projectCode = transaction.ProjectCode ?? transaction.BeneficiaryProjectCode;
        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            var pendingHl = await GetPendingStockDefectQtyByMaterialAsync(projectCode, null, ct);
            foreach (var item in transaction.Items)
            {
                var projectInventory = await GetOrCreateProjectInventoryAsync(
                    projectCode, item.MaterialCode, ct);
                pendingHl.TryGetValue(item.MaterialCode, out var hold);
                var available = projectInventory.ActualInventoryQty - hold;
                if (item.Quantity > available)
                    throw new DomainRuleException(
                        $"Vật tư {item.MaterialCode} vượt tồn khả dụng ({available}).");

                var removePlanned = Math.Min(item.Quantity, projectInventory.PlannedExportQty);
                if (removePlanned > 0)
                    projectInventory.RemovePlannedExport(removePlanned);

                projectInventory.ApplyActualExport(item.Quantity);
            }
            return;
        }

        if (string.IsNullOrEmpty(transaction.WarehouseId))
            return;

        foreach (var item in transaction.Items)
            await AdjustWarehouseQtyAsync(
                transaction.WarehouseId, item.MaterialCode, -item.Quantity, ct);
    }

    public async Task ApplyDirectIssueAsync(
        WarehouseTransaction importTransaction, CancellationToken ct = default)
    {
        var projectCode = importTransaction.BeneficiaryProjectCode
            ?? importTransaction.ProjectCode
            ?? throw new DomainRuleException("Direct Issue yêu cầu mã dự án thụ hưởng.");

        var exportTransaction = await FindLinkedDirectExportAsync(importTransaction, ct);
        if (exportTransaction is null)
        {
            var exportTicketCode = await TicketCodeAllocator.NextAsync(
                db, TransactionType.Export, ct);

            exportTransaction = BuildDirectExport(
                importTransaction,
                projectCode,
                exportTicketCode,
                ApprovalStatus.Approved);
            db.WarehouseTransactions.Add(exportTransaction);
        }
        else
        {
            SyncDirectExportFromImport(exportTransaction, importTransaction, projectCode);
            exportTransaction.ApprovalStatus = ApprovalStatus.Approved;
            exportTransaction.WarehouseApprovalStatus = importTransaction.WarehouseApprovalStatus;
            exportTransaction.ApprovedById = importTransaction.ApprovedById;
            exportTransaction.ApprovedByName = importTransaction.ApprovedByName;
            exportTransaction.ApprovedAt = importTransaction.ApprovedAt ?? DateTime.UtcNow;
            exportTransaction.UpdatedAt = DateTime.UtcNow;
        }

        importTransaction.LinkedTicketCode = exportTransaction.TicketCode;
        exportTransaction.LinkedTicketCode = importTransaction.TicketCode;

        foreach (var item in importTransaction.Items)
        {
            var projectInventory = await GetOrCreateProjectInventoryAsync(
                projectCode, item.MaterialCode, ct);
            var removePlanned = Math.Min(item.Quantity, projectInventory.PlannedImportQty);
            if (removePlanned > 0)
                projectInventory.RemovePlannedImport(removePlanned);
            projectInventory.ApplyActualImport(item.Quantity);
            projectInventory.ApplyActualExport(item.Quantity);
        }
    }

    public async Task EnsurePendingDirectExportAsync(
        WarehouseTransaction importTransaction, CancellationToken ct = default)
    {
        if (!importTransaction.IsDirectProcurement)
            return;
        if (importTransaction.ApprovalStatus != ApprovalStatus.Pending)
            return;

        var projectCode = importTransaction.BeneficiaryProjectCode
            ?? importTransaction.ProjectCode
            ?? throw new DomainRuleException("Xuất thẳng yêu cầu mã dự án.");

        var existing = await FindLinkedDirectExportAsync(importTransaction, ct);
        if (existing is not null)
        {
            SyncDirectExportFromImport(existing, importTransaction, projectCode);
            importTransaction.LinkedTicketCode = existing.TicketCode;
            existing.LinkedTicketCode = importTransaction.TicketCode;
            return;
        }

        var exportTicketCode = await TicketCodeAllocator.NextAsync(
            db, TransactionType.Export, ct);
        var exportTransaction = BuildDirectExport(
            importTransaction,
            projectCode,
            exportTicketCode,
            ApprovalStatus.Pending);
        db.WarehouseTransactions.Add(exportTransaction);
        importTransaction.LinkedTicketCode = exportTicketCode;
    }

    public async Task HealPendingDirectExportsAsync(CancellationToken ct = default)
    {
        var pendingDirects = await db.WarehouseTransactions
            .Include(t => t.Items)
            .Where(t => t.TransactionType == TransactionType.Import
                        && t.IsDirectProcurement
                        && t.ApprovalStatus == ApprovalStatus.Pending)
            .ToListAsync(ct);

        foreach (var import in pendingDirects)
            await EnsurePendingDirectExportAsync(import, ct);

        if (pendingDirects.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private async Task<WarehouseTransaction?> FindLinkedDirectExportAsync(
        WarehouseTransaction importTransaction, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(importTransaction.LinkedTicketCode))
        {
            var byCode = await db.WarehouseTransactions
                .Include(t => t.Items)
                .FirstOrDefaultAsync(
                    t => t.TransactionType == TransactionType.Export
                         && t.TicketCode == importTransaction.LinkedTicketCode,
                    ct);
            if (byCode is not null)
                return byCode;
        }

        return await db.WarehouseTransactions
            .Include(t => t.Items)
            .FirstOrDefaultAsync(
                t => t.TransactionType == TransactionType.Export
                     && t.LinkedTicketCode == importTransaction.TicketCode,
                ct);
    }

    private static WarehouseTransaction BuildDirectExport(
        WarehouseTransaction importTransaction,
        string projectCode,
        string exportTicketCode,
        ApprovalStatus status)
    {
        var approved = status == ApprovalStatus.Approved;
        return new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = exportTicketCode,
            TransactionType = TransactionType.Export,
            ProjectCode = projectCode,
            BeneficiaryProjectCode = projectCode,
            WarehouseId = importTransaction.WarehouseId,
            SoCode = importTransaction.ReferenceCode,
            LinkedTicketCode = importTransaction.TicketCode,
            IsDirectProcurement = true,
            ApprovalStatus = status,
            WarehouseApprovalStatus = approved
                ? importTransaction.WarehouseApprovalStatus
                : WarehouseKeeperApprovalStatus.Pending,
            ApprovedById = approved ? importTransaction.ApprovedById : null,
            ApprovedByName = approved ? importTransaction.ApprovedByName : null,
            ApprovedAt = approved ? (importTransaction.ApprovedAt ?? DateTime.UtcNow) : null,
            CreatedById = importTransaction.CreatedById,
            CreatedByName = importTransaction.CreatedByName,
            Notes = string.IsNullOrWhiteSpace(importTransaction.Notes)
                ? $"Xuất thẳng công trường từ {importTransaction.TicketCode}"
                : importTransaction.Notes,
            Items = CloneDirectExportItems(importTransaction, exportTicketCode),
        };
    }

    private void SyncDirectExportFromImport(
        WarehouseTransaction exportTransaction,
        WarehouseTransaction importTransaction,
        string projectCode)
    {
        exportTransaction.ProjectCode = projectCode;
        exportTransaction.BeneficiaryProjectCode = projectCode;
        exportTransaction.WarehouseId = importTransaction.WarehouseId;
        exportTransaction.SoCode = importTransaction.ReferenceCode;
        exportTransaction.IsDirectProcurement = true;
        if (exportTransaction.Items.Count > 0)
            db.WarehouseTransactionItems.RemoveRange(exportTransaction.Items);
        exportTransaction.Items = CloneDirectExportItems(importTransaction, exportTransaction.TicketCode);
    }

    private static List<WarehouseTransactionItem> CloneDirectExportItems(
        WarehouseTransaction importTransaction,
        string exportTicketCode) =>
        importTransaction.Items.Select(i =>
        {
            var clone = new WarehouseTransactionItem
            {
                TicketCode = exportTicketCode,
                MaterialCode = i.MaterialCode,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                ConstructionPhase = i.ConstructionPhase,
                Notes = i.Notes
            };
            TransactionItemHelper.CopySnapshots(i, clone);
            return clone;
        }).ToList();

    public async Task ApplyTransferAsync(
        WarehouseTransaction transaction, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(transaction.FromWarehouseId) || string.IsNullOrEmpty(transaction.ToWarehouseId))
            return;

        foreach (var item in transaction.Items)
        {
            var available = await GetAvailableQtyAsync(
                transaction.FromWarehouseId, item.MaterialCode, ct);
            if (item.Quantity > available)
                throw new DomainRuleException(
                    $"Vật tư {item.MaterialCode} vượt tồn khả dụng tại kho nguồn ({available}).");

            await AdjustWarehouseQtyAsync(
                transaction.FromWarehouseId, item.MaterialCode, -item.Quantity, ct);
            await AdjustWarehouseQtyAsync(
                transaction.ToWarehouseId, item.MaterialCode, item.Quantity, ct);
        }
    }

    public async Task ApplyReturnToGeneralAsync(
        WarehouseTransaction transaction, CancellationToken ct = default)
    {
        var projectCode = transaction.ProjectCode ?? transaction.BeneficiaryProjectCode;
        if (string.IsNullOrWhiteSpace(projectCode))
            throw new DomainRuleException("Phiếu hoàn trả yêu cầu mã kho dự án.");

        var generalWarehouseId = await GetGeneralWarehouseIdAsync(ct);
        transaction.ToWarehouseId = generalWarehouseId;

        var pendingHl = await GetPendingStockDefectQtyByMaterialAsync(projectCode, null, ct);
        foreach (var item in transaction.Items)
        {
            var projectInventory = await GetOrCreateProjectInventoryAsync(
                projectCode, item.MaterialCode, ct);
            pendingHl.TryGetValue(item.MaterialCode, out var hold);
            var available = projectInventory.ActualInventoryQty - hold;
            if (item.Quantity > available)
                throw new DomainRuleException(
                    $"Vật tư {item.MaterialCode} vượt tồn khả dụng ({available}).");

            var removePlanned = Math.Min(item.Quantity, projectInventory.PlannedExportQty);
            if (removePlanned > 0)
                projectInventory.RemovePlannedExport(removePlanned);

            projectInventory.ApplyActualExport(item.Quantity);
            await AdjustWarehouseQtyAsync(generalWarehouseId, item.MaterialCode, item.Quantity, ct);
        }
    }

    public async Task ApplyCrossProjectTransferAsync(
        WarehouseTransaction transaction, CancellationToken ct = default)
    {
        var fromCode = transaction.ProjectCode;
        var toCode = transaction.BeneficiaryProjectCode;
        if (string.IsNullOrWhiteSpace(fromCode) || string.IsNullOrWhiteSpace(toCode))
            throw new DomainRuleException("Phiếu chuyển dự án yêu cầu kho nguồn và kho đích.");
        if (string.Equals(fromCode, toCode, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleException("Kho nguồn và kho đích phải khác nhau.");

        var pendingHl = await GetPendingStockDefectQtyByMaterialAsync(fromCode, null, ct);
        foreach (var item in transaction.Items)
        {
            var sourceInventory = await GetOrCreateProjectInventoryAsync(fromCode, item.MaterialCode, ct);
            pendingHl.TryGetValue(item.MaterialCode, out var hold);
            var available = sourceInventory.ActualInventoryQty - hold;
            if (item.Quantity > available)
                throw new DomainRuleException(
                    $"Vật tư {item.MaterialCode} vượt tồn khả dụng tại kho nguồn ({available}).");

            var removePlanned = Math.Min(item.Quantity, sourceInventory.PlannedExportQty);
            if (removePlanned > 0)
                sourceInventory.RemovePlannedExport(removePlanned);

            sourceInventory.ApplyActualExport(item.Quantity);

            var targetInventory = await GetOrCreateProjectInventoryAsync(toCode, item.MaterialCode, ct);
            targetInventory.ApplyActualImport(item.Quantity);
        }
    }

    public async Task ApplyStocktakeAdjustmentAsync(
        string projectCode, string materialCode, decimal diffQty, CancellationToken ct = default)
    {
        if (diffQty == 0) return;

        var projectInventory = await GetOrCreateProjectInventoryAsync(projectCode, materialCode, ct);
        if (diffQty > 0)
        {
            projectInventory.ApplyActualImport(diffQty);
            return;
        }

        var exportQty = -diffQty;
        var pending = await GetPendingStockDefectQtyByMaterialAsync(projectCode, null, ct);
        pending.TryGetValue(materialCode, out var hold);
        var available = projectInventory.ActualInventoryQty - projectInventory.PlannedExportQty - hold;
        if (exportQty > available)
            throw new DomainRuleException(
                $"Vật tư {materialCode} không đủ tồn khả dụng để điều chỉnh kiểm kê ({available}).");

        projectInventory.ApplyActualExport(exportQty);
    }

    public async Task CreateProjectExportReturnPlannedAsync(
        WarehouseTransaction transaction, CancellationToken ct = default)
    {
        var projectCode = transaction.ProjectCode ?? transaction.BeneficiaryProjectCode;
        if (string.IsNullOrWhiteSpace(projectCode))
            throw new DomainRuleException("Phiếu hoàn trả yêu cầu mã kho dự án.");

        foreach (var item in transaction.Items)
        {
            var projectInventory = await GetOrCreateProjectInventoryAsync(
                projectCode, item.MaterialCode, ct);
            projectInventory.AddPlannedImport(item.Quantity);
        }
    }

    public async Task CancelProjectExportReturnPlannedAsync(
        WarehouseTransaction transaction, CancellationToken ct = default)
    {
        var projectCode = transaction.ProjectCode ?? transaction.BeneficiaryProjectCode;
        if (string.IsNullOrWhiteSpace(projectCode))
            return;

        foreach (var item in transaction.Items)
        {
            var projectInventory = await db.ProjectInventories
                .FirstOrDefaultAsync(
                    pi => pi.ProjectCode == projectCode && pi.MaterialCode == item.MaterialCode, ct);
            projectInventory?.RemovePlannedImport(item.Quantity);
        }
    }

    public async Task ApplyApprovedExportReturnAsync(
        WarehouseTransaction transaction, CancellationToken ct = default)
    {
        var projectCode = transaction.ProjectCode ?? transaction.BeneficiaryProjectCode;
        if (string.IsNullOrWhiteSpace(projectCode))
            throw new DomainRuleException("Phiếu hoàn trả yêu cầu mã kho dự án.");

        foreach (var item in transaction.Items)
        {
            var projectInventory = await GetOrCreateProjectInventoryAsync(
                projectCode, item.MaterialCode, ct);

            var removePlanned = Math.Min(item.Quantity, projectInventory.PlannedImportQty);
            if (removePlanned > 0)
                projectInventory.RemovePlannedImport(removePlanned);

            projectInventory.ApplyExportReturn(item.Quantity);
        }
    }

    public async Task ApplyProjectCloseTransferAsync(string projectCode, CancellationToken ct = default)
    {
        var generalWarehouseId = await GetGeneralWarehouseIdAsync(ct);
        var inventories = await db.ProjectInventories
            .Where(pi => pi.ProjectCode == projectCode)
            .ToListAsync(ct);

        foreach (var row in inventories)
        {
            var remaining = row.ActualInventoryQty;
            if (remaining <= 0)
                continue;

            await AdjustWarehouseQtyAsync(generalWarehouseId, row.MaterialCode, remaining, ct);
            row.ApplyActualExport(remaining);
        }

        await CancelReservationsForProjectAsync(projectCode, ct);
    }

    private async Task CancelReservationsForProjectAsync(string projectCode, CancellationToken ct)
    {
        var ticketCodes = await db.InventoryReservations
            .Where(r => r.ProjectCode == projectCode && r.Status == ReservationStatus.Holding)
            .Select(r => r.SourceTicketCode)
            .Distinct()
            .ToListAsync(ct);

        foreach (var ticketCode in ticketCodes)
            await CancelReservationsAsync(ticketCode, ct);
    }

    private async Task<ProjectInventory> GetOrCreateProjectInventoryAsync(
        string projectCode, string materialCode, CancellationToken ct)
    {
        var inventory = await db.ProjectInventories
            .FirstOrDefaultAsync(pi => pi.ProjectCode == projectCode && pi.MaterialCode == materialCode, ct);

        if (inventory is not null)
            return inventory;

        inventory = new ProjectInventory
        {
            ProjectCode = projectCode,
            MaterialCode = materialCode
        };
        db.ProjectInventories.Add(inventory);
        return inventory;
    }

    private async Task DeductSourceWarehouseAsync(WarehouseTransaction transaction, CancellationToken ct)
    {
        var warehouseId = transaction.WarehouseId ?? "";
        foreach (var item in transaction.Items)
        {
            if (item.Quantity <= 0)
                continue;

            var available = await GetAvailableQtyAsync(warehouseId, item.MaterialCode, ct);
            if (item.Quantity > available)
                throw new DomainRuleException(
                    $"Kho trung tâm không đủ '{item.MaterialCode}' (khả dụng {available:0.##}, cần {item.Quantity:0.##}).");

            await AdjustWarehouseQtyAsync(warehouseId, item.MaterialCode, -item.Quantity, ct);
        }
    }

    private async Task AdjustWarehouseQtyAsync(
        string warehouseId, string materialCode, decimal delta, CancellationToken ct)
    {
        var inventory = await db.WarehouseInventories
            .FirstOrDefaultAsync(i => i.WarehouseId == warehouseId && i.MaterialCode == materialCode, ct);

        if (inventory is null)
        {
            inventory = new WarehouseInventory
            {
                WarehouseId = warehouseId,
                MaterialCode = materialCode,
                Qty = 0
            };
            db.WarehouseInventories.Add(inventory);
        }

        inventory.Qty += delta;
        inventory.UpdatedAt = DateTime.UtcNow;
    }
}
