using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/suppliers")]
[Authorize]
public class SuppliersController(WarehouseDbContext db, ISupplierGroupService groups) : ControllerBase
{
    [HttpGet("{id:int}/groups")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<IReadOnlyList<SupplierGroupRefDto>>> GetGroups(
        int id, CancellationToken ct) =>
        Ok(await groups.ListGroupsOfSupplierAsync(id, ct));

    [HttpPut("{id:int}/groups")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<IReadOnlyList<SupplierGroupRefDto>>> SetGroups(
        int id, [FromBody] SetSupplierGroupsRequest request, CancellationToken ct) =>
        Ok(await groups.SetGroupsOfSupplierAsync(id, request, ct));


    [HttpGet]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<PagedResult<SupplierDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? rating = null, CancellationToken ct = default)
    {
        var query = db.Suppliers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.SupplierName.Contains(search) || s.SupplierCode.Contains(search));
        if (!string.IsNullOrWhiteSpace(rating))
            query = query.Where(s => s.Rating == rating);
        var total = await query.CountAsync(ct);
        var suppliers = await query
            .OrderBy(s => s.SupplierCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.SupplierCode,
                s.SupplierName,
                s.ContactPhone,
                s.MaxDebtDays,
                s.PaymentTerms,
                s.Rating,
            })
            .ToListAsync(ct);

        var supplierIds = suppliers.Select(s => s.Id).ToArray();
        var industryGroups = await db.SupplierGroupMembers
            .AsNoTracking()
            .Where(m => supplierIds.Contains(m.SupplierId))
            .Select(m => new { m.SupplierId, GroupName = m.Group!.Name })
            .ToListAsync(ct);
        var industryGroupMap = industryGroups
            .GroupBy(x => x.SupplierId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g
                    .Select(x => x.GroupName)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList());

        var revenueRows = await db.PurchaseOrders
            .AsNoTracking()
            .Where(o => o.SupplierId != null
                        && supplierIds.Contains(o.SupplierId.Value)
                        && o.DeliveryStatus == PurchaseOrderDeliveryStatuses.Complete)
            .GroupBy(o => o.SupplierId!.Value)
            .Select(g => new { SupplierId = g.Key, PurchasedRevenue = g.Sum(o => o.TotalAmount) })
            .ToListAsync(ct);
        var revenueMap = revenueRows.ToDictionary(x => x.SupplierId, x => Math.Round(x.PurchasedRevenue, 2));

        var latestEvaluations = await db.SupplierEvaluations
            .AsNoTracking()
            .Where(v => supplierIds.Contains(v.SupplierId))
            .GroupBy(v => v.SupplierId)
            .Select(g => g
                .OrderByDescending(v => v.EvaluatedAt)
                .ThenByDescending(v => v.Id)
                .Select(v => new
                {
                    v.SupplierId,
                    v.Period,
                    v.Score,
                    v.Grade,
                    v.EvaluatedAt,
                    v.OnTimeDeliveryRate,
                    v.DefectRate,
                    v.WarrantyCompletionRate,
                })
                .FirstOrDefault()!)
            .ToListAsync(ct);
        var latestEvaluationMap = latestEvaluations
            .Where(x => x != null)
            .ToDictionary(
                x => x.SupplierId,
                x => (SupplierLatestEvaluationDto?)new SupplierLatestEvaluationDto(
                    x.Period,
                    x.Score,
                    x.Grade,
                    x.EvaluatedAt,
                    x.OnTimeDeliveryRate,
                    x.DefectRate,
                    x.WarrantyCompletionRate));

        var items = suppliers.Select(s => new SupplierDto(
            s.Id,
            s.SupplierCode,
            s.SupplierName,
            s.ContactPhone,
            s.MaxDebtDays,
            s.PaymentTerms,
            s.Rating,
            revenueMap.GetValueOrDefault(s.Id, 0m),
            industryGroupMap.GetValueOrDefault(s.Id, Array.Empty<string>()),
            latestEvaluationMap.GetValueOrDefault(s.Id))).ToList();
        return Ok(PagedResult<SupplierDto>.Create(items, total, page, pageSize));
    }

    [HttpGet("{id:int}")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<SupplierDto>> GetById(int id, CancellationToken ct)
    {
        var s = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null)
            return NotFound();

        var groups = await db.SupplierGroupMembers.AsNoTracking()
            .Where(m => m.SupplierId == id)
            .Select(m => m.Group!.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);

        var purchasedRevenue = await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.SupplierId == id && o.DeliveryStatus == PurchaseOrderDeliveryStatuses.Complete)
            .SumAsync(o => o.TotalAmount, ct);

        var latestEvaluation = await db.SupplierEvaluations.AsNoTracking()
            .Where(v => v.SupplierId == id)
            .OrderByDescending(v => v.EvaluatedAt)
            .ThenByDescending(v => v.Id)
            .Select(v => new SupplierLatestEvaluationDto(
                v.Period,
                v.Score,
                v.Grade,
                v.EvaluatedAt,
                v.OnTimeDeliveryRate,
                v.DefectRate,
                v.WarrantyCompletionRate))
            .FirstOrDefaultAsync(ct);

        return Ok(new SupplierDto(
            s.Id,
            s.SupplierCode,
            s.SupplierName,
            s.ContactPhone,
            s.MaxDebtDays,
            s.PaymentTerms,
            s.Rating,
            Math.Round(purchasedRevenue, 2),
            groups,
            latestEvaluation));
    }

    [HttpPost]
    [RequirePermission("wms.suppliers.create")]
    public async Task<IActionResult> Create([FromBody] UpsertSupplierRequest req, CancellationToken ct)
    {
        if (await db.Suppliers.AnyAsync(s => s.SupplierCode == req.SupplierCode, ct))
            return Conflict($"Mã NCC '{req.SupplierCode}' đã tồn tại.");
        var s = new Supplier
        {
            SupplierCode = req.SupplierCode,
            SupplierName = req.SupplierName,
            ContactPhone = req.ContactPhone,
            MaxDebtDays = req.MaxDebtDays,
            PaymentTerms = string.IsNullOrWhiteSpace(req.PaymentTerms) ? null : req.PaymentTerms.Trim(),
            Rating = req.Rating,
        };
        db.Suppliers.Add(s);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = s.Id }, null);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertSupplierRequest req, CancellationToken ct)
    {
        var s = await db.Suppliers.FindAsync([id], ct);
        if (s is null) return NotFound();
        s.SupplierName = req.SupplierName; s.ContactPhone = req.ContactPhone;
        s.MaxDebtDays = req.MaxDebtDays;
        s.PaymentTerms = string.IsNullOrWhiteSpace(req.PaymentTerms) ? null : req.PaymentTerms.Trim();
        s.Rating = req.Rating;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("wms.suppliers.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var s = await db.Suppliers.FindAsync([id], ct);
        if (s is null) return NotFound();
        s.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
