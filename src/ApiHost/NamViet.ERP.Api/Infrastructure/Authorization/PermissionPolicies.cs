namespace NamViet.ERP.Api.Infrastructure.Authorization;

public static class PermissionPolicies
{
    public const string Prefix = "Permission:";
    public const string AnyPrefix = "PermissionAny:";

    public static string Name(string permission) => $"{Prefix}{permission}";

    public static string AnyName(params string[] permissions) =>
        $"{AnyPrefix}{string.Join(";", permissions.OrderBy(p => p, StringComparer.Ordinal))}";
}
