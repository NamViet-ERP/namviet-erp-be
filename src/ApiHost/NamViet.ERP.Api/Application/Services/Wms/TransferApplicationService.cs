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

public interface ITransferApplicationService
{
    Task<List<TransferDto>> ListAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default);
    Task<TransferDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<TransferDto> CreateAsync(CreateTransferDto dto, CancellationToken ct = default);
    Task<TransferDto?> UpdateAsync(string id, CreateTransferDto dto, CancellationToken ct = default);
    Task<TransferDto?> ApproveAsync(string id, CancellationToken ct = default);
    Task<TransferDto?> SubmitAsync(string id, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}

public sealed class TransferApplicationService(
    WarehouseDbContext db,
    IInventoryDomainService inventory,
    ICurrentUser currentUser) : ITransferApplicationService
{
    public async Task<List<TransferDto>> ListAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default)
    {
        var items = await Query()
            .WhereCreatedBetween(dateFrom, dateTo)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        var warehouseNames = await GetWarehouseNamesAsync(items, ct);
        var projectNames = await GetProjectNamesAsync(items, ct);
        return items.Select(t => MapTransfer(t, warehouseNames, projectNames)).ToList();
    }

    public async Task<TransferDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return null;
        var warehouseNames = await GetWarehouseNamesAsync([t], ct);
        var projectNames = await GetProjectNamesAsync([t], ct);
        return MapTransfer(t, warehouseNames, projectNames);
    }

    public async Task<TransferDto> CreateAsync(CreateTransferDto dto, CancellationToken ct = default)
    {
        var validationError = await ValidateTransferRequestAsync(dto, ct);
        if (validationError is not null)
            throw new InvalidOperationException(validationError);

        var count = await db.WarehouseTransactions.CountAsync(t => t.TransactionType == TransactionType.Transfer, ct);
        var ticketCode = string.IsNullOrWhiteSpace(dto.VoucherCode)
            ? WarehouseTransaction.GenerateTicketCode(TransactionType.Transfer, count + 1)
            : dto.VoucherCode;

        var approvalStatus = WmsMapper.FromClientApprovalStatus(dto.Status);
        var transaction = await BuildTransactionAsync(dto, ticketCode, approvalStatus, ct);
        await ValidateAndSnapshotItemsAsync(transaction.Items, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.WarehouseTransactions.Add(transaction);
        await ApplyDraftOrApprovedAsync(transaction, approvalStatus, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await db.Entry(transaction).Collection(t => t.Items).LoadAsync(ct);
        foreach (var item in transaction.Items)
            await db.Entry(item).Reference(i => i.Material).LoadAsync(ct);

        return await MapTransferResponseAsync(transaction, ct);
    }

    public async Task<TransferDto?> UpdateAsync(string id, CreateTransferDto dto, CancellationToken ct = default)
    {
        var validationError = await ValidateTransferRequestAsync(dto, ct);
        if (validationError is not null)
            throw new InvalidOperationException(validationError);

        var t = await FindAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new InvalidOperationException("Phiếu đã duyệt, không thể chỉnh sửa.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (IsProjectOutbound(t) && HasPlannedInventory(t.ApprovalStatus))
            await inventory.CancelProjectExportPlannedAsync(t, ct);

        t.FromWarehouseId = dto.FromWarehouseId;
        t.ToWarehouseId = dto.ToWarehouseId;
        t.ProjectCode = dto.Project?.Trim();
        t.BeneficiaryProjectCode = ResolveBeneficiaryProjectCode(dto);
        t.Notes = dto.Note;
        var nextStatus = WmsMapper.FromClientApprovalStatus(dto.Status);
        if (nextStatus == ApprovalStatus.Approved)
            throw new InvalidOperationException("Không thể duyệt phiếu qua cập nhật. Hãy dùng thao tác Duyệt kho.");
        t.ApprovalStatus = nextStatus;
        t.UpdatedAt = DateTime.UtcNow;

        db.WarehouseTransactionItems.RemoveRange(t.Items);
        t.Items = MapItems(dto.Items, t.TicketCode);
        await ValidateAndSnapshotItemsAsync(t.Items, ct);

        if (IsProjectOutbound(t) && HasPlannedInventory(t.ApprovalStatus))
            await inventory.CreateProjectExportPlannedAsync(t, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await MapTransferResponseAsync(t, ct);
    }

    public async Task<TransferDto?> ApproveAsync(string id, CancellationToken ct = default)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus is ApprovalStatus.Approved or ApprovalStatus.Rejected)
            throw new InvalidOperationException($"Phiếu đã ở trạng thái '{t.ApprovalStatus}', không thể duyệt lại.");
        if (t.ApprovalStatus != ApprovalStatus.Pending)
            throw new InvalidOperationException("Chỉ phiếu đã chốt dự kiến (Pending) mới được duyệt kho.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        t.ApprovalStatus = ApprovalStatus.Approved;
        t.ApprovedById = currentUser.Id;
        t.ApprovedByName = currentUser.Name;
        t.ApprovedAt = DateTime.UtcNow;
        t.UpdatedAt = DateTime.UtcNow;

        await ApplyApprovedTransferAsync(t, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await MapTransferResponseAsync(t, ct);
    }

    public async Task<TransferDto?> SubmitAsync(string id, CancellationToken ct = default)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus != ApprovalStatus.Draft)
            throw new InvalidOperationException("Chỉ phiếu nháp mới được PM chốt.");

        t.ApprovalStatus = ApprovalStatus.Pending;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await MapTransferResponseAsync(t, ct);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return false;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new InvalidOperationException("Phiếu đã duyệt, không thể xóa.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (IsProjectOutbound(t) && HasPlannedInventory(t.ApprovalStatus))
            await inventory.CancelProjectExportPlannedAsync(t, ct);
        db.WarehouseTransactions.Remove(t);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private static bool HasPlannedInventory(ApprovalStatus status) =>
        status is ApprovalStatus.Draft or ApprovalStatus.Pending;

    private static bool IsProjectOutbound(WarehouseTransaction t) =>
        !string.IsNullOrWhiteSpace(t.ProjectCode);

    private static bool IsCrossProjectTransfer(WarehouseTransaction t) =>
        !string.IsNullOrWhiteSpace(t.ProjectCode)
        && !string.IsNullOrWhiteSpace(t.BeneficiaryProjectCode)
        && !string.Equals(t.ProjectCode, t.BeneficiaryProjectCode, StringComparison.OrdinalIgnoreCase);

    private static bool IsProjectReturn(WarehouseTransaction t) =>
        IsProjectOutbound(t) && !IsCrossProjectTransfer(t);

    private static string? ResolveBeneficiaryProjectCode(CreateTransferDto dto)
    {
        var toProject = dto.ToProject?.Trim();
        return string.IsNullOrWhiteSpace(toProject) ? null : toProject;
    }

    private async Task<string?> ValidateTransferRequestAsync(CreateTransferDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Project))
            return "Vui lòng chọn kho dự án nguồn.";

        var toProject = dto.ToProject?.Trim();
        if (string.IsNullOrWhiteSpace(toProject))
            return null;

        if (string.Equals(dto.Project.Trim(), toProject, StringComparison.OrdinalIgnoreCase))
            return "Kho nguồn và kho đích phải khác nhau.";

        var target = await db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == toProject, ct);
        if (target is null)
            return $"Không tìm thấy kho dự án đích '{toProject}'.";
        if (target.Status != ProjectStatus.Active)
            return $"Kho dự án đích '{toProject}' không ở trạng thái Active.";

        return null;
    }

    private async Task ApplyDraftOrApprovedAsync(
        WarehouseTransaction transaction, ApprovalStatus approvalStatus, CancellationToken ct)
    {
        if (!IsProjectOutbound(transaction))
        {
            if (approvalStatus == ApprovalStatus.Approved)
                await inventory.ApplyTransferAsync(transaction, ct);
            return;
        }

        if (approvalStatus is ApprovalStatus.Draft or ApprovalStatus.Pending)
            await inventory.CreateProjectExportPlannedAsync(transaction, ct);
        else if (approvalStatus == ApprovalStatus.Approved)
            await ApplyApprovedTransferAsync(transaction, ct);
    }

    private async Task ApplyApprovedTransferAsync(WarehouseTransaction transaction, CancellationToken ct)
    {
        if (IsCrossProjectTransfer(transaction))
            await inventory.ApplyCrossProjectTransferAsync(transaction, ct);
        else if (IsProjectReturn(transaction))
            await inventory.ApplyReturnToGeneralAsync(transaction, ct);
        else
            await inventory.ApplyTransferAsync(transaction, ct);
    }

    private async Task<WarehouseTransaction> BuildTransactionAsync(
        CreateTransferDto dto, string ticketCode, ApprovalStatus approvalStatus, CancellationToken ct)
    {
        var generalWarehouseId = await inventory.GetGeneralWarehouseIdAsync(ct);
        var isCrossProject = !string.IsNullOrWhiteSpace(dto.ToProject?.Trim());

        return new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = ticketCode,
            TransactionType = TransactionType.Transfer,
            FromWarehouseId = dto.FromWarehouseId,
            ToWarehouseId = isCrossProject
                ? null
                : string.IsNullOrWhiteSpace(dto.ToWarehouseId) ? generalWarehouseId : dto.ToWarehouseId,
            ProjectCode = dto.Project?.Trim(),
            BeneficiaryProjectCode = ResolveBeneficiaryProjectCode(dto),
            Notes = dto.Note,
            CreatedById = currentUser.Id,
            CreatedByName = string.IsNullOrEmpty(dto.CreatedBy) ? currentUser.Name : dto.CreatedBy,
            ApprovalStatus = approvalStatus,
            Items = MapItems(dto.Items, ticketCode),
        };
    }

    private static List<WarehouseTransactionItem> MapItems(
        IEnumerable<TransferItemDto> items, string ticketCode) =>
        items.Select(i => new WarehouseTransactionItem
        {
            TicketCode = ticketCode,
            MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
            Quantity = i.Qty,
            ConstructionPhase = string.IsNullOrWhiteSpace(i.ConstructionPhase) ? null : i.ConstructionPhase.Trim(),
        }).ToList();

    private async Task<TransferDto> MapTransferResponseAsync(WarehouseTransaction t, CancellationToken ct)
    {
        var projectName = await ResolveProjectNameAsync(t.ProjectCode, ct);
        if (IsCrossProjectTransfer(t))
        {
            var toProjectName = await ResolveProjectNameAsync(t.BeneficiaryProjectCode, ct);
            return WmsMapper.ToTransfer(t, projectName: projectName, toName: toProjectName, toProjectName: toProjectName);
        }

        var generalName = await ResolveGeneralWarehouseNameAsync(ct);
        return WmsMapper.ToTransfer(t, toName: generalName, projectName: projectName);
    }

    private TransferDto MapTransfer(
        WarehouseTransaction t,
        Dictionary<string, string> warehouseNames,
        Dictionary<string, string> projectNames)
    {
        var projectName = t.ProjectCode != null ? projectNames.GetValueOrDefault(t.ProjectCode) : null;
        if (IsCrossProjectTransfer(t))
        {
            var toProjectName = t.BeneficiaryProjectCode != null
                ? projectNames.GetValueOrDefault(t.BeneficiaryProjectCode)
                : null;
            return WmsMapper.ToTransfer(t, projectName: projectName, toName: toProjectName, toProjectName: toProjectName);
        }

        var toName = !string.IsNullOrEmpty(t.ToWarehouseId)
            ? warehouseNames.GetValueOrDefault(t.ToWarehouseId)
            : null;
        return WmsMapper.ToTransfer(t, toName: toName, projectName: projectName);
    }

    private IQueryable<WarehouseTransaction> Query() =>
        db.WarehouseTransactions
            .Include(t => t.Items).ThenInclude(i => i.Material)
            .Where(t => t.TransactionType == TransactionType.Transfer);

    private async Task<WarehouseTransaction?> FindAsync(string id, CancellationToken ct)
    {
        if (Guid.TryParse(id, out var guid))
            return await Query().FirstOrDefaultAsync(t => t.Id == guid, ct);
        return await Query().FirstOrDefaultAsync(t => t.TicketCode == id, ct);
    }

    private async Task<Dictionary<string, string>> GetWarehouseNamesAsync(
        List<WarehouseTransaction> items, CancellationToken ct)
    {
        var ids = items
            .SelectMany(i => new[] { i.FromWarehouseId, i.ToWarehouseId })
            .Where(x => x != null).Distinct().ToList();
        return await db.WmsWarehouses.Where(w => ids.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name, ct);
    }

    private async Task<Dictionary<string, string>> GetProjectNamesAsync(
        List<WarehouseTransaction> items, CancellationToken ct)
    {
        var codes = items
            .SelectMany(t => new[] { t.ProjectCode, t.BeneficiaryProjectCode })
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();
        return await db.Projects
            .Where(p => codes.Contains(p.ProjectCode))
            .ToDictionaryAsync(p => p.ProjectCode, p => p.ProjectName, ct);
    }

    private async Task<string?> ResolveProjectNameAsync(string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return await db.Projects.Where(p => p.ProjectCode == code)
            .Select(p => p.ProjectName).FirstOrDefaultAsync(ct);
    }

    private async Task<string?> ResolveGeneralWarehouseNameAsync(CancellationToken ct)
    {
        var generalId = await inventory.GetGeneralWarehouseIdAsync(ct);
        return await db.WmsWarehouses.Where(w => w.Id == generalId)
            .Select(w => w.Name).FirstOrDefaultAsync(ct);
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
}
