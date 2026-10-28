using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Application.Helpers;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/materials")]
[Authorize]
public class MaterialsController(WarehouseDbContext db, IMaterialImportExcelService materialExcel) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.materials.view:all")]
    public async Task<ActionResult<PagedResult<MaterialDto>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? danhMuc = null,
        [FromQuery] string? nhomVatTu = null,
        [FromQuery] string? loaiVatTu = null,
        [FromQuery] string? phanLoai = null,
        [FromQuery] string? kieu = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var hasPathFilter = !string.IsNullOrWhiteSpace(danhMuc) || !string.IsNullOrWhiteSpace(nhomVatTu) ||
            !string.IsNullOrWhiteSpace(loaiVatTu) || !string.IsNullOrWhiteSpace(phanLoai) ||
            !string.IsNullOrWhiteSpace(kieu);

        List<MaterialCategory>? allCategories = null;
        if (hasPathFilter || categoryId.HasValue)
            allCategories = await db.MaterialCategories.AsNoTracking().ToListAsync(ct);

        var query = db.Materials.AsNoTracking().Where(m => m.DeletedAt == null);
        if (!includeInactive)
            query = query.Where(m => m.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.MaterialName.Contains(search) || m.MaterialCode.Contains(search));

        var matchingIds = hasPathFilter
            ? MaterialCategoryHelper.CollectCategoryIdsMatchingPathFilter(
                allCategories!, danhMuc, nhomVatTu, loaiVatTu, phanLoai, kieu)
            : [];
        if (hasPathFilter)
        {
            if (matchingIds.Count == 0)
                return Ok(PagedResult<MaterialDto>.Create([], 0, page, pageSize));
            query = query.Where(m => matchingIds.Contains(m.CategoryId));
        }

        if (categoryId.HasValue)
        {
            var subtree = MaterialCategoryHelper.CollectSelfAndDescendantIds(categoryId.Value, allCategories!);
            query = query.Where(m => subtree.Contains(m.CategoryId));
        }

        var total = await query.CountAsync(ct);
        var materials = await query
            .OrderBy(m => m.MaterialCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var categoryById = allCategories is not null
            ? allCategories.ToDictionary(c => c.Id)
            : await LoadCategoryLookupAsync(materials.Select(m => m.CategoryId), ct);
        var items = materials.Select(m => ToDto(m, categoryById)).ToList();
        return Ok(PagedResult<MaterialDto>.Create(items, total, page, pageSize));
    }

    [HttpGet("{code}")]
    [RequirePermission("wms.materials.view:all")]
    public async Task<ActionResult<MaterialDto>> GetById(string code, CancellationToken ct)
    {
        var m = await db.Materials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaterialCode == code && x.DeletedAt == null, ct);
        if (m is null) return NotFound();

        var categoryById = await LoadCategoryLookupAsync([m.CategoryId], ct);
        return Ok(ToDto(m, categoryById));
    }

    [HttpPost]
    [RequirePermission("wms.materials.create")]
    public async Task<IActionResult> Create([FromBody] UpsertMaterialRequest request, CancellationToken ct)
    {
        if (await db.Materials.IgnoreQueryFilters()
                .AnyAsync(m => m.MaterialCode == request.MaterialCode && m.DeletedAt == null, ct))
            return Conflict($"Mã vật tư '{request.MaterialCode}' đã tồn tại.");
        if (!await db.MaterialCategories.AnyAsync(c => c.Id == request.CategoryId, ct))
            return BadRequest("Danh mục vật tư không tồn tại.");

        var leaf = await db.MaterialCategories.AsNoTracking().FirstAsync(c => c.Id == request.CategoryId, ct);
        if (leaf.Level < 2)
            return BadRequest("Phải chọn tối thiểu đến cấp Nhóm vật tư (cấp 2).");

        var unit = MaterialUnitHelper.NormalizeDisplayName(request.Unit);
        if (string.IsNullOrEmpty(unit))
            return BadRequest("Vui lòng chọn đơn vị tính.");
        if (!await db.MaterialUnits.AnyAsync(u => u.Name == unit && u.IsActive, ct))
            return BadRequest("Đơn vị tính không tồn tại hoặc đã ngừng dùng.");

        var origin = MaterialOriginHelper.NormalizeDisplayName(request.Origin);
        var material = new Material
        {
            MaterialCode = request.MaterialCode.Trim(),
            MaterialName = request.MaterialName.Trim(),
            CategoryId = request.CategoryId,
            Unit = unit,
            Origin = string.IsNullOrEmpty(origin) ? null : origin,
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            TechnicalSpec = request.TechnicalSpec,
            GeneralTechnicalSpec = request.GeneralTechnicalSpec,
            QualityStandard = request.QualityStandard,
            CeilingPrice = request.CeilingPrice is > 0 ? request.CeilingPrice : null,
            AbcClass = NormalizeAbc(request.AbcClass),
            IsConsumable = request.IsConsumable,
            IsActive = true
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync(ct);

        var categoryById = await LoadCategoryLookupAsync([material.CategoryId], ct);
        return CreatedAtAction(nameof(GetById), new { code = material.MaterialCode },
            ToDto(material, categoryById));
    }

    [HttpPost("upload-image")]
    [RequireAnyPermission("wms.materials.create", "wms.materials.update")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(
        IFormFile file,
        [FromForm] string? materialCode,
        [FromServices] NamViet.ERP.Api.Services.Storage.IFileStorage storage,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("File ảnh không hợp lệ hoặc rỗng.");

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest("Kích thước ảnh tối đa là 10MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif"))
            return BadRequest("Định dạng file không hỗ trợ. Vui lòng chọn ảnh JPG, PNG, WEBP hoặc GIF.");

        var contentType = file.ContentType ?? "image/jpeg";
        var safeCode = string.IsNullOrWhiteSpace(materialCode) ? "mat" : materialCode.Trim().Replace('/', '_').Replace('\\', '_');
        var relativePath = $"materials/{safeCode}_{Guid.NewGuid():N}{ext}";

        await using var stream = file.OpenReadStream();
        var stored = await storage.SaveAsync(stream, relativePath, contentType, ct);

        var imageUrl = $"/api/warehouse/materials/images/{stored.StorageKey}";
        return Ok(new { imageUrl, storageKey = stored.StorageKey });
    }

    [HttpGet("images/{*key}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetImage(
        string key,
        [FromServices] NamViet.ERP.Api.Services.Storage.IFileStorage storage,
        CancellationToken ct)
    {
        if (!NamViet.ERP.Api.Services.Storage.StorageKeyPolicy.IsMaterialImage(key)) return NotFound();
        var stored = await storage.OpenReadAsync(key, ct);
        if (stored is null) return NotFound();
        return File(stored.Stream, stored.ResolveContentType(key), enableRangeProcessing: true);
    }

    [HttpGet("import-template")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadImportTemplate(CancellationToken ct)
    {
        var bytes = await materialExcel.GenerateTemplateAsync(ct);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "mau-import-danh-muc-vat-tu.xlsx");
    }

    [HttpPost("parse-import-file")]
    [RequirePermission("wms.materials.create")]
    [RequestSizeLimit(5 * 1024 * 1024 + 1024)]
    public async Task<ActionResult<MaterialImportParseResultDto>> ParseImportFile(IFormFile file, CancellationToken ct)
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
            var result = await materialExcel.ParseImportFileAsync(stream, file.FileName, ct);
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
    [RequirePermission("wms.materials.create")]
    public async Task<ActionResult<MaterialImportResultDto>> ImportBatch(
        [FromBody] MaterialImportBatchRequest request, CancellationToken ct)
    {
        if (request is null || request.Rows.Count == 0)
            return BadRequest("Không có dữ liệu vật tư để import.");

        try
        {
            var result = await materialExcel.ImportBatchAsync(request, ct);
            return Ok(result);
        }
        catch (DomainRuleException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest($"Lỗi trong quá trình import vật tư: {ex.Message}");
        }
    }

    [HttpPut("{code}")]
    [RequirePermission("wms.materials.update")]
    public async Task<IActionResult> Update(string code, [FromBody] UpsertMaterialRequest request, CancellationToken ct)
    {
        var m = await db.Materials.FirstOrDefaultAsync(x => x.MaterialCode == code, ct);
        if (m is null) return NotFound();
        if (!await db.MaterialCategories.AnyAsync(c => c.Id == request.CategoryId, ct))
            return BadRequest("Danh mục vật tư không tồn tại.");

        var leaf = await db.MaterialCategories.AsNoTracking().FirstAsync(c => c.Id == request.CategoryId, ct);
        if (leaf.Level < 2)
            return BadRequest("Phải chọn tối thiểu đến cấp Nhóm vật tư (cấp 2).");

        var unit = MaterialUnitHelper.NormalizeDisplayName(request.Unit);
        if (string.IsNullOrEmpty(unit))
            return BadRequest("Vui lòng chọn đơn vị tính.");
        if (!await db.MaterialUnits.AnyAsync(u => u.Name == unit && u.IsActive, ct))
            return BadRequest("Đơn vị tính không tồn tại hoặc đã ngừng dùng.");

        var origin = MaterialOriginHelper.NormalizeDisplayName(request.Origin);
        m.MaterialName = request.MaterialName.Trim();
        m.CategoryId = request.CategoryId;
        m.Unit = unit;
        m.Origin = string.IsNullOrEmpty(origin) ? null : origin;
        m.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        m.TechnicalSpec = request.TechnicalSpec;
        m.GeneralTechnicalSpec = request.GeneralTechnicalSpec;
        m.QualityStandard = request.QualityStandard;
        m.CeilingPrice = request.CeilingPrice is > 0 ? request.CeilingPrice : null;
        m.AbcClass = NormalizeAbc(request.AbcClass);
        m.IsConsumable = request.IsConsumable;
        m.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{code}/discontinue")]
    [RequirePermission("wms.materials.update")]
    public async Task<IActionResult> Discontinue(string code, CancellationToken ct)
    {
        var m = await db.Materials.FirstOrDefaultAsync(x => x.MaterialCode == code, ct);
        if (m is null) return NotFound();
        if (!m.IsActive) return NoContent();

        m.IsActive = false;
        m.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{code}/reactivate")]
    [RequirePermission("wms.materials.update")]
    public async Task<IActionResult> Reactivate(string code, CancellationToken ct)
    {
        var m = await db.Materials.FirstOrDefaultAsync(x => x.MaterialCode == code, ct);
        if (m is null) return NotFound();
        if (m.IsActive) return NoContent();

        m.IsActive = true;
        m.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{code}")]
    [RequirePermission("wms.materials.delete")]
    public async Task<IActionResult> Delete(string code, CancellationToken ct)
    {
        var m = await db.Materials.FirstOrDefaultAsync(x => x.MaterialCode == code, ct);
        if (m is null) return NotFound();

        if (await MaterialUsageHelper.IsInUseAsync(db, code, ct))
        {
            return Conflict(new
            {
                message = "Vật tư đang được dùng trong dự án, kho hoặc phiếu. Hãy dùng «Ngừng sử dụng» để giữ lịch sử.",
                canDiscontinue = true
            });
        }

        m.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IReadOnlyDictionary<int, MaterialCategory>> LoadCategoryLookupAsync(
        IEnumerable<int> leafCategoryIds,
        CancellationToken ct)
    {
        var lookup = new Dictionary<int, MaterialCategory>();
        var pending = leafCategoryIds.Distinct().ToHashSet();

        while (pending.Count > 0)
        {
            var batch = pending.ToList();
            pending.Clear();

            var rows = await db.MaterialCategories.AsNoTracking()
                .Where(c => batch.Contains(c.Id))
                .ToListAsync(ct);

            foreach (var category in rows)
            {
                lookup[category.Id] = category;
                if (category.ParentId is int parentId && !lookup.ContainsKey(parentId))
                    pending.Add(parentId);
            }
        }

        return lookup;
    }

    private static string? NormalizeAbc(string? value)
    {
        var v = value?.Trim().ToUpperInvariant();
        return v is "A" or "B" or "C" ? v : null;
    }

    private static MaterialDto ToDto(Material m, IReadOnlyDictionary<int, MaterialCategory> categoryById)
    {
        categoryById.TryGetValue(m.CategoryId, out var leaf);
        var path = MaterialCategoryHelper.BuildPath(leaf, categoryById);
        return new MaterialDto(
            m.MaterialCode, m.MaterialName, m.CategoryId, leaf?.Name,
            path,
            m.Unit, m.TechnicalSpec, m.GeneralTechnicalSpec, m.QualityStandard,
            m.CeilingPrice, m.AbcClass,
            m.IsConsumable, m.IsActive,
            m.Origin, m.ImageUrl);
    }
}
