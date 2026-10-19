namespace NamViet.ERP.Modules.Warehouse.Domain.SupplierEvaluation;

/// <summary>Vai trò liên hệ NCC: Chính / Phụ / Backup.</summary>
public static class SupplierContactRoles
{
    public const string Primary = "primary";
    public const string Secondary = "secondary";
    public const string Backup = "backup";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Primary, Secondary, Backup,
    };

    public static bool TryNormalize(string? role, out string normalized)
    {
        var value = (role ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Length == 0)
        {
            normalized = Secondary;
            return true;
        }

        if (!All.Contains(value))
        {
            normalized = Secondary;
            return false;
        }

        normalized = value;
        return true;
    }

    public static string ToClientLabel(string role) =>
        string.Equals(role, Primary, StringComparison.OrdinalIgnoreCase) ? "Chính"
        : string.Equals(role, Backup, StringComparison.OrdinalIgnoreCase) ? "Backup"
        : "Phụ";
}
