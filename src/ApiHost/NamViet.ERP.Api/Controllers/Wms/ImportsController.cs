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
[Route("api/imports")]
[Authorize]
public class ImportsController(WarehouseDbContext db, ICurrentUser currentUser) : ControllerBase
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

        var transaction = new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = ticketCode,
            TransactionType = TransactionType.Import,
            WarehouseId = dto.WarehouseId,
            ReferenceCode = dto.ReferenceCode,
            SupplierName = dto.Supplier,
            WarehouseKeeper = dto.WarehouseKeeper,
            WarehouseApprovalStatus = WmsMapper.FromClientKeeperStatus(dto.WarehouseApproval),
            IsDirectProcurement = dto.IsDirectProcurement ?? false,
            BeneficiaryProjectCode = dto.Project,
            Notes = dto.Note,
            CreatedById = currentUser.Id,
            CreatedByName = string.IsNullOrEmpty(dto.CreatedBy) ? currentUser.Name : dto.CreatedBy,
            ApprovalStatus = WmsMapper.FromClientApprovalStatus(dto.Status),
            Items = dto.Items.Select(i => new WarehouseTransactionItem
            {
                MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
                Quantity = i.Qty,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        db.WarehouseTransactions.Add(transaction);
        if (transaction.ApprovalStatus == ApprovalStatus.Approved)
            await WmsInventoryHelper.ApplyApprovedImportAsync(db, transaction, ct);
        await db.SaveChangesAsync(ct);
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

        t.WarehouseId = dto.WarehouseId;
        t.ReferenceCode = dto.ReferenceCode;
        t.SupplierName = dto.Supplier;
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
            UnitPrice = i.UnitPrice
        }).ToList();

        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToImport(t));
    }

    [HttpPost("{id}/approve")]
    public async Task<ActionResult<ImportDto>> Approve(string id, CancellationToken ct)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return NotFound();
        if (t.ApprovalStatus != ApprovalStatus.Pending)
            return BadRequest($"Phiếu đã ở trạng thái '{t.ApprovalStatus}', không thể duyệt lại.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        t.ApprovalStatus = ApprovalStatus.Approved;
        t.WarehouseApprovalStatus = WarehouseKeeperApprovalStatus.Approved;
        t.ApprovedById = currentUser.Id;
        t.ApprovedByName = currentUser.Name;
        t.ApprovedAt = DateTime.UtcNow;
        t.UpdatedAt = DateTime.UtcNow;

        await WmsInventoryHelper.ApplyApprovedImportAsync(db, t, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(WmsMapper.ToImport(t));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return NotFound();
        db.WarehouseTransactions.Remove(t);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<WarehouseTransaction?> FindImportAsync(string id, CancellationToken ct)
    {
        if (Guid.TryParse(id, out var guid))
            return await ImportQuery().FirstOrDefaultAsync(t => t.Id == guid, ct);
        return await ImportQuery().FirstOrDefaultAsync(t => t.TicketCode == id, ct);
    }
}
