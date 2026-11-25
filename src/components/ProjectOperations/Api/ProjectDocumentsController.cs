using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using NamViet.ERP.Api.Application.Services.Cskh;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/projects/{code}/documents")]
[Authorize]
public class ProjectDocumentsController(ICskhCoordinationService cskhService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.projects.view:all")]
    public async Task<ActionResult<List<ProjectDocumentDto>>> List(string code, CancellationToken ct = default)
    {
        var list = await cskhService.GetDocumentsAsync(code, ct);
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<ProjectDocumentDto>> Create(
        string code, [FromBody] CreateProjectDocumentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Mã hồ sơ và tên hồ sơ không được để trống." });

        var created = await cskhService.CreateDocumentAsync(code, request, ct);
        return Created($"/api/warehouse/projects/{code}/documents/{created.Id}", created);
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<ProjectDocumentDto>> Update(
        string code, string id, [FromBody] CreateProjectDocumentRequest request, CancellationToken ct = default)
    {
        var updated = await cskhService.UpdateDocumentAsync(code, id, request, ct);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [RequirePermission("wms.projects.update")] // Note: using update since delete might not exist
    public async Task<ActionResult> Delete(string code, string id, CancellationToken ct = default)
    {
        var success = await cskhService.DeleteDocumentAsync(code, id, ct);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPut("{id}/status")]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<ProjectDocumentDto>> UpdateStatus(
        string code, string id, [FromBody] UpdateProjectDocumentStatusRequest request, CancellationToken ct = default)
    {
        var updated = await cskhService.UpdateDocumentStatusAsync(code, id, request, ct);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpPost("{id}/upload")]
    [RequirePermission("wms.projects.update")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ProjectDocumentDto>> UploadFile(
        string code,
        string id,
        Microsoft.AspNetCore.Http.IFormFile file,
        [FromServices] IFileStorage storage,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0) return BadRequest(new { error = "File rỗng." });
        if (!StorageKeyPolicy.IsSafeSegment(code) || !StorageKeyPolicy.IsSafeSegment(id))
            return BadRequest("Mã dự án hoặc hồ sơ không hợp lệ.");

        var docs = await cskhService.GetDocumentsAsync(code, ct);
        if (!docs.Any(d => d.Id == id)) return NotFound();

        var contentType = file.ContentType ?? "application/octet-stream";
        await using var stream = file.OpenReadStream();
        var (isValidFile, validationError) = await FileUploadValidator.ValidateProjectDocumentAsync(
            file.FileName, contentType, stream, ct);
        if (!isValidFile)
            return BadRequest(new { error = validationError });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var relativePath = $"cskh/projects/{code}/docs/{id}/{Guid.NewGuid()}{ext}";

        var stored = await storage.SaveAsync(stream, relativePath, contentType, ct);

        var mb = file.Length / 1024.0 / 1024.0;
        var sizeStr = mb < 1 ? $"{file.Length / 1024.0:F1} KB" : $"{mb:F1} MB";

        var downloadUrl = $"/api/warehouse/projects/{code}/documents/{id}/download?key={System.Net.WebUtility.UrlEncode(stored.StorageKey)}";

        var updated = await cskhService.UpdateDocumentFileAsync(code, id, file.FileName, sizeStr, downloadUrl, ct);
        if (updated == null) return NotFound();

        return Ok(updated);
    }

    [HttpGet("{id}/download")]
    [RequirePermission("wms.projects.view:all")]
    public async Task<IActionResult> DownloadFile(
        string code,
        string id,
        [FromQuery] string? key,
        [FromServices] IFileStorage storage,
        CancellationToken ct = default)
    {
        var docs = await cskhService.GetDocumentsAsync(code, ct);
        var doc = docs.FirstOrDefault(d => d.Id == id);
        if (doc is null || string.IsNullOrWhiteSpace(doc.DownloadUrl)
            || !StorageKeyPolicy.IsSafeSegment(code) || !StorageKeyPolicy.IsSafeSegment(id))
            return NotFound();

        var queryStart = doc.DownloadUrl.IndexOf('?');
        if (queryStart < 0) return NotFound();
        var query = QueryHelpers.ParseQuery(doc.DownloadUrl[queryStart..]);
        var storedKey = query["key"].ToString();
        if (!StorageKeyPolicy.IsFileInDirectory(storedKey, $"cskh/projects/{code}/docs/{id}")
            || (key is not null && !string.Equals(key, storedKey, StringComparison.Ordinal))) return NotFound();

        var stored = await storage.OpenReadAsync(storedKey, ct);
        if (stored == null) return NotFound();
        var fileName = doc.FileName ?? "download";

        return File(stored.Stream, stored.ResolveContentType(fileName), fileName);
    }
}
