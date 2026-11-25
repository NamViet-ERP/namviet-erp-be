using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Api.Infrastructure.RateLimiting;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Application.Helpers;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/material-units")]
[Authorize]
public class MaterialUnitsController(WarehouseDbContext db, IMaterialUnitImportExcelService? excelService = null) : ControllerBase
{
    private IMaterialUnitImportExcelService ExcelService => excelService ?? new MaterialUnitImportExcelService(db);
    [HttpGet]
    [RequirePermission("wms.material_units.view:all")]
    public async Task<ActionResult<List<MaterialUnitDto>>> List(
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        var query = db.MaterialUnits.AsNoTracking().AsQueryable();
        if (activeOnly)
            query = query.Where(u => u.IsActive);

        var items = await query
            .OrderBy(u => u.Name)
            .Select(u => new MaterialUnitDto(u.Id, u.Name, u.SortOrder, u.IsActive))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [RequirePermission("wms.material_units.create")]
    public async Task<ActionResult<MaterialUnitDto>> Create(
        [FromBody] UpsertMaterialUnitRequest request,
        CancellationToken ct)
    {
        var name = MaterialUnitHelper.NormalizeDisplayName(request.Name);
        if (string.IsNullOrEmpty(name))
            return BadRequest("Vui lòng nhập tên đơn vị tính.");

        if (await db.MaterialUnits.AnyAsync(u => u.Name == name, ct))
            return Conflict($"Đơn vị tính '{name}' đã tồn tại.");

        var sortOrder = request.SortOrder;
        if (sortOrder <= 0)
            sortOrder = await db.MaterialUnits.CountAsync(ct) + 1;

        var entity = new MaterialUnit
        {
            Name = name,
            SortOrder = sortOrder,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
        };
        db.MaterialUnits.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(new MaterialUnitDto(entity.Id, entity.Name, entity.SortOrder, entity.IsActive));
    }

    [HttpPut("{id:int}")]
    [RequirePermission("wms.material_units.update")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertMaterialUnitRequest request, CancellationToken ct)
    {
        var entity = await db.MaterialUnits.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (entity is null) return NotFound();

        var name = MaterialUnitHelper.NormalizeDisplayName(request.Name);
        if (string.IsNullOrEmpty(name))
            return BadRequest("Vui lòng nhập tên đơn vị tính.");

        if (await db.MaterialUnits.AnyAsync(u => u.Id != id && u.Name == name, ct))
            return Conflict($"Đơn vị tính '{name}' đã tồn tại.");

        var oldName = entity.Name;
        entity.Name = name;
        if (request.SortOrder > 0)
            entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        if (!string.Equals(oldName, name, StringComparison.Ordinal))
        {
            var materials = await db.Materials
                .Where(m => m.Unit == oldName)
                .ToListAsync(ct);
            foreach (var material in materials)
                material.Unit = name;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new MaterialUnitDto(entity.Id, entity.Name, entity.SortOrder, entity.IsActive));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("wms.material_units.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await db.MaterialUnits.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (entity is null) return NotFound();

        if (await db.Materials.AnyAsync(m => m.Unit == entity.Name && m.DeletedAt == null, ct))
            return BadRequest("Có vật tư đang dùng đơn vị tính này.");

        db.MaterialUnits.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("import-template")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicRead)]
    public async Task<IActionResult> DownloadImportTemplate(CancellationToken ct)
    {
        var bytes = await ExcelService.GenerateTemplateAsync(ct);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "mau-import-don-vi-tinh.xlsx");
    }

    [HttpPost("parse-import-file")]
    [RequestSizeLimit(5 * 1024 * 1024 + 1024)]
    [RequirePermission("wms.material_units.create")]
    public async Task<ActionResult<MaterialUnitImportParseResultDto>> ParseImportFile(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Vui lòng chọn file import.");
        if (file.Length > 5 * 1024 * 1024)
            return BadRequest("Kích thước file vượt quá 5MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".xlsx" or ".xls" or ".csv"))
            return BadRequest("Chỉ chấp nhận file .xlsx, .xls hoặc .csv.");

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await ExcelService.ParseImportFileAsync(stream, file.FileName, ct);
            return Ok(result);
        }
        catch (DomainRuleException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest($"Không thể đọc dữ liệu file: {ex.Message}");
        }
    }

    [HttpPost("import-batch")]
    [RequirePermission("wms.material_units.create")]
    public async Task<ActionResult<MaterialUnitImportResultDto>> ImportBatch(
        [FromBody] MaterialUnitImportBatchRequest request,
        CancellationToken ct)
    {
        if (request is null || request.Rows.Count == 0)
            return BadRequest("Không có dữ liệu đơn vị tính để import.");

        try
        {
            var result = await ExcelService.ImportBatchAsync(request, ct);
            return Ok(result);
        }
        catch (DomainRuleException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest($"Lỗi trong quá trình import: {ex.Message}");
        }
    }
}
