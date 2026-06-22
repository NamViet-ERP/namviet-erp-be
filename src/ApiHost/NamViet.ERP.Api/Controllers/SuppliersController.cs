using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/suppliers")]
[Authorize]
public class SuppliersController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? rating = null, CancellationToken ct = default)
    {
        var query = db.Suppliers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.SupplierName.Contains(search) || s.SupplierCode.Contains(search));
        if (!string.IsNullOrWhiteSpace(rating))
            query = query.Where(s => s.Rating == rating);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(s => s.SupplierCode).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new SupplierDto(s.Id, s.SupplierCode, s.SupplierName, s.ContactPhone, s.MaxDebtDays, s.Rating))
            .ToListAsync(ct);
        return Ok(PagedResult<SupplierDto>.Create(items, total, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var s = await db.Suppliers.FindAsync([id], ct);
        if (s is null) return NotFound();
        return Ok(new SupplierDto(s.Id, s.SupplierCode, s.SupplierName, s.ContactPhone, s.MaxDebtDays, s.Rating));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Procurement")]
    public async Task<IActionResult> Create([FromBody] UpsertSupplierRequest req, CancellationToken ct)
    {
        if (await db.Suppliers.AnyAsync(s => s.SupplierCode == req.SupplierCode, ct))
            return Conflict($"Mã NCC '{req.SupplierCode}' đã tồn tại.");
        var s = new Supplier { SupplierCode = req.SupplierCode, SupplierName = req.SupplierName, ContactPhone = req.ContactPhone, MaxDebtDays = req.MaxDebtDays, Rating = req.Rating };
        db.Suppliers.Add(s);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = s.Id }, null);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Procurement")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertSupplierRequest req, CancellationToken ct)
    {
        var s = await db.Suppliers.FindAsync([id], ct);
        if (s is null) return NotFound();
        s.SupplierName = req.SupplierName; s.ContactPhone = req.ContactPhone;
        s.MaxDebtDays = req.MaxDebtDays; s.Rating = req.Rating; s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var s = await db.Suppliers.FindAsync([id], ct);
        if (s is null) return NotFound();
        s.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
