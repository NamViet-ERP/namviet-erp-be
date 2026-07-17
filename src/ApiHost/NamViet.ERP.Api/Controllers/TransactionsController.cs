using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Application.Transactions;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/transactions")]
[Authorize]
public class TransactionsController(WarehouseDbContext db, IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.transactions.view:all")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null, [FromQuery] string? status = null,
        [FromQuery] string? projectCode = null, CancellationToken ct = default)
    {
        var query = db.WarehouseTransactions
            .Include(t => t.Items).ThenInclude(i => i.Material)
            .Include(t => t.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<TransactionType>(type, out var tt))
            query = query.Where(t => t.TransactionType == tt);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ApprovalStatus>(status, out var ap))
            query = query.Where(t => t.ApprovalStatus == ap);
        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(t => t.ProjectCode == projectCode);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(t => new TransactionDto(
            t.TicketCode, t.TransactionType.ToString(), t.ProjectCode,
            t.SupplierId, t.Supplier?.SupplierName,
            t.InvoiceNumber, t.DnttNumber,
            t.CreatedByName, t.ApprovalStatus.ToString(),
            t.ApprovedByName, t.ApprovedAt, t.Notes, t.CreatedAt,
            t.Items.Select(i => new TransactionItemDto(
                i.Id, i.MaterialCode, TransactionItemHelper.ResolveName(i),
                i.Quantity, TransactionItemHelper.ResolveUnit(i), i.UnitPrice, i.TotalAmount,
                i.ConstructionPhase, i.Notes)).ToList())).ToList();

        return Ok(PagedResult<TransactionDto>.Create(dtos, total, page, pageSize));
    }

    [HttpGet("{ticketCode}")]
    [RequirePermission("wms.transactions.view:all")]
    public async Task<IActionResult> GetById(string ticketCode, CancellationToken ct)
    {
        var t = await db.WarehouseTransactions
            .Include(t => t.Items).ThenInclude(i => i.Material)
            .Include(t => t.Supplier)
            .FirstOrDefaultAsync(x => x.TicketCode == ticketCode, ct);
        if (t is null) return NotFound();

        return Ok(new TransactionDto(
            t.TicketCode, t.TransactionType.ToString(), t.ProjectCode,
            t.SupplierId, t.Supplier?.SupplierName,
            t.InvoiceNumber, t.DnttNumber,
            t.CreatedByName, t.ApprovalStatus.ToString(),
            t.ApprovedByName, t.ApprovedAt, t.Notes, t.CreatedAt,
            t.Items.Select(i => new TransactionItemDto(
                i.Id, i.MaterialCode, TransactionItemHelper.ResolveName(i),
                i.Quantity, TransactionItemHelper.ResolveUnit(i), i.UnitPrice, i.TotalAmount,
                i.ConstructionPhase, i.Notes)).ToList()));
    }

    [HttpPost]
    [RequirePermission("wms.transactions.create")]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateTransactionCommand(request), ct);
        return CreatedAtAction(nameof(GetById), new { ticketCode = result.TicketCode }, result);
    }

    [HttpPost("{ticketCode}/approve")]
    [RequirePermission("wms.transactions.approve")]
    public async Task<IActionResult> Approve(string ticketCode, [FromBody] ApproveTransactionRequest request, CancellationToken ct)
    {
        var status = await mediator.Send(
            new ApproveTransactionCommand(ticketCode, request.Approve, request.Reason), ct);
        return Ok(new { ticketCode, status });
    }
}
