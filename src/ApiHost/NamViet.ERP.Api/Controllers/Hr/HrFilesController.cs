using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Services.Storage;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/hr/files")]
[Authorize]
public class HrFilesController(IFileStorage storage) : ControllerBase
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> ImageContentTypes =
    [
        "image/jpeg", "image/png", "image/jpg", "application/pdf"
    ];
    private static readonly HashSet<string> CvContentTypes =
    [
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
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
        var safeCategory = SanitizeSegment(category);
        var isCv = safeCategory.Equals("cv", StringComparison.OrdinalIgnoreCase);

        if (isCv)
        {
            if (!CvContentTypes.Contains(contentType) && !HasCvExtension(file.FileName))
                return BadRequest("Chỉ chấp nhận file CV (.pdf, .doc, .docx).");
        }
        else if (!ImageContentTypes.Contains(contentType))
        {
            return BadRequest("Chỉ chấp nhận file ảnh (JPEG/PNG) hoặc PDF.");
        }

        var ext = ResolveExtension(file.FileName, contentType, isCv);
        string relativePath;

        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            if (User.Identity?.IsAuthenticated != true)
                return Unauthorized();
            relativePath = $"hr/employees/{SanitizeSegment(employeeId)}/{safeCategory}{ext}";
        }
        else if (isCv)
        {
            var sid = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : SanitizeSegment(sessionId);
            if (string.IsNullOrEmpty(sid)) sid = Guid.NewGuid().ToString("N");
            var safeName = SanitizeFileName(file.FileName);
            relativePath = $"hr/candidates/{sid}/{safeName}";
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

    private static bool HasCvExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".pdf" or ".doc" or ".docx";
    }

    private static string ResolveExtension(string fileName, string contentType, bool isCv)
    {
        if (isCv && !string.IsNullOrWhiteSpace(fileName))
        {
            var ext = Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(ext)) return ext.ToLowerInvariant();
        }

        return contentType switch
        {
            "image/png" => ".png",
            "application/pdf" => ".pdf",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            _ => ".jpg",
        };
    }

    private static string SanitizeSegment(string value) =>
        new string(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());

    private static string SanitizeFileName(string value)
    {
        var name = Path.GetFileName(value);
        var ext = Path.GetExtension(name);
        var stem = Path.GetFileNameWithoutExtension(name);
        var safeStem = new string(stem.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.').ToArray());
        if (string.IsNullOrWhiteSpace(safeStem)) safeStem = "cv";
        var safeExt = ext.ToLowerInvariant();
        if (safeExt is not (".pdf" or ".doc" or ".docx")) safeExt = ".pdf";
        return $"{safeStem}{safeExt}";
    }
}
