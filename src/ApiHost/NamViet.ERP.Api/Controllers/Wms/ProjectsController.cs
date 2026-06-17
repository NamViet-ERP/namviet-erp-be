using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProjectDto>>> List(CancellationToken ct)
    {
        var items = await db.Projects.Where(p => p.DeletedAt == null).ToListAsync(ct);
        return Ok(items.Select(WmsMapper.ToProject).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create([FromBody] ProjectDto dto, CancellationToken ct)
    {
        var code = dto.Id.StartsWith("proj-") ? $"DA-{dto.Id[5..].ToUpper()}" : dto.Id;
        var p = new Project
        {
            ProjectCode = code,
            ClientProjectId = dto.Id,
            ProjectName = dto.Name,
            Stage = dto.Stage,
            PmName = dto.Pm,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        db.Projects.Add(p);
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToProject(p));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProjectDto>> Update(string id, [FromBody] ProjectDto dto, CancellationToken ct)
    {
        var p = await FindProjectAsync(id, ct);
        if (p is null) return NotFound();
        p.ProjectName = dto.Name;
        p.Stage = dto.Stage;
        p.PmName = dto.Pm;
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToProject(p));
    }

    [HttpGet("{id}/stock-matrix")]
    public async Task<ActionResult<List<ProjectStockCellDto>>> StockMatrix(string id, CancellationToken ct)
    {
        var p = await FindProjectAsync(id, ct);
        if (p is null) return NotFound();

        var materials = await db.Materials.Where(m => m.DeletedAt == null).ToListAsync(ct);
        var projectInv = await db.ProjectInventories
            .Where(pi => pi.ProjectCode == p.ProjectCode).ToListAsync(ct);
        var generalInv = await db.WarehouseInventories.ToListAsync(ct);

        var result = new List<ProjectStockCellDto>();
        foreach (var m in materials)
        {
            var pi = projectInv.FirstOrDefault(x => x.MaterialCode == m.MaterialCode);
            var generalQty = generalInv.Where(g => g.MaterialCode == m.MaterialCode).Sum(g => g.Qty);
            result.Add(new ProjectStockCellDto(
                m.MaterialCode, "general", generalQty, 0, 0, generalQty));
            result.Add(new ProjectStockCellDto(
                m.MaterialCode, p.ClientProjectId ?? p.ProjectCode,
                pi?.ActualImportQty - pi?.ActualExportQty ?? 0,
                pi?.ActualImportQty ?? 0, pi?.ActualExportQty ?? 0,
                (pi?.ActualImportQty ?? 0) - (pi?.ActualExportQty ?? 0)));
        }
        return Ok(result);
    }

    [HttpGet("{id}/consumable-quotas")]
    public async Task<ActionResult<List<ConsumableQuotaDto>>> GetQuotas(string id, CancellationToken ct)
    {
        var p = await FindProjectAsync(id, ct);
        if (p is null) return NotFound();
        var quotas = await db.ConsumableQuotas.Where(q => q.ProjectCode == p.ProjectCode).ToListAsync(ct);
        return Ok(quotas.Select(q => new ConsumableQuotaDto(
            p.ClientProjectId ?? p.ProjectCode, q.MaterialCode, q.QuotaQty, q.IssuedQty)).ToList());
    }

    [HttpPost("{id}/consumable-quotas")]
    public async Task<ActionResult<ConsumableQuotaDto>> SetQuota(string id, [FromBody] ConsumableQuotaDto dto, CancellationToken ct)
    {
        var p = await FindProjectAsync(id, ct);
        if (p is null) return NotFound();
        var q = await db.ConsumableQuotas
            .FirstOrDefaultAsync(x => x.ProjectCode == p.ProjectCode && x.MaterialCode == dto.ProductId, ct);
        if (q is null)
        {
            q = new ConsumableQuota { ProjectCode = p.ProjectCode, MaterialCode = dto.ProductId, QuotaQty = dto.QuotaQty };
            db.ConsumableQuotas.Add(q);
        }
        else q.QuotaQty = dto.QuotaQty;
        await db.SaveChangesAsync(ct);
        return Ok(new ConsumableQuotaDto(p.ClientProjectId ?? p.ProjectCode, q.MaterialCode, q.QuotaQty, q.IssuedQty));
    }

    [HttpPost("{id}/consumable-issue")]
    public async Task<ActionResult<ConsumableQuotaDto>> Issue(string id, [FromBody] ConsumableQuotaDto dto, CancellationToken ct)
    {
        var p = await FindProjectAsync(id, ct);
        if (p is null) return NotFound();
        var q = await db.ConsumableQuotas
            .FirstOrDefaultAsync(x => x.ProjectCode == p.ProjectCode && x.MaterialCode == dto.ProductId, ct);
        if (q is null) return NotFound("Quota not found");
        q.IssuedQty += dto.IssuedQty;
        await db.SaveChangesAsync(ct);
        return Ok(new ConsumableQuotaDto(p.ClientProjectId ?? p.ProjectCode, q.MaterialCode, q.QuotaQty, q.IssuedQty));
    }

    [HttpGet("{id}/bom")]
    public async Task<ActionResult<Dictionary<string, decimal>>> Bom(string id, CancellationToken ct)
    {
        var p = await FindProjectAsync(id, ct);
        if (p is null) return NotFound();
        var boms = await db.ProjectBoms.Where(b => b.ProjectCode == p.ProjectCode).ToListAsync(ct);
        return Ok(boms.ToDictionary(b => b.MaterialCode, b => b.Qty));
    }

    [HttpGet("{id}/requirements")]
    public async Task<ActionResult<List<object>>> Requirements(string id, CancellationToken ct)
    {
        var p = await FindProjectAsync(id, ct);
        if (p is null) return NotFound();
        var boms = await db.ProjectBoms
            .Where(b => b.ProjectCode == p.ProjectCode)
            .Join(db.Materials, b => b.MaterialCode, m => m.MaterialCode, (b, m) => new { m.MaterialCode, m.MaterialName, needed = b.Qty })
            .ToListAsync(ct);
        return Ok(boms.Select(b => new { sku = b.MaterialCode, name = b.MaterialName, needed = b.needed }).ToList());
    }

    private async Task<Project?> FindProjectAsync(string id, CancellationToken ct) =>
        await db.Projects.FirstOrDefaultAsync(p =>
            (p.ClientProjectId == id || p.ProjectCode == id) && p.DeletedAt == null, ct);
}

[ApiController]
[Route("api/lending-logs")]
[Authorize]
public class LendingLogsController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<LendingLogDto>>> List(CancellationToken ct)
    {
        var items = await db.ProjectMaterialLoans.Include(l => l.Material).ToListAsync(ct);
        return Ok(items.Select(WmsMapper.ToLendingLog).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<LendingLogDto>> Create([FromBody] LendingLogDto dto, CancellationToken ct)
    {
        var count = await db.ProjectMaterialLoans.CountAsync(ct);
        var l = new ProjectMaterialLoan
        {
            LoanCode = string.IsNullOrWhiteSpace(dto.VoucherCode) ? $"MT-{count + 1:D6}" : dto.VoucherCode,
            BorrowingProjectCode = dto.BorrowingProjectId,
            LendingProjectCode = dto.LendingProjectId,
            MaterialCode = dto.ProductId,
            LoanQty = dto.Qty,
            LoanReason = dto.Note,
            CreatedByName = dto.CreatedBy,
            Status = LoanStatus.Borrowing
        };
        db.ProjectMaterialLoans.Add(l);
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToLendingLog(l));
    }

    [HttpPatch("{id}/sign")]
    public async Task<ActionResult<LendingLogDto>> Sign(string id, [FromBody] object body, CancellationToken ct)
    {
        var l = await db.ProjectMaterialLoans.FirstOrDefaultAsync(x => x.LoanCode == id, ct);
        if (l is null) return NotFound();
        var json = System.Text.Json.JsonSerializer.Serialize(body);
        if (json.Contains("borrow")) l.BorrowPmSigned = true;
        if (json.Contains("lend")) l.LendPmSigned = true;
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToLendingLog(l));
    }

    [HttpPatch("{id}/return")]
    public async Task<ActionResult<LendingLogDto>> Return(string id, CancellationToken ct)
    {
        var l = await db.ProjectMaterialLoans.FirstOrDefaultAsync(x => x.LoanCode == id, ct);
        if (l is null) return NotFound();
        l.Status = LoanStatus.Returned;
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToLendingLog(l));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var l = await db.ProjectMaterialLoans.FirstOrDefaultAsync(x => x.LoanCode == id, ct);
        if (l is null) return NotFound();
        db.ProjectMaterialLoans.Remove(l);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
