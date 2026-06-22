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
[Route("api/imports")]
[Authorize]
public class ImportsController(
    WarehouseDbContext db,
    IInventoryDomainService inventory,
    ICurrentUser currentUser) : ControllerBase
{
    private IQueryable<WarehouseTransaction> ImportQuery() =>
        db.WarehouseTransactions
            .Include(t => t.Items).ThenInclude(i => i.Material)
            .Include(t => t.Supplier)
            .Where(t => t.TransactionType == TransactionType.Import);

    [HttpGet]
    public async Task<ActionResult<List<ImportDto>>> List(CancellationToken ct)
    {
        var items = await ImportQuery().OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        return Ok(items.Select(WmsMapper.ToImport).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ImportDto>> GetById(string id, CancellationToken ct)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return NotFound();
        return Ok(WmsMapper.ToImport(t));
    }

    [HttpPost]
    public async Task<ActionResult<ImportDto>> Create([FromBody] CreateImportDto dto, CancellationToken ct)
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
        return Ok(WmsMapper.ToImport(transaction));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ImportDto>> Update(string id, [FromBody] CreateImportDto dto, CancellationToken ct)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return NotFound();
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            return BadRequest("Phiếu đã duyệt, không thể chỉnh sửa.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (t.ApprovalStatus == ApprovalStatus.Draft)
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
        t.ApprovalStatus = WmsMapper.FromClientApprovalStatus(dto.Status);
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

        if (t.ApprovalStatus == ApprovalStatus.Draft)
            await CreateDraftReservationsAsync(t, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(WmsMapper.ToImport(t));
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Admin,Warehouse_Keeper,Storekeeper")]
    public async Task<ActionResult<ImportDto>> Approve(string id, CancellationToken ct)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return NotFound();
        if (t.ApprovalStatus is ApprovalStatus.Approved or ApprovalStatus.Rejected)
            return BadRequest($"Phiếu đã ở trạng thái '{t.ApprovalStatus}', không thể duyệt lại.");
        if (t.ApprovalStatus != ApprovalStatus.Pending && t.ApprovalStatus != ApprovalStatus.Draft)
            return BadRequest($"Phiếu ở trạng thái '{t.ApprovalStatus}', không thể duyệt.");

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

        return Ok(WmsMapper.ToImport(t));
    }

    /// <summary>PM chốt phiếu nháp → chờ thủ kho duyệt (giữ nguyên reservation).</summary>
    [HttpPost("{id}/submit")]
    [Authorize(Roles = "Admin,PM,Warehouse_Keeper,Storekeeper")]
    public async Task<ActionResult<ImportDto>> Submit(string id, CancellationToken ct)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return NotFound();
        if (t.ApprovalStatus != ApprovalStatus.Draft)
            return BadRequest("Chỉ phiếu nháp mới được PM chốt.");

        t.ApprovalStatus = ApprovalStatus.Pending;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToImport(t));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return NotFound();
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            return BadRequest("Phiếu đã duyệt, không thể xóa.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (t.ApprovalStatus == ApprovalStatus.Draft)
        {
            await inventory.CancelReservationsAsync(t.TicketCode, ct);
            await inventory.CancelProjectImportPlannedAsync(t, ct);
        }
        db.WarehouseTransactions.Remove(t);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

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
