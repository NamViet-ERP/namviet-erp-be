using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

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

    public async Task<ReservationResult> CreateReservationAsync(
        string warehouseId,
        string projectCode,
        string materialCode,
        decimal requestedQty,
        string sourceTicketCode,
        CancellationToken ct = default)
    {
        if (requestedQty <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedQty));

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
            throw new InvalidOperationException("Phiếu xuất dự án yêu cầu mã kho dự án.");

        foreach (var item in transaction.Items)
        {
            var projectInventory = await GetOrCreateProjectInventoryAsync(
                projectCode, item.MaterialCode, ct);
            var available = projectInventory.ActualInventoryQty - projectInventory.PlannedExportQty;
            if (item.Quantity > available)
                throw new InvalidOperationException(
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
                projectInventory.RemovePlannedImport(item.Quantity);
            }
            return;
        }

        if (string.IsNullOrEmpty(transaction.WarehouseId))
            return;

        foreach (var item in transaction.Items)
            await AdjustWarehouseQtyAsync(
                transaction.WarehouseId, item.MaterialCode, item.Quantity, ct);
    }

    public async Task ApplyApprovedExportAsync(
        WarehouseTransaction transaction, CancellationToken ct = default)
    {
        var projectCode = transaction.ProjectCode ?? transaction.BeneficiaryProjectCode;
        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            foreach (var item in transaction.Items)
            {
                var projectInventory = await GetOrCreateProjectInventoryAsync(
                    projectCode, item.MaterialCode, ct);
                if (item.Quantity > projectInventory.ActualInventoryQty)
                    throw new InvalidOperationException(
                        $"Vật tư {item.MaterialCode} không đủ tồn thực tế ({projectInventory.ActualInventoryQty}).");

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
            ?? throw new InvalidOperationException("Direct Issue yêu cầu mã dự án thụ hưởng.");

        var exportCount = await db.WarehouseTransactions
            .CountAsync(t => t.TransactionType == TransactionType.Export, ct);
        var exportTicketCode = WarehouseTransaction.GenerateTicketCode(
            TransactionType.Export, exportCount + 1);

        var exportTransaction = new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = exportTicketCode,
            TransactionType = TransactionType.Export,
            ProjectCode = projectCode,
            BeneficiaryProjectCode = projectCode,
            WarehouseId = importTransaction.WarehouseId,
            LinkedTicketCode = importTransaction.TicketCode,
            IsDirectProcurement = true,
            ApprovalStatus = ApprovalStatus.Approved,
            WarehouseApprovalStatus = importTransaction.WarehouseApprovalStatus,
            ApprovedById = importTransaction.ApprovedById,
            ApprovedByName = importTransaction.ApprovedByName,
            ApprovedAt = importTransaction.ApprovedAt ?? DateTime.UtcNow,
            CreatedById = importTransaction.CreatedById,
            CreatedByName = importTransaction.CreatedByName,
            Notes = $"Auto Direct Issue from {importTransaction.TicketCode}",
            Items = importTransaction.Items.Select(i =>
            {
                var clone = new WarehouseTransactionItem
                {
                    MaterialCode = i.MaterialCode,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    ConstructionPhase = i.ConstructionPhase,
                    Notes = i.Notes
                };
                TransactionItemHelper.CopySnapshots(i, clone);
                return clone;
            }).ToList()
        };

        importTransaction.LinkedTicketCode = exportTicketCode;
        db.WarehouseTransactions.Add(exportTransaction);

        foreach (var item in importTransaction.Items)
        {
            var projectInventory = await GetOrCreateProjectInventoryAsync(
                projectCode, item.MaterialCode, ct);
            projectInventory.ApplyActualImport(item.Quantity);
            projectInventory.ApplyActualExport(item.Quantity);
        }
    }

    public async Task ApplyTransferAsync(
        WarehouseTransaction transaction, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(transaction.FromWarehouseId) || string.IsNullOrEmpty(transaction.ToWarehouseId))
            return;

        foreach (var item in transaction.Items)
        {
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
            throw new InvalidOperationException("Phiếu hoàn trả yêu cầu mã kho dự án.");

        var generalWarehouseId = await GetGeneralWarehouseIdAsync(ct);
        transaction.ToWarehouseId = generalWarehouseId;

        foreach (var item in transaction.Items)
        {
            var projectInventory = await GetOrCreateProjectInventoryAsync(
                projectCode, item.MaterialCode, ct);
            if (item.Quantity > projectInventory.ActualInventoryQty)
                throw new InvalidOperationException(
                    $"Vật tư {item.MaterialCode} không đủ tồn thực tế ({projectInventory.ActualInventoryQty}).");

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
            throw new InvalidOperationException("Phiếu chuyển dự án yêu cầu kho nguồn và kho đích.");
        if (string.Equals(fromCode, toCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Kho nguồn và kho đích phải khác nhau.");

        foreach (var item in transaction.Items)
        {
            var sourceInventory = await GetOrCreateProjectInventoryAsync(fromCode, item.MaterialCode, ct);
            if (item.Quantity > sourceInventory.ActualInventoryQty)
                throw new InvalidOperationException(
                    $"Vật tư {item.MaterialCode} không đủ tồn thực tế tại kho nguồn ({sourceInventory.ActualInventoryQty}).");

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
        if (exportQty > projectInventory.ActualInventoryQty)
            throw new InvalidOperationException(
                $"Vật tư {materialCode} không đủ tồn để điều chỉnh kiểm kê ({projectInventory.ActualInventoryQty}).");

        projectInventory.ApplyActualExport(exportQty);
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
