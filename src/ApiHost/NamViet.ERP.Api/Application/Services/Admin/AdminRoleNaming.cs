using System.Globalization;
using System.Text;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Identity.Domain.Entities;

namespace NamViet.ERP.Api.Application.Services.Admin;

public static class AdminRoleNaming
{
    public static string NormalizeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Collapse Vietnamese diacritics so "Nhân viên" and "Nhan vien" compare equal.
    /// FormD + strip marks is not enough for đ.
    /// </summary>
    public static string FoldLabel(string? value)
    {
        var normalized = NormalizeLabel(value);
        if (normalized.Length == 0) return string.Empty;

        var formD = normalized.Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (ch is 'đ') { folded.Append('d'); continue; }
            if (ch is 'Đ') { folded.Append('D'); continue; }
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            folded.Append(ch);
        }

        return folded.ToString();
    }

    public static bool SameDisplayLabel(string? a, string? b) =>
        string.Equals(FoldLabel(a), FoldLabel(b), StringComparison.OrdinalIgnoreCase);

    public static string DisplayLabel(ApplicationRole role) =>
        DisplayLabel(role.Description, role.Name);

    public static string DisplayLabel(string? description, string? identityName)
    {
        if (!string.IsNullOrWhiteSpace(description))
            return description.Trim();
        return WmsMapper.ToClientRoleLabel(identityName ?? "").Trim();
    }
}
