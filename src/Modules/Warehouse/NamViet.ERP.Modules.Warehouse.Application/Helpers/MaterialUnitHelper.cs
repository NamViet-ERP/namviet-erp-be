using System.Globalization;

namespace NamViet.ERP.Modules.Warehouse.Application.Helpers;

public static class MaterialUnitHelper
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Trim + sentence case (chữ cái đầu viết hoa, còn lại thường). VD: "mÉt Khối" → "Mét khối".</summary>
    public static string NormalizeDisplayName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var trimmed = raw.Trim();
        var lower = trimmed.ToLower(Vi);
        if (lower.Length == 0) return string.Empty;
        return char.ToUpper(lower[0], Vi) + lower[1..];
    }
}
