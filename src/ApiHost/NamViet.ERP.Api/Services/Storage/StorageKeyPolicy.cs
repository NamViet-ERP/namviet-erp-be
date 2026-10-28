namespace NamViet.ERP.Api.Services.Storage;

public static class StorageKeyPolicy
{
    public static bool IsSafeSegment(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value is not "." and not ".."
        && !value.Any(c => char.IsControl(c) || c is '/' or '\\' or ':' or '%' or '?' or '#');

    public static bool IsFileInDirectory(string? key, string directory) =>
        key is not null && key.StartsWith(directory + "/", StringComparison.Ordinal)
        && IsSafeSegment(key[(directory.Length + 1)..]);

    public static bool IsMaterialImage(string? key) =>
        IsFileInDirectory(key, "materials")
        && Path.GetExtension(key!).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif";
}
