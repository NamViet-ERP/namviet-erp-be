using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using System.Text.Json;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/projects")]
[Authorize]
public class ProjectsController(
    WarehouseDbContext db,
    IInventoryDomainService inventory,
    IProjectBudgetService projectBudget,
    IValidator<CreateProjectRequest> createValidator,
    IValidator<UpdateProjectRequest> updateValidator) : ControllerBase
{
    private static IReadOnlyList<ProjectWarehouseKeeperDto> NormalizeKeepers(
        IReadOnlyList<ProjectWarehouseKeeperDto>? keepers) =>
        (keepers ?? [])
            .Where(k => k.UserId != Guid.Empty)
            .GroupBy(k => k.UserId)
            .Select(g =>
            {
                var first = g.First();
                var name = string.IsNullOrWhiteSpace(first.DisplayName)
                    ? first.UserId.ToString()
                    : first.DisplayName.Trim();
                return new ProjectWarehouseKeeperDto(g.Key, name);
            })
            .ToList();

    private async Task ReplaceKeepersAsync(
        string projectCode,
        IReadOnlyList<ProjectWarehouseKeeperDto>? keepers,
        CancellationToken ct)
    {
        var normalized = NormalizeKeepers(keepers);
        var existing = await db.ProjectWarehouseKeepers
            .Where(k => k.ProjectCode == projectCode)
            .ToListAsync(ct);
        db.ProjectWarehouseKeepers.RemoveRange(existing);
        if (normalized.Count == 0) return;

        db.ProjectWarehouseKeepers.AddRange(normalized.Select(k => new ProjectWarehouseKeeper
        {
            ProjectCode = projectCode,
            UserId = k.UserId,
            DisplayName = k.DisplayName,
        }));
    }

    [HttpGet]
    [RequirePermission("wms.projects.view:all")]
    public async Task<ActionResult<PagedResult<ProjectDto>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
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
    [RequirePermission("wms.projects.view:all")]
    public async Task<ActionResult<ProjectDto>> GetById(string code, CancellationToken ct)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.ProjectCode == code, ct);
        if (p is null) return NotFound();
        var items = await MapProjectsWithStockAsync([p], ct);
        return Ok(items[0]);
    }

    [HttpGet("{code}/inventory")]
    [RequirePermission("wms.projects.view:all")]
    public async Task<ActionResult<List<ProjectInventoryDto>>> GetInventory(string code, CancellationToken ct)
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
                (pi.ActualImportQty - pi.ActualExportQty) + pi.PlannedImportQty,
                pi.ReservedQty,
                pi.MinStockQty))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPatch("{code}/inventory/{materialCode}/min-stock")]
    [RequirePermission("wms.projects.update")]
    public async Task<IActionResult> UpdateInventoryMinStock(
        string code, string materialCode,
        [FromBody] UpdateProjectInventoryMinStockRequest request,
        CancellationToken ct)
    {
        if (request.MinStockQty < 0)
            return BadRequest("Tồn tối thiểu không được âm.");

        var row = await db.ProjectInventories
            .FirstOrDefaultAsync(pi => pi.ProjectCode == code && pi.MaterialCode == materialCode, ct);
        if (row is null) return NotFound();

        row.MinStockQty = request.MinStockQty;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{code}/ledger")]
    [RequirePermission("wms.projects.view:all")]
    public async Task<ActionResult<List<ProjectLedgerEntryDto>>> GetLedger(string code, CancellationToken ct)
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
    [RequirePermission("wms.projects.update")]
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
    [RequirePermission("wms.projects.create")]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationBadRequest(validation);

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
            MaterialBudget = request.MaterialBudget,
            ClientName = NormalizeOptional(request.ClientName),
            ScMembers = NormalizeOptional(request.ScMembers),
        };
        db.Projects.Add(project);
        await ReplaceKeepersAsync(project.ProjectCode, request.WarehouseKeepers, ct);
        await db.SaveChangesAsync(ct);
        var items = await MapProjectsWithStockAsync([project], ct);
        return CreatedAtAction(nameof(GetById), new { code = project.ProjectCode }, items[0]);
    }

    [HttpPut("{code}")]
    [RequirePermission("wms.projects.update")]
    public async Task<IActionResult> Update(string code, [FromBody] UpdateProjectRequest request, CancellationToken ct)
    {
        var validation = await updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationBadRequest(validation);

        var p = await db.Projects.FirstOrDefaultAsync(x => x.ProjectCode == code, ct);
        if (p is null) return NotFound();
        p.ProjectName = request.ProjectName;
        p.PmId = request.PmId;
        p.PmName = request.PmName;
        p.EndDate = request.EndDate;
        p.MaterialBudget = request.MaterialBudget;
        p.Status = request.Status;
        p.ClientName = NormalizeOptional(request.ClientName);
        p.ScMembers = NormalizeOptional(request.ScMembers);
        p.UpdatedAt = DateTime.UtcNow;
        await ReplaceKeepersAsync(code, request.WarehouseKeepers, ct);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<List<ProjectDto>> MapProjectsWithStockAsync(
        List<Project> projects, CancellationToken ct)
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
                StockPlannedQty = g.Sum(pi =>
                    (pi.ActualImportQty - pi.ActualExportQty) + pi.PlannedImportQty),
                StockReservedQty = g.Sum(pi => pi.ReservedQty),
                StockMaterialCount = g.Count(pi =>
                    pi.ActualImportQty - pi.ActualExportQty != 0
                    || pi.PlannedImportQty != 0
                    || pi.PlannedExportQty != 0
                    || pi.ReservedQty != 0),
            })
            .ToListAsync(ct);

        var stockByCode = stockRows.ToDictionary(x => x.ProjectCode);
        var approvedByCode = await projectBudget.GetApprovedImportValuesAsync(codes, ct);

        var keepers = await db.ProjectWarehouseKeepers.AsNoTracking()
            .Where(k => codes.Contains(k.ProjectCode))
            .OrderBy(k => k.DisplayName)
            .ToListAsync(ct);
        var keepersByCode = keepers
            .GroupBy(k => k.ProjectCode)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ProjectWarehouseKeeperDto>)g
                    .Select(k => new ProjectWarehouseKeeperDto(k.UserId, k.DisplayName))
                    .ToList());

        return projects.Select(p =>
        {
            stockByCode.TryGetValue(p.ProjectCode, out var stock);
            approvedByCode.TryGetValue(p.ProjectCode, out var approvedImportValue);
            keepersByCode.TryGetValue(p.ProjectCode, out var projectKeepers);
            var remaining = p.MaterialBudget - approvedImportValue;
            return new ProjectDto(
                p.ProjectCode, p.ProjectName, p.ProjectType.ToString(),
                p.PmId, p.PmName, p.StartDate, p.EndDate, p.MaterialBudget, p.Status.ToString(),
                p.ClientName, p.ScMembers,
                stock?.StockActualQty ?? 0,
                stock?.StockPlannedQty ?? 0,
                stock?.StockReservedQty ?? 0,
                stock?.StockMaterialCount ?? 0,
                approvedImportValue,
                remaining,
                projectKeepers ?? []);
        }).ToList();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    private static BadRequestObjectResult ValidationBadRequest(FluentValidation.Results.ValidationResult validation)
    {
        var errors = validation.Errors
            .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return new BadRequestObjectResult(new { errors });
    }
}
