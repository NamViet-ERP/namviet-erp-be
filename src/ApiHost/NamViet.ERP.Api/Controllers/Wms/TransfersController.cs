using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/transfers")]
[Authorize]
public class TransfersController(WarehouseDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TransferDto>>> List(CancellationToken ct)
    {
        var items = await Query().OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        var names = await GetWarehouseNamesAsync(items, ct);
        return Ok(items.Select(t => WmsMapper.ToTransfer(
            t,
            names.GetValueOrDefault(t.FromWarehouseId ?? ""),
            names.GetValueOrDefault(t.ToWarehouseId ?? ""))).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TransferDto>> GetById(string id, CancellationToken ct)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return NotFound();
        var names = await db.WmsWarehouses
            .Where(w => w.Id == t.FromWarehouseId || w.Id == t.ToWarehouseId)
            .ToDictionaryAsync(w => w.Id, w => w.Name, ct);
        return Ok(WmsMapper.ToTransfer(
            t,
            names.GetValueOrDefault(t.FromWarehouseId ?? ""),
            names.GetValueOrDefault(t.ToWarehouseId ?? "")));
    }

    [HttpPost]
    public async Task<ActionResult<TransferDto>> Create([FromBody] TransferDto dto, CancellationToken ct)
    {
        var count = await db.WarehouseTransactions.CountAsync(t => t.TransactionType == TransactionType.Transfer, ct);
        var t = new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = string.IsNullOrWhiteSpace(dto.VoucherCode)
                ? $"CK-{(count + 1):D6}" : dto.VoucherCode,
            TransactionType = TransactionType.Transfer,
            FromWarehouseId = dto.FromWarehouseId,
            ToWarehouseId = dto.ToWarehouseId,
            Notes = dto.Note,
            ApprovedByName = dto.ApprovedBy,
            CreatedById = currentUser.Id,
            CreatedByName = string.IsNullOrEmpty(dto.CreatedBy) ? currentUser.Name : dto.CreatedBy,
            ApprovalStatus = WmsMapper.FromClientApprovalStatus(dto.Status),
            Items = dto.Items.Select(i => new WarehouseTransactionItem
            {
                MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
                Quantity = i.Qty
            }).ToList()
        };
        db.WarehouseTransactions.Add(t);
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            await WmsInventoryHelper.ApplyApprovedTransferAsync(db, t, ct);
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToTransfer(t, dto.FromWarehouseName, dto.ToWarehouseName));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TransferDto>> Update(string id, [FromBody] TransferDto dto, CancellationToken ct)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return NotFound();
        var previousStatus = t.ApprovalStatus;
        t.FromWarehouseId = dto.FromWarehouseId;
        t.ToWarehouseId = dto.ToWarehouseId;
        t.Notes = dto.Note;
        t.ApprovedByName = dto.ApprovedBy;
        t.ApprovalStatus = WmsMapper.FromClientApprovalStatus(dto.Status);
        t.UpdatedAt = DateTime.UtcNow;
        db.WarehouseTransactionItems.RemoveRange(t.Items);
        t.Items = dto.Items.Select(i => new WarehouseTransactionItem
        {
            TicketCode = t.TicketCode,
            MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
            Quantity = i.Qty
        }).ToList();
        if (WmsInventoryHelper.IsNewlyApproved(previousStatus, t.ApprovalStatus))
            await WmsInventoryHelper.ApplyApprovedTransferAsync(db, t, ct);
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToTransfer(t, dto.FromWarehouseName, dto.ToWarehouseName));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return NotFound();
        db.WarehouseTransactions.Remove(t);
        await db.SaveChangesAsync(ct);
        return NoContent();
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
}
