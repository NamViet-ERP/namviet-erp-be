namespace NamViet.ERP.Api.Services.Storage;

public class LocalFileStorage(IWebHostEnvironment env) : IFileStorage
{
    private string StorageRoot => Path.GetFullPath(
        Path.Combine(env.ContentRootPath, "App_Data", "private-uploads"));

    public async Task<StoredFile> SaveAsync(
        Stream content, string relativePath, string contentType, CancellationToken ct = default)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var fullPath = ResolvePath(normalized);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct);

        var fileName = Path.GetFileName(fullPath);
        var size = new FileInfo(fullPath).Length;
        return new StoredFile(normalized, fileName, size);
    }

    public StoredFileContent? Get(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return null;
        var normalizedKey = NormalizeKey(storageKey);
        string fullPath;
        try { fullPath = ResolvePath(normalizedKey); }
        catch (InvalidOperationException) { return null; }
        if (File.Exists(fullPath))
            return new StoredFileContent(fullPath, new FileInfo(fullPath).Length);

        var legacyRoot = Path.GetFullPath(Path.Combine(
            env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads"));
        var legacyPath = Path.GetFullPath(Path.Combine(
            legacyRoot, normalizedKey.Replace('/', Path.DirectorySeparatorChar)));
        var legacyPrefix = legacyRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return legacyPath.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase)
            && File.Exists(legacyPath)
                ? new StoredFileContent(legacyPath, new FileInfo(legacyPath).Length)
                : null;
    }

    public Task<int> DeleteUnreferencedOlderThanAsync(
        IReadOnlySet<string> referencedKeys,
        DateTime olderThanUtc,
        CancellationToken ct = default)
    {
        var referenced = referencedKeys
            .Select(NormalizeKey)
            .ToHashSet(StringComparer.Ordinal);
        var deleted = 0;
        foreach (var relativeRoot in new[]
        {
            "hr/candidates",
            "hr/onboard",
            "hr/employees/pending",
        })
        {
            ct.ThrowIfCancellationRequested();
            var root = ResolvePath(relativeRoot);
            if (!Directory.Exists(root)) continue;

            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var directory = pending.Pop();
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                        pending.Push(child);
                }

                foreach (var path in Directory.EnumerateFiles(directory))
                {
                    var info = new FileInfo(path);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0
                        || info.LastWriteTimeUtc >= olderThanUtc)
                        continue;
                    var key = Path.GetRelativePath(StorageRoot, info.FullName)
                        .Replace('\\', '/');
                    if (referenced.Contains(key)) continue;
                    info.Delete();
                    deleted++;
                }
            }
        }
        return Task.FromResult(deleted);
    }

    private string ResolvePath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var fullPath = Path.GetFullPath(Path.Combine(
            StorageRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = StorageRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid storage key.");
        return fullPath;
    }

    private static string NormalizeKey(string storageKey)
    {
        var normalized = storageKey.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase)
            ? normalized["uploads/".Length..]
            : normalized;
    }
}
