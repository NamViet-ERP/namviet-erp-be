namespace NamViet.ERP.Modules.Warehouse.Application.Helpers;

public static class MaterialOriginHelper
{
    /// <summary>Trim whitespace and clean string for origin/country of origin.</summary>
    public static string NormalizeDisplayName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return raw.Trim();
    }
}
