using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Purchasing;

public record ServiceExpenseItemDto(
    int Id,
    string Code,
    string Name,
    string Type,
    string Unit,
    decimal? ReferencePrice,
    string? Description,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateServiceExpenseItemRequest(
    string Code,
    string Name,
    string Type,
    string Unit,
    decimal? ReferencePrice,
    string? Description,
    bool IsActive = true,
    int SortOrder = 0);

public record UpdateServiceExpenseItemRequest(
    string Code,
    string Name,
    string Type,
    string Unit,
    decimal? ReferencePrice,
    string? Description,
    bool IsActive,
    int SortOrder);

public record ServiceExpenseItemPagedResponse(
    IReadOnlyList<ServiceExpenseItemDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

[ApiController]
[Route("api/purchasing/service-items")]
[Authorize]
public class ServiceExpenseItemsController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ServiceExpenseItemPagedResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? type = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool all = false,
        CancellationToken ct = default)
    {
        var query = db.ServiceExpenseItems
            .AsNoTracking()
            .Where(s => s.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.Code, pattern) ||
                EF.Functions.ILike(s.Name, pattern) ||
                (s.Description != null && EF.Functions.ILike(s.Description, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normType = type.Trim().ToLowerInvariant();
            query = query.Where(s => s.Type == normType);
        }

        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        query = query.OrderBy(s => s.SortOrder).ThenBy(s => s.Code);

        var total = await query.CountAsync(ct);

        List<ServiceExpenseItem> entities;
        if (all)
        {
            entities = await query.ToListAsync(ct);
            page = 1;
            pageSize = total > 0 ? total : 1;
        }
        else
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);
            entities = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        var dtos = entities.Select(s => new ServiceExpenseItemDto(
            s.Id,
            s.Code,
            s.Name,
            s.Type,
            s.Unit,
            s.ReferencePrice,
            s.Description,
            s.IsActive,
            s.SortOrder,
            s.CreatedAt,
            s.UpdatedAt)).ToList();

        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)total / pageSize) : 0;

        return Ok(new ServiceExpenseItemPagedResponse(dtos, total, page, pageSize, totalPages));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ServiceExpenseItemDto>> GetById(int id, CancellationToken ct)
    {
        var item = await db.ServiceExpenseItems
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);

        if (item is null) return NotFound("Không tìm thấy dịch vụ/chi phí.");

        return Ok(new ServiceExpenseItemDto(
            item.Id,
            item.Code,
            item.Name,
            item.Type,
            item.Unit,
            item.ReferencePrice,
            item.Description,
            item.IsActive,
            item.SortOrder,
            item.CreatedAt,
            item.UpdatedAt));
    }

    [HttpPost]
    public async Task<ActionResult<ServiceExpenseItemDto>> Create(
        [FromBody] CreateServiceExpenseItemRequest request,
        CancellationToken ct)
    {
        var code = request.Code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Mã dịch vụ / chi phí là bắt buộc.");

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Tên dịch vụ / chi phí là bắt buộc.");

        var type = request.Type?.Trim().ToLowerInvariant();
        if (type != "service" && type != "expense")
            type = "service";

        var unit = request.Unit?.Trim();
        if (string.IsNullOrWhiteSpace(unit))
            return BadRequest("Đơn vị tính là bắt buộc.");

        var exists = await db.ServiceExpenseItems
            .AnyAsync(s => s.Code == code && s.DeletedAt == null, ct);
        if (exists)
            return Conflict($"Mã '{code}' đã tồn tại trong danh mục dịch vụ & chi phí.");

        var now = DateTime.UtcNow;
        var entity = new ServiceExpenseItem
        {
            Code = code,
            Name = name,
            Type = type,
            Unit = unit,
            ReferencePrice = request.ReferencePrice >= 0 ? request.ReferencePrice : null,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.ServiceExpenseItems.Add(entity);
        await db.SaveChangesAsync(ct);

        var dto = new ServiceExpenseItemDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Type,
            entity.Unit,
            entity.ReferencePrice,
            entity.Description,
            entity.IsActive,
            entity.SortOrder,
            entity.CreatedAt,
            entity.UpdatedAt);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ServiceExpenseItemDto>> Update(
        int id,
        [FromBody] UpdateServiceExpenseItemRequest request,
        CancellationToken ct)
    {
        var entity = await db.ServiceExpenseItems
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);

        if (entity is null) return NotFound("Không tìm thấy dịch vụ/chi phí.");

        var code = request.Code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Mã dịch vụ / chi phí là bắt buộc.");

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Tên dịch vụ / chi phí là bắt buộc.");

        var type = request.Type?.Trim().ToLowerInvariant();
        if (type != "service" && type != "expense")
            type = "service";

        var unit = request.Unit?.Trim();
        if (string.IsNullOrWhiteSpace(unit))
            return BadRequest("Đơn vị tính là bắt buộc.");

        var exists = await db.ServiceExpenseItems
            .AnyAsync(s => s.Id != id && s.Code == code && s.DeletedAt == null, ct);
        if (exists)
            return Conflict($"Mã '{code}' đã được sử dụng bởi dịch vụ/chi phí khác.");

        entity.Code = code;
        entity.Name = name;
        entity.Type = type;
        entity.Unit = unit;
        entity.ReferencePrice = request.ReferencePrice >= 0 ? request.ReferencePrice : null;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.IsActive = request.IsActive;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new ServiceExpenseItemDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Type,
            entity.Unit,
            entity.ReferencePrice,
            entity.Description,
            entity.IsActive,
            entity.SortOrder,
            entity.CreatedAt,
            entity.UpdatedAt));
    }

    [HttpPatch("{id:int}/toggle-active")]
    public async Task<ActionResult<ServiceExpenseItemDto>> ToggleActive(int id, CancellationToken ct)
    {
        var entity = await db.ServiceExpenseItems
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);

        if (entity is null) return NotFound("Không tìm thấy dịch vụ/chi phí.");

        entity.IsActive = !entity.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new ServiceExpenseItemDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Type,
            entity.Unit,
            entity.ReferencePrice,
            entity.Description,
            entity.IsActive,
            entity.SortOrder,
            entity.CreatedAt,
            entity.UpdatedAt));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await db.ServiceExpenseItems
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);

        if (entity is null) return NotFound("Không tìm thấy dịch vụ/chi phí.");

        var isReferenced = await db.PurchasePlanLines
            .AnyAsync(l => l.MaterialCode == entity.Code, ct);

        if (isReferenced)
        {
            return Conflict(
                $"Mã '{entity.Code}' đang được sử dụng trong Kế hoạch mua hàng, không thể xóa. Bạn có thể tắt kích hoạt thay vì xóa.");
        }

        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }
}
