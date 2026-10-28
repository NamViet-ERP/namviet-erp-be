using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Services.Cskh;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Api.Services.Storage;
using Microsoft.AspNetCore.WebUtilities;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/projects/{code}/documents")]
[Authorize]
public class ProjectDocumentsController(ICskhCoordinationService cskhService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.projects.view:all")]
    public ActionResult<List<ProjectDocumentDto>> List(string code)
    {
        var list = cskhService.GetDocuments(code);
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("wms.projects.update")]
    public ActionResult<ProjectDocumentDto> Create(string code, [FromBody] CreateProjectDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Mã hồ sơ và tên hồ sơ không được để trống." });

        var created = cskhService.CreateDocument(code, request);
        return Created($"/api/warehouse/projects/{code}/documents/{created.Id}", created);
    }


    [HttpPut("{id}")]
    [RequirePermission("wms.projects.update")]
    public ActionResult<ProjectDocumentDto> Update(string code, string id, [FromBody] CreateProjectDocumentRequest request)
    {
        var updated = cskhService.UpdateDocument(code, id, request);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [RequirePermission("wms.projects.update")] // Note: using update since delete might not exist
    public ActionResult Delete(string code, string id)
    {
        var success = cskhService.DeleteDocument(code, id);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPut("{id}/status")]
    [RequirePermission("wms.projects.update")]
    public ActionResult<ProjectDocumentDto> UpdateStatus(string code, string id, [FromBody] UpdateProjectDocumentStatusRequest request)
    {
        var updated = cskhService.UpdateDocumentStatus(code, id, request);
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
        [FromServices] NamViet.ERP.Api.Services.Storage.IFileStorage storage,
        System.Threading.CancellationToken ct)
    {
        if (file == null || file.Length == 0) return BadRequest(new { error = "File rỗng." });
        if (!StorageKeyPolicy.IsSafeSegment(code) || !StorageKeyPolicy.IsSafeSegment(id))
            return BadRequest("Mã dự án hoặc hồ sơ không hợp lệ.");
        if (!cskhService.GetDocuments(code).Any(d => d.Id == id)) return NotFound();

        var contentType = file.ContentType ?? "application/octet-stream";
        var ext = System.IO.Path.GetExtension(file.FileName);
        var relativePath = $"cskh/projects/{code}/docs/{id}/{System.Guid.NewGuid()}{ext}";

        using var stream = file.OpenReadStream();
        var stored = await storage.SaveAsync(stream, relativePath, contentType, ct);

        // Convert length to MB
        var mb = file.Length / 1024.0 / 1024.0;
        var sizeStr = mb < 1 ? $"{file.Length / 1024.0:F1} KB" : $"{mb:F1} MB";
        
        var downloadUrl = $"/api/warehouse/projects/{code}/documents/{id}/download?key={System.Net.WebUtility.UrlEncode(stored.StorageKey)}";

        var updated = cskhService.UpdateDocumentFile(code, id, file.FileName, sizeStr, downloadUrl);
        if (updated == null) return NotFound();

        return Ok(updated);
    }

    [HttpGet("{id}/download")]
    [RequirePermission("wms.projects.view:all")]
    public async Task<IActionResult> DownloadFile(
        string code,
        string id,
        [FromQuery] string? key,
        [FromServices] NamViet.ERP.Api.Services.Storage.IFileStorage storage,
        System.Threading.CancellationToken ct)
    {
        var doc = cskhService.GetDocuments(code).FirstOrDefault(d => d.Id == id);
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
