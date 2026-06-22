using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/projects")]
[Authorize]
public class ProjectsController(WarehouseDbContext db, IInventoryDomainService inventory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var query = db.Projects.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.ProjectName.Contains(search) || p.ProjectCode.Contains(search));
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ProjectStatus>(status, out var s))
            query = query.Where(p => p.Status == s);

        var total = await query.CountAsync(ct);
        var pageProjects = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var items = await MapProjectsWithStockAsync(pageProjects, ct);

        return Ok(PagedResult<ProjectDto>.Create(items, total, page, pageSize));
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetById(string code, CancellationToken ct)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.ProjectCode == code, ct);
        if (p is null) return NotFound();
        var items = await MapProjectsWithStockAsync([p], ct);
        return Ok(items[0]);
    }

    [HttpGet("{code}/inventory")]
    public async Task<IActionResult> GetInventory(string code, CancellationToken ct)
    {
        var items = await db.ProjectInventories
            .Include(pi => pi.Material)
            .Where(pi => pi.ProjectCode == code)
            .Select(pi => new ProjectInventoryDto(
                pi.ProjectCode, pi.MaterialCode,
                pi.Material != null ? pi.Material.MaterialName : pi.MaterialCode,
                pi.Material != null ? pi.Material.Unit : string.Empty,
                pi.PlannedImportQty, pi.PlannedExportQty,
                pi.ActualImportQty, pi.ActualExportQty,
                pi.ActualImportQty - pi.ActualExportQty,
                pi.PlannedImportQty - pi.PlannedExportQty,
                pi.ReservedQty))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{code}/ledger")]
    public async Task<IActionResult> GetLedger(string code, CancellationToken ct)
    {
        if (!await db.Projects.AnyAsync(p => p.ProjectCode == code, ct))
            return NotFound();

        var transactions = await db.WarehouseTransactions
            .AsNoTracking()
            .Include(t => t.Items)
            .Where(t =>
                (t.ProjectCode == code || t.BeneficiaryProjectCode == code)
                && (t.TransactionType == TransactionType.Import
                    || t.TransactionType == TransactionType.Export
                    || t.TransactionType == TransactionType.Transfer))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var entries = transactions.Select(MapLedgerEntry).ToList();
        return Ok(entries);
    }

    [HttpPost("{code}/close")]
    [Authorize(Roles = "Admin,Warehouse_Keeper,Storekeeper,PM")]
    public async Task<IActionResult> CloseProject(string code, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == code, ct);
        if (project is null) return NotFound();
        if (project.Status == ProjectStatus.Closed)
            return BadRequest("Dự án đã đóng kho.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await inventory.ApplyProjectCloseTransferAsync(code, ct);
        project.Status = ProjectStatus.Closed;
        project.EndDate = DateOnly.FromDateTime(DateTime.UtcNow);
        project.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { projectCode = code, status = project.Status.ToString() });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,PM")]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        if (await db.Projects.AnyAsync(p => p.ProjectCode == request.ProjectCode, ct))
            return Conflict($"Mã dự án '{request.ProjectCode}' đã tồn tại.");

        var project = new Project
        {
            ProjectCode = request.ProjectCode,
            ProjectName = request.ProjectName,
            ProjectType = request.ProjectType,
            PmId = request.PmId,
            PmName = request.PmName,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MaterialBudget = request.MaterialBudget
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { code = project.ProjectCode }, null);
    }

    [HttpPut("{code}")]
    [Authorize(Roles = "Admin,PM")]
    public async Task<IActionResult> Update(string code, [FromBody] UpdateProjectRequest request, CancellationToken ct)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.ProjectCode == code, ct);
        if (p is null) return NotFound();
        p.ProjectName = request.ProjectName;
        p.PmId = request.PmId;
        p.PmName = request.PmName;
        p.EndDate = request.EndDate;
        p.MaterialBudget = request.MaterialBudget;
        p.Status = request.Status;
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<List<ProjectDto>> MapProjectsWithStockAsync(
        IReadOnlyList<Project> projects, CancellationToken ct)
    {
        if (projects.Count == 0) return [];

        var codes = projects.Select(p => p.ProjectCode).ToList();
        var stockRows = await db.ProjectInventories
            .Where(pi => codes.Contains(pi.ProjectCode))
            .GroupBy(pi => pi.ProjectCode)
            .Select(g => new
            {
                ProjectCode = g.Key,
                StockActualQty = g.Sum(pi => pi.ActualImportQty - pi.ActualExportQty),
                StockPlannedQty = g.Sum(pi => pi.PlannedImportQty - pi.PlannedExportQty),
                StockReservedQty = g.Sum(pi => pi.ReservedQty),
                StockMaterialCount = g.Count(pi =>
                    pi.ActualImportQty - pi.ActualExportQty != 0
                    || pi.PlannedImportQty - pi.PlannedExportQty != 0
                    || pi.ReservedQty != 0),
            })
            .ToListAsync(ct);

        var stockByCode = stockRows.ToDictionary(x => x.ProjectCode);

        return projects.Select(p =>
        {
            stockByCode.TryGetValue(p.ProjectCode, out var stock);
            return new ProjectDto(
                p.ProjectCode, p.ProjectName, p.ProjectType.ToString(),
                p.PmId, p.PmName, p.StartDate, p.EndDate, p.MaterialBudget, p.Status.ToString(),
                stock?.StockActualQty ?? 0,
                stock?.StockPlannedQty ?? 0,
                stock?.StockReservedQty ?? 0,
                stock?.StockMaterialCount ?? 0);
        }).ToList();
    }

    private static ProjectLedgerEntryDto MapLedgerEntry(WarehouseTransaction t)
    {
        var itemQty = t.Items.Sum(i => i.Quantity);
        decimal? totalValue = t.TransactionType == TransactionType.Import
            ? t.Items.Sum(i => i.Quantity * i.UnitPrice)
            : null;

        return new ProjectLedgerEntryDto(
            t.Id.ToString(),
            t.TransactionType switch
            {
                TransactionType.Import => "import",
                TransactionType.Export => "export",
                TransactionType.Transfer => "transfer",
                _ => "other"
            },
            t.TicketCode,
            WmsMapper.FormatDate(t.CreatedAt),
            WmsMapper.ToClientApprovalStatus(t.ApprovalStatus),
            itemQty,
            totalValue);
    }
}
