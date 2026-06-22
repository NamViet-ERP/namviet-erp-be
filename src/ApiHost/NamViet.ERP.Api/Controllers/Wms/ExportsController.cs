using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/exports")]
[Authorize]
public class ExportsController(
    WarehouseDbContext db,
    IInventoryDomainService inventory,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ExportDto>>> List(CancellationToken ct)
    {
        var items = await ExportQuery().OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        var warehouseNames = await GetWarehouseNamesAsync(items, ct);
        var projectNames = await GetProjectNamesAsync(items, ct);
        return Ok(items.Select(t =>
        {
            var projectCode = t.ProjectCode ?? t.BeneficiaryProjectCode;
            var projectName = projectCode != null ? projectNames.GetValueOrDefault(projectCode) : null;
            var warehouseName = !string.IsNullOrEmpty(t.WarehouseId)
                ? warehouseNames.GetValueOrDefault(t.WarehouseId)
                : projectName;
            return WmsMapper.ToExport(t, warehouseName, projectName);
        }).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ExportDto>> GetById(string id, CancellationToken ct)
    {
        var t = await FindExportAsync(id, ct);
        if (t is null) return NotFound();
        var warehouseName = await ResolveWarehouseNameAsync(t, ct);
        var projectName = await ResolveProjectNameAsync(t, ct);
        return Ok(WmsMapper.ToExport(t, warehouseName, projectName));
    }

    [HttpPost]
    public async Task<ActionResult<ExportDto>> Create([FromBody] CreateExportDto dto, CancellationToken ct)
    {
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
        return Ok(WmsMapper.ToExport(transaction, projectName, projectName));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ExportDto>> Update(string id, [FromBody] CreateExportDto dto, CancellationToken ct)
    {
        var t = await FindExportAsync(id, ct);
        if (t is null) return NotFound();
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            return BadRequest("Phiếu đã duyệt, không thể chỉnh sửa.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (t.ApprovalStatus == ApprovalStatus.Draft)
            await inventory.CancelProjectExportPlannedAsync(t, ct);

        t.WarehouseId = dto.WarehouseId;
        t.SoCode = dto.SoCode;
        t.Customer = dto.Customer;
        t.Notes = dto.Note;
        t.ProjectCode = dto.Project;
        t.BeneficiaryProjectCode = dto.Project;
        t.ApprovalStatus = WmsMapper.FromClientApprovalStatus(dto.Status);
        t.UpdatedAt = DateTime.UtcNow;

        db.WarehouseTransactionItems.RemoveRange(t.Items);
        t.Items = dto.Items.Select(i => new WarehouseTransactionItem
        {
            TicketCode = t.TicketCode,
            MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
            Quantity = i.Qty,
        }).ToList();
        await ValidateAndSnapshotItemsAsync(t.Items, ct);

        if (t.ApprovalStatus == ApprovalStatus.Draft)
            await CreateDraftPlannedAsync(t, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var projectName = await ResolveProjectNameAsync(t, ct);
        return Ok(WmsMapper.ToExport(t, projectName, projectName));
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Admin,Warehouse_Keeper,Storekeeper")]
    public async Task<ActionResult<ExportDto>> Approve(string id, CancellationToken ct)
    {
        var t = await FindExportAsync(id, ct);
        if (t is null) return NotFound();
        if (t.ApprovalStatus is ApprovalStatus.Approved or ApprovalStatus.Rejected)
            return BadRequest($"Phiếu đã ở trạng thái '{t.ApprovalStatus}', không thể duyệt lại.");
        if (t.ApprovalStatus != ApprovalStatus.Pending && t.ApprovalStatus != ApprovalStatus.Draft)
            return BadRequest($"Phiếu ở trạng thái '{t.ApprovalStatus}', không thể duyệt.");

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
        return Ok(WmsMapper.ToExport(t, projectName, projectName));
    }

    [HttpPost("{id}/submit")]
    [Authorize(Roles = "Admin,PM,Warehouse_Keeper,Storekeeper")]
    public async Task<ActionResult<ExportDto>> Submit(string id, CancellationToken ct)
    {
        var t = await FindExportAsync(id, ct);
        if (t is null) return NotFound();
        if (t.ApprovalStatus != ApprovalStatus.Draft)
            return BadRequest("Chỉ phiếu nháp mới được PM chốt.");

        t.ApprovalStatus = ApprovalStatus.Pending;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var projectName = await ResolveProjectNameAsync(t, ct);
        return Ok(WmsMapper.ToExport(t, projectName, projectName));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var t = await FindExportAsync(id, ct);
        if (t is null) return NotFound();
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            return BadRequest("Phiếu đã duyệt, không thể xóa.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (t.ApprovalStatus == ApprovalStatus.Draft)
            await inventory.CancelProjectExportPlannedAsync(t, ct);
        db.WarehouseTransactions.Remove(t);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

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
    }
}
