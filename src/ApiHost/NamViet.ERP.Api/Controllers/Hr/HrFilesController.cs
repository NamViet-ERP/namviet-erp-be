using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Services.Storage;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/hr/files")]
public class HrFilesController(IFileStorage storage) : ControllerBase
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/jpeg", "image/png", "image/jpg", "application/pdf"
    ];

    [HttpPost("upload")]
    [AllowAnonymous]
    [RequestSizeLimit(MaxBytes + 1024)]
    public async Task<ActionResult<FeFileUploadResponse>> Upload(
        IFormFile file,
        [FromForm] string category,
        [FromForm] string? sessionId,
        [FromForm] string? employeeId,
        CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > MaxBytes)
            return BadRequest("File không hợp lệ hoặc vượt quá 5MB.");

        var contentType = file.ContentType?.ToLowerInvariant() ?? "";
        if (!AllowedContentTypes.Contains(contentType))
            return BadRequest("Chỉ chấp nhận file ảnh (JPEG/PNG) hoặc PDF.");

        var ext = contentType switch
        {
            "image/png" => ".png",
            "application/pdf" => ".pdf",
            _ => ".jpg"
        };

        var safeCategory = SanitizeSegment(category);
        string relativePath;
        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            if (User.Identity?.IsAuthenticated != true)
                return Unauthorized();
            relativePath = $"hr/employees/{SanitizeSegment(employeeId)}/{safeCategory}{ext}";
        }
        else
        {
            var sid = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : SanitizeSegment(sessionId);
            if (string.IsNullOrEmpty(sid)) sid = Guid.NewGuid().ToString("N");
            relativePath = $"hr/onboard/{sid}/{safeCategory}{ext}";
        }

        await using var stream = file.OpenReadStream();
        var stored = await storage.SaveAsync(stream, relativePath, contentType, ct);
        return Ok(new FeFileUploadResponse(stored.Url, stored.FileName, stored.Size));
    }

    private static string SanitizeSegment(string value) =>
        new string(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
}
