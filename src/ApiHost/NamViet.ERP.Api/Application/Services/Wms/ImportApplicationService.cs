using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IImportApplicationService
{
    Task<List<ImportDto>> ListAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default);
    Task<ImportDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<ImportDto> CreateAsync(CreateImportDto dto, CancellationToken ct = default);
    Task<ImportDto?> UpdateAsync(string id, CreateImportDto dto, CancellationToken ct = default);
    Task<ImportDto?> ApproveAsync(string id, CancellationToken ct = default);
    Task<ImportDto?> SubmitAsync(string id, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}

public sealed class ImportApplicationService(
    WarehouseDbContext db,
    IInventoryDomainService inventory,
    ICurrentUser currentUser) : IImportApplicationService
{
    public async Task<List<ImportDto>> ListAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default)
    {
        var items = await ImportQuery()
            .WhereCreatedBetween(dateFrom, dateTo)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        return items.Select(WmsMapper.ToImport).ToList();
    }

    public async Task<ImportDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        return t is null ? null : WmsMapper.ToImport(t);
    }

    public async Task<ImportDto> CreateAsync(CreateImportDto dto, CancellationToken ct = default)
    {
        var count = await db.WarehouseTransactions.CountAsync(t => t.TransactionType == TransactionType.Import, ct);
        var ticketCode = string.IsNullOrWhiteSpace(dto.VoucherCode)
            ? WarehouseTransaction.GenerateTicketCode(TransactionType.Import, count + 1)
            : dto.VoucherCode;

        var approvalStatus = WmsMapper.FromClientApprovalStatus(dto.Status);
        var transaction = BuildTransaction(dto, ticketCode, approvalStatus);
        await ValidateAndSnapshotItemsAsync(transaction.Items, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.WarehouseTransactions.Add(transaction);

        if (approvalStatus is ApprovalStatus.Draft or ApprovalStatus.Pending)
            await CreateDraftReservationsAsync(transaction, ct);
        else if (approvalStatus == ApprovalStatus.Approved)
            await inventory.ApplyApprovedImportAsync(transaction, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await db.Entry(transaction).Collection(t => t.Items).LoadAsync(ct);
        foreach (var item in transaction.Items)
            await db.Entry(item).Reference(i => i.Material).LoadAsync(ct);
        return WmsMapper.ToImport(transaction);
    }

    public async Task<ImportDto?> UpdateAsync(string id, CreateImportDto dto, CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new InvalidOperationException("Phiếu đã duyệt, không thể chỉnh sửa.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (HasPlannedInventory(t.ApprovalStatus))
        {
            await inventory.CancelReservationsAsync(t.TicketCode, ct);
            await inventory.CancelProjectImportPlannedAsync(t, ct);
        }

        t.WarehouseId = dto.WarehouseId;
        t.ReferenceCode = dto.ReferenceCode;
        t.SupplierName = ResolveHeaderSupplier(dto);
        t.WarehouseKeeper = dto.WarehouseKeeper;
        t.WarehouseApprovalStatus = WmsMapper.FromClientKeeperStatus(dto.WarehouseApproval);
        t.IsDirectProcurement = dto.IsDirectProcurement ?? false;
        t.BeneficiaryProjectCode = dto.Project;
        t.Notes = dto.Note;
        var nextStatus = WmsMapper.FromClientApprovalStatus(dto.Status);
        if (nextStatus == ApprovalStatus.Approved)
            throw new InvalidOperationException("Không thể duyệt phiếu qua cập nhật. Hãy dùng thao tác Duyệt kho.");
        t.ApprovalStatus = nextStatus;
        t.UpdatedAt = DateTime.UtcNow;

        db.WarehouseTransactionItems.RemoveRange(t.Items);
        t.Items = dto.Items.Select(i => new WarehouseTransactionItem
        {
            TicketCode = t.TicketCode,
            MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
            Quantity = i.Qty,
            UnitPrice = i.UnitPrice,
            SupplierId = i.SupplierId,
            SupplierName = ResolveLineSupplierName(i, dto.Supplier),
        }).ToList();
        await ValidateAndSnapshotItemsAsync(t.Items, ct);

        if (HasPlannedInventory(t.ApprovalStatus))
            await CreateDraftReservationsAsync(t, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return WmsMapper.ToImport(t);
    }

    public async Task<ImportDto?> ApproveAsync(string id, CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus is ApprovalStatus.Approved or ApprovalStatus.Rejected)
            throw new InvalidOperationException($"Phiếu đã ở trạng thái '{t.ApprovalStatus}', không thể duyệt lại.");
        if (t.ApprovalStatus != ApprovalStatus.Pending)
            throw new InvalidOperationException("Chỉ phiếu đã chốt dự kiến (Pending) mới được duyệt kho.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        t.ApprovalStatus = ApprovalStatus.Approved;
        t.WarehouseApprovalStatus = WarehouseKeeperApprovalStatus.Approved;
        t.ApprovedById = currentUser.Id;
        t.ApprovedByName = currentUser.Name;
        t.ApprovedAt = DateTime.UtcNow;
        t.UpdatedAt = DateTime.UtcNow;

        await inventory.ApplyApprovedImportAsync(t, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return WmsMapper.ToImport(t);
    }

    public async Task<ImportDto?> SubmitAsync(string id, CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus != ApprovalStatus.Draft)
            throw new InvalidOperationException("Chỉ phiếu nháp mới được PM chốt.");

        t.ApprovalStatus = ApprovalStatus.Pending;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return WmsMapper.ToImport(t);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return false;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new InvalidOperationException("Phiếu đã duyệt, không thể xóa.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (HasPlannedInventory(t.ApprovalStatus))
        {
            await inventory.CancelReservationsAsync(t.TicketCode, ct);
            await inventory.CancelProjectImportPlannedAsync(t, ct);
        }
        db.WarehouseTransactions.Remove(t);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private IQueryable<WarehouseTransaction> ImportQuery() =>
        db.WarehouseTransactions
            .Include(t => t.Items).ThenInclude(i => i.Material)
            .Include(t => t.Supplier)
            .Where(t => t.TransactionType == TransactionType.Import);

    private static bool HasPlannedInventory(ApprovalStatus status) =>
        status is ApprovalStatus.Draft or ApprovalStatus.Pending;

    private async Task CreateDraftReservationsAsync(WarehouseTransaction transaction, CancellationToken ct)
    {
        if (transaction.IsDirectProcurement || string.IsNullOrWhiteSpace(transaction.BeneficiaryProjectCode))
            return;

        await inventory.CreateProjectImportPlannedAsync(transaction, ct);
    }

    private WarehouseTransaction BuildTransaction(
        CreateImportDto dto, string ticketCode, ApprovalStatus approvalStatus) =>
        new()
        {
            Id = Guid.NewGuid(),
            TicketCode = ticketCode,
            TransactionType = TransactionType.Import,
            WarehouseId = dto.WarehouseId,
            ReferenceCode = dto.ReferenceCode,
            SupplierName = ResolveHeaderSupplier(dto),
            WarehouseKeeper = dto.WarehouseKeeper,
            WarehouseApprovalStatus = WmsMapper.FromClientKeeperStatus(dto.WarehouseApproval),
            IsDirectProcurement = dto.IsDirectProcurement ?? false,
            BeneficiaryProjectCode = dto.Project,
            Notes = dto.Note,
            CreatedById = currentUser.Id,
            CreatedByName = string.IsNullOrEmpty(dto.CreatedBy) ? currentUser.Name : dto.CreatedBy,
            ApprovalStatus = approvalStatus,
            Items = dto.Items.Select(i => new WarehouseTransactionItem
            {
                MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
                Quantity = i.Qty,
                UnitPrice = i.UnitPrice,
                SupplierId = i.SupplierId,
                SupplierName = ResolveLineSupplierName(i, dto.Supplier),
            }).ToList()
        };

    private static string ResolveLineSupplierName(ImportItemDto item, string? headerSupplier)
    {
        if (!string.IsNullOrWhiteSpace(item.Supplier)) return item.Supplier.Trim();
        return headerSupplier?.Trim() ?? "";
    }

    private static string ResolveHeaderSupplier(CreateImportDto dto)
    {
        var lineNames = dto.Items
            .Select(i => ResolveLineSupplierName(i, null))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (lineNames.Count == 0)
            return dto.Supplier?.Trim() ?? "";

        if (lineNames.Count == 1)
            return lineNames[0];

        return "Nhiều NCC";
    }

    private async Task ValidateAndSnapshotItemsAsync(
        IEnumerable<WarehouseTransactionItem> items, CancellationToken ct)
    {
        var list = items.ToList();
        var materials = await TransactionItemHelper.LoadMaterialsAsync(
            db, list.Select(i => i.MaterialCode), ct);
        TransactionItemHelper.EnsureActiveMaterials(list.Select(i => i.MaterialCode), materials);
        await TransactionItemHelper.ApplySnapshotsAsync(db, list, ct);
    }

    private async Task<WarehouseTransaction?> FindImportAsync(string id, CancellationToken ct)
    {
        if (Guid.TryParse(id, out var guid))
            return await ImportQuery().FirstOrDefaultAsync(t => t.Id == guid, ct);
        return await ImportQuery().FirstOrDefaultAsync(t => t.TicketCode == id, ct);
    }
}
