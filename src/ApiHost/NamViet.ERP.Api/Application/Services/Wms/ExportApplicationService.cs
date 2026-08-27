using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IExportApplicationService
{
    Task<List<ExportDto>> ListAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default);
    Task<ExportDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<ExportDto> CreateAsync(CreateExportDto dto, CancellationToken ct = default);
    Task<ExportDto?> UpdateAsync(string id, CreateExportDto dto, CancellationToken ct = default);
    Task<ExportDto?> ApproveAsync(string id, CancellationToken ct = default);
    Task<ExportDto?> SubmitAsync(string id, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}

public sealed class ExportApplicationService(
    WarehouseDbContext db,
    IInventoryDomainService inventory,
    IStocktakeFreezeGuard stocktakeFreeze,
    ICurrentUser currentUser,
    IWmsCatalogQueryService catalog) : IExportApplicationService
{
    public async Task<List<ExportDto>> ListAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default)
    {
        var items = await ExportQuery()
            .WhereCreatedBetween(dateFrom, dateTo)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        var warehouseNames = await GetWarehouseNamesAsync(items, ct);
        var projectNames = await GetProjectNamesAsync(items, ct);
        return items.Select(t =>
        {
            var projectCode = t.ProjectCode ?? t.BeneficiaryProjectCode;
            var projectName = projectCode != null ? projectNames.GetValueOrDefault(projectCode) : null;
            var warehouseName = !string.IsNullOrEmpty(t.WarehouseId)
                ? warehouseNames.GetValueOrDefault(t.WarehouseId)
                : projectName;
            return WmsMapper.ToExport(t, warehouseName, projectName);
        }).ToList();
    }

    public async Task<ExportDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var t = await FindExportAsync(id, ct);
        if (t is null) return null;
        var warehouseName = await ResolveWarehouseNameAsync(t, ct);
        var projectName = await ResolveProjectNameAsync(t, ct);
        return WmsMapper.ToExport(t, warehouseName, projectName);
    }

    public async Task<ExportDto> CreateAsync(CreateExportDto dto, CancellationToken ct = default)
    {
        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([dto.Project], ct);

        var count = await db.WarehouseTransactions.CountAsync(t => t.TransactionType == TransactionType.Export, ct);
        var ticketCode = string.IsNullOrWhiteSpace(dto.VoucherCode)
            ? WarehouseTransaction.GenerateTicketCode(TransactionType.Export, count + 1)
            : dto.VoucherCode;

        var approvalStatus = WmsMapper.FromClientApprovalStatus(dto.Status);
        var transaction = BuildTransaction(dto, ticketCode, approvalStatus);
        await ValidateAndSnapshotItemsAsync(transaction.Items, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.WarehouseTransactions.Add(transaction);

        if (approvalStatus is ApprovalStatus.Draft or ApprovalStatus.Pending)
            await CreateDraftPlannedAsync(transaction, ct);
        else if (approvalStatus == ApprovalStatus.Approved)
            await inventory.ApplyApprovedExportAsync(transaction, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await db.Entry(transaction).Collection(t => t.Items).LoadAsync(ct);
        foreach (var item in transaction.Items)
            await db.Entry(item).Reference(i => i.Material).LoadAsync(ct);

        var projectName = await ResolveProjectNameAsync(transaction, ct);
        return WmsMapper.ToExport(transaction, projectName, projectName);
    }

    public async Task<ExportDto?> UpdateAsync(string id, CreateExportDto dto, CancellationToken ct = default)
    {
        var t = await FindExportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new DomainRuleException("Phiếu đã duyệt, không thể chỉnh sửa.");

        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([dto.Project], ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (HasPlannedInventory(t.ApprovalStatus))
            await inventory.CancelProjectExportPlannedAsync(t, ct);

        t.WarehouseId = dto.WarehouseId;
        t.SoCode = dto.SoCode;
        t.Customer = dto.Customer;
        t.Notes = dto.Note;
        t.ProjectCode = dto.Project;
        t.BeneficiaryProjectCode = dto.Project;
        var nextStatus = WmsMapper.FromClientApprovalStatus(dto.Status);
        if (nextStatus == ApprovalStatus.Approved)
            throw new DomainRuleException("Không thể duyệt phiếu qua cập nhật. Hãy dùng thao tác Duyệt kho.");
        t.ApprovalStatus = nextStatus;
        t.UpdatedAt = DateTime.UtcNow;

        db.WarehouseTransactionItems.RemoveRange(t.Items);
        t.Items = dto.Items.Select(i => new WarehouseTransactionItem
        {
            TicketCode = t.TicketCode,
            MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
            Quantity = i.Qty,
        }).ToList();
        await ValidateAndSnapshotItemsAsync(t.Items, ct);

        if (HasPlannedInventory(t.ApprovalStatus))
            await CreateDraftPlannedAsync(t, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var projectName = await ResolveProjectNameAsync(t, ct);
        return WmsMapper.ToExport(t, projectName, projectName);
    }

    public async Task<ExportDto?> ApproveAsync(string id, CancellationToken ct = default)
    {
        var t = await FindExportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus is ApprovalStatus.Approved or ApprovalStatus.Rejected)
            throw new DomainRuleException($"Phiếu đã ở trạng thái '{t.ApprovalStatus}', không thể duyệt lại.");
        if (t.ApprovalStatus != ApprovalStatus.Pending)
            throw new DomainRuleException("Chỉ phiếu đã chốt dự kiến (Pending) mới được duyệt kho.");

        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([t.ProjectCode ?? t.BeneficiaryProjectCode], ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        t.ApprovalStatus = ApprovalStatus.Approved;
        t.ApprovedById = currentUser.Id;
        t.ApprovedByName = currentUser.Name;
        t.ApprovedAt = DateTime.UtcNow;
        t.UpdatedAt = DateTime.UtcNow;

        await inventory.ApplyApprovedExportAsync(t, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var projectName = await ResolveProjectNameAsync(t, ct);
        return WmsMapper.ToExport(t, projectName, projectName);
    }

    public async Task<ExportDto?> SubmitAsync(string id, CancellationToken ct = default)
    {
        var t = await FindExportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus != ApprovalStatus.Draft)
            throw new DomainRuleException("Chỉ phiếu nháp mới được PM chốt.");

        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([t.ProjectCode ?? t.BeneficiaryProjectCode], ct);

        t.ApprovalStatus = ApprovalStatus.Pending;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var projectName = await ResolveProjectNameAsync(t, ct);
        return WmsMapper.ToExport(t, projectName, projectName);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var t = await FindExportAsync(id, ct);
        if (t is null) return false;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new DomainRuleException("Phiếu đã duyệt, không thể xóa.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (HasPlannedInventory(t.ApprovalStatus))
            await inventory.CancelProjectExportPlannedAsync(t, ct);
        db.WarehouseTransactions.Remove(t);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private static bool HasPlannedInventory(ApprovalStatus status) =>
        status is ApprovalStatus.Draft or ApprovalStatus.Pending;

    private async Task CreateDraftPlannedAsync(WarehouseTransaction transaction, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(transaction.ProjectCode ?? transaction.BeneficiaryProjectCode))
            return;

        await inventory.CreateProjectExportPlannedAsync(transaction, ct);
    }

    private WarehouseTransaction BuildTransaction(
        CreateExportDto dto, string ticketCode, ApprovalStatus approvalStatus) =>
        new()
        {
            Id = Guid.NewGuid(),
            TicketCode = ticketCode,
            TransactionType = TransactionType.Export,
            WarehouseId = dto.WarehouseId,
            SoCode = dto.SoCode,
            Customer = dto.Customer,
            ProjectCode = dto.Project,
            BeneficiaryProjectCode = dto.Project,
            Notes = dto.Note,
            CreatedById = currentUser.Id,
            CreatedByName = string.IsNullOrEmpty(dto.CreatedBy) ? currentUser.Name : dto.CreatedBy,
            ApprovalStatus = approvalStatus,
            Items = dto.Items.Select(i => new WarehouseTransactionItem
            {
                MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
                Quantity = i.Qty,
            }).ToList()
        };

    private IQueryable<WarehouseTransaction> ExportQuery() =>
        db.WarehouseTransactions
            .Include(t => t.Items).ThenInclude(i => i.Material)
            .Where(t => t.TransactionType == TransactionType.Export);

    private async Task<WarehouseTransaction?> FindExportAsync(string id, CancellationToken ct)
    {
        if (Guid.TryParse(id, out var guid))
            return await ExportQuery().FirstOrDefaultAsync(t => t.Id == guid, ct);
        return await ExportQuery().FirstOrDefaultAsync(t => t.TicketCode == id, ct);
    }

    private async Task<Dictionary<string, string>> GetWarehouseNamesAsync(
        List<WarehouseTransaction> items, CancellationToken ct)
    {
        var ids = items.Select(i => i.WarehouseId).Where(x => x != null).Distinct().ToList();
        return await db.WmsWarehouses.Where(w => ids.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name, ct);
    }

    private async Task<Dictionary<string, string>> GetProjectNamesAsync(
        List<WarehouseTransaction> items, CancellationToken ct)
    {
        var codes = items
            .Select(t => t.ProjectCode ?? t.BeneficiaryProjectCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();
        return await db.Projects
            .Where(p => codes.Contains(p.ProjectCode))
            .ToDictionaryAsync(p => p.ProjectCode, p => p.ProjectName, ct);
    }

    private async Task<string?> ResolveWarehouseNameAsync(WarehouseTransaction t, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(t.WarehouseId)) return null;
        return await db.WmsWarehouses.Where(w => w.Id == t.WarehouseId)
            .Select(w => w.Name).FirstOrDefaultAsync(ct);
    }

    private async Task<string?> ResolveProjectNameAsync(WarehouseTransaction t, CancellationToken ct)
    {
        var code = t.ProjectCode ?? t.BeneficiaryProjectCode;
        if (string.IsNullOrWhiteSpace(code)) return null;
        return await db.Projects.Where(p => p.ProjectCode == code)
            .Select(p => p.ProjectName).FirstOrDefaultAsync(ct);
    }

    private async Task ValidateAndSnapshotItemsAsync(
        IEnumerable<WarehouseTransactionItem> items, CancellationToken ct)
    {
        var list = items.ToList();
        var materials = await TransactionItemHelper.LoadMaterialsAsync(
            db, list.Select(i => i.MaterialCode), ct);
        TransactionItemHelper.EnsureActiveMaterials(list.Select(i => i.MaterialCode), materials);
        await TransactionItemHelper.ApplySnapshotsAsync(db, list, ct);

        // Giá xuất = WAC nhập đã duyệt trong 12 tháng gần nhất (cùng công thức tồn kho).
        foreach (var item in list)
        {
            if (item.UnitPrice > 0) continue;
            item.UnitPrice = await catalog.GetLatestPriceAsync(item.MaterialCode, ct);
        }
    }
}
