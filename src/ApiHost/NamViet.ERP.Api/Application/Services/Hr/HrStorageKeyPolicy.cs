using NamViet.ERP.Api.Application.Dtos.Hr;

namespace NamViet.ERP.Api.Application.Services.Hr;

public static class HrStorageKeyPolicy
{
    public static string Normalize(string value)
    {
        var normalized = value.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase)
            ? normalized["uploads/".Length..]
            : normalized;
    }

    public static string SanitizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Guid.NewGuid().ToString("N");
        var sanitized = new string(value.Where(c =>
            char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrEmpty(sanitized) ? Guid.NewGuid().ToString("N") : sanitized;
    }

    public static bool IsSafeStorageKey(string value)
    {
        var normalized = Normalize(value);
        return !Path.IsPathRooted(normalized)
            && normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .All(segment => segment is not "." and not ".." && !segment.Contains(':'));
    }

    public static bool AreOnboardingAttachmentsOwnedByInvite(EmployeeDto dto, int inviteId)
    {
        return IsOnboardingAttachmentOwnedByInvite(dto.PhotoUrl, inviteId)
            && IsOnboardingAttachmentOwnedByInvite(dto.CccdFrontUrl, inviteId)
            && IsOnboardingAttachmentOwnedByInvite(dto.CccdBackUrl, inviteId)
            && IsOnboardingAttachmentOwnedByInvite(dto.DegreePhotoUrl, inviteId);
    }

    public static bool IsOnboardingAttachmentOwnedByInvite(string? key, int inviteId) =>
        key is null || (IsSafeStorageKey(key)
            && Normalize(key).StartsWith($"hr/onboard/{inviteId}/", StringComparison.Ordinal));
}
