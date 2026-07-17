using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Api.Infrastructure.RateLimiting;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/hr/files")]
[Authorize]
public class HrFilesController(
    IFileStorage storage,
    IEmployeeOnboardingInviteService onboardingInvites,
    HrDbContext db,
    ICurrentUser currentUser,
    IEmployeeAccessService employeeAccess,
    IAuthorizationService authorization) : ControllerBase
{
    private const long MaxBytes = 5 * 1024 * 1024;

    [HttpPost("upload")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicUpload)]
    [RequestSizeLimit(MaxBytes + 1024)]
    public async Task<ActionResult<FileUploadResponse>> Upload(
        IFormFile file,
        [FromForm] string category,
        [FromForm] string? sessionId,
        [FromForm] string? employeeId,
        [FromForm] string? inviteToken,
        CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > MaxBytes)
            return BadRequest("File không hợp lệ hoặc vượt quá 5MB.");

        var contentType = file.ContentType?.Trim().ToLowerInvariant() ?? "";
        var safeCategory = HrStorageKeyPolicy.SanitizeSegment(category);
        var isCv = safeCategory.Equals("cv", StringComparison.OrdinalIgnoreCase);

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream((int)file.Length);
        await input.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (!HrUploadValidator.IsValid(file.FileName, contentType, bytes, isCv))
        {
            return BadRequest(isCv
                ? "CV phải là PDF, DOC hoặc DOCX hợp lệ và khớp định dạng khai báo."
                : "File phải là PDF, JPEG hoặc PNG hợp lệ và khớp định dạng khai báo.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext == ".jpeg") ext = ".jpg";
        string relativePath;

        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            if (User.Identity?.IsAuthenticated != true)
                return Unauthorized();
            var canUpdate = await authorization.AuthorizeAsync(
                User, null, PermissionPolicies.Name("hrm.employees.update"));
            if (!canUpdate.Succeeded)
                return Forbid();
            relativePath = $"hr/employees/{HrStorageKeyPolicy.SanitizeSegment(employeeId)}/{Guid.NewGuid():N}{ext}";
        }
        else if (isCv)
        {
            var sid = HrStorageKeyPolicy.SanitizeSegment(sessionId);
            relativePath = $"hr/candidates/{sid}/{Guid.NewGuid():N}{ext}";
        }
        else if (currentUser.IsAuthenticated)
        {
            var canCreate = await authorization.AuthorizeAsync(
                User, null, PermissionPolicies.Name("hrm.employees.create"));
            if (!canCreate.Succeeded)
                return Forbid();
            var sid = HrStorageKeyPolicy.SanitizeSegment(sessionId);
            relativePath = $"hr/employees/pending/{sid}/{Guid.NewGuid():N}{ext}";
        }
        else
        {
            var invite = await onboardingInvites.FindValidAsync(inviteToken ?? "", ct);
            if (invite is null)
                return Unauthorized("Lời mời onboarding không hợp lệ hoặc đã hết hạn.");
            var sid = HrStorageKeyPolicy.SanitizeSegment(sessionId);
            relativePath = $"hr/onboard/{invite.Id}/{sid}/{Guid.NewGuid():N}{ext}";
        }

        buffer.Position = 0;
        var stored = await storage.SaveAsync(buffer, relativePath, contentType, ct);
        return Ok(new FileUploadResponse(stored.StorageKey, file.FileName, stored.Size));
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download(
        [FromQuery] string key,
        [FromQuery] string? fileName,
        CancellationToken ct)
    {
        if (!HrStorageKeyPolicy.IsSafeStorageKey(key))
            return NotFound();
        var normalized = HrStorageKeyPolicy.Normalize(key);
        if (!normalized.StartsWith("hr/employees/", StringComparison.Ordinal)
            && !normalized.StartsWith("hr/onboard/", StringComparison.Ordinal))
            return NotFound();

        var legacyKey = $"/uploads/{normalized}";
        var employeeId = await db.Employees.AsNoTracking()
            .Where(e => e.DeletedAt == null
                && (e.PhotoUrl == normalized || e.PhotoUrl == legacyKey
                    || e.CccdFrontUrl == normalized || e.CccdFrontUrl == legacyKey
                    || e.CccdBackUrl == normalized || e.CccdBackUrl == legacyKey
                    || e.DegreePhotoUrl == normalized || e.DegreePhotoUrl == legacyKey))
            .Select(e => (int?)e.Id)
            .FirstOrDefaultAsync(ct);
        if (!employeeId.HasValue
            || !await employeeAccess.CanViewEmployeeAsync(currentUser, employeeId.Value, ct))
            return NotFound();

        var stored = storage.Get(normalized);
        if (stored is null) return NotFound();
        var downloadName = string.IsNullOrWhiteSpace(fileName)
            ? Path.GetFileName(stored.FullPath)
            : Path.GetFileName(fileName);
        var contentType = Path.GetExtension(downloadName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream",
        };
        return PhysicalFile(stored.FullPath, contentType, downloadName);
    }
}
