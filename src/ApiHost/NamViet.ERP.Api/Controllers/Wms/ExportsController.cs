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
[Route("api/exports")]
[Authorize]
public class ExportsController(WarehouseDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ExportDto>>> List(CancellationToken ct)
    {
        var items = await Query().OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        var warehouseNames = await GetWarehouseNamesAsync(items, ct);
        return Ok(items.Select(t => WmsMapper.ToExport(t, warehouseNames.GetValueOrDefault(t.WarehouseId ?? ""))).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ExportDto>> GetById(string id, CancellationToken ct)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return NotFound();
        var name = await db.WmsWarehouses.Where(w => w.Id == t.WarehouseId).Select(w => w.Name).FirstOrDefaultAsync(ct);
        return Ok(WmsMapper.ToExport(t, name));
    }

    [HttpPost]
    public async Task<ActionResult<ExportDto>> Create([FromBody] ExportDto dto, CancellationToken ct)
    {
        var count = await db.WarehouseTransactions.CountAsync(t => t.TransactionType == TransactionType.Export, ct);
        var t = new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = string.IsNullOrWhiteSpace(dto.VoucherCode)
                ? WarehouseTransaction.GenerateTicketCode(TransactionType.Export, count + 1) : dto.VoucherCode,
            TransactionType = TransactionType.Export,
            WarehouseId = dto.WarehouseId,
            SoCode = dto.SoCode,
            Customer = dto.Customer,
            Notes = dto.Note,
            CreatedById = currentUser.Id,
            CreatedByName = dto.CreatedBy,
            ApprovalStatus = WmsMapper.FromClientApprovalStatus(dto.Status),
            Items = dto.Items.Select(i => new WarehouseTransactionItem
            {
                MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
                Quantity = i.Qty
            }).ToList()
        };
        db.WarehouseTransactions.Add(t);
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            await WmsInventoryHelper.ApplyApprovedExportAsync(db, t, ct);
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToExport(t, dto.WarehouseName));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ExportDto>> Update(string id, [FromBody] ExportDto dto, CancellationToken ct)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return NotFound();
        var previousStatus = t.ApprovalStatus;
        t.WarehouseId = dto.WarehouseId;
        t.SoCode = dto.SoCode;
        t.Customer = dto.Customer;
        t.Notes = dto.Note;
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
            await WmsInventoryHelper.ApplyApprovedExportAsync(db, t, ct);
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToExport(t, dto.WarehouseName));
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
            .Where(t => t.TransactionType == TransactionType.Export);

    private async Task<WarehouseTransaction?> FindAsync(string id, CancellationToken ct)
    {
        if (Guid.TryParse(id, out var guid))
            return await Query().FirstOrDefaultAsync(t => t.Id == guid, ct);
        return await Query().FirstOrDefaultAsync(t => t.TicketCode == id, ct);
    }

    private async Task<Dictionary<string, string>> GetWarehouseNamesAsync(
        List<WarehouseTransaction> items, CancellationToken ct)
    {
        var ids = items.Select(i => i.WarehouseId).Where(x => x != null).Distinct().ToList();
        return await db.WmsWarehouses.Where(w => ids.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name, ct);
    }
}
