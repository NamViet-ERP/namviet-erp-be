using Microsoft.Extensions.Configuration;

namespace NamViet.ERP.Api.Services.Storage;

public class LocalFileStorage(IWebHostEnvironment env, IConfiguration configuration) : IFileStorage
{
    private string? _resolvedRoot;

    private string StorageRoot => _resolvedRoot ??= ResolvePrimaryRoot();

    public async Task<StoredFile> SaveAsync(
        Stream content, string relativePath, string contentType, CancellationToken ct = default)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var fullPath = ResolvePath(StorageRoot, normalized);
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

        foreach (var root in EnumerateLookupRoots())
        {
            string fullPath;
            try { fullPath = ResolvePath(root, normalizedKey); }
            catch (InvalidOperationException) { continue; }
            if (File.Exists(fullPath))
                return new StoredFileContent(fullPath, new FileInfo(fullPath).Length);
        }

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

    public Task<StoredFileStream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var stored = Get(storageKey);
        if (stored is null) return Task.FromResult<StoredFileStream?>(null);

        var stream = new FileStream(
            stored.FullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        return Task.FromResult<StoredFileStream?>(new StoredFileStream(stream, GuessContentType(stored.FullPath), stored.Size));
    }

    private static string GuessContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream",
        };
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
            var root = ResolvePath(StorageRoot, relativeRoot);
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

    private string ResolvePrimaryRoot()
    {
        var configured = configuration["FileStorage:Root"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var path = Path.GetFullPath(configured);
            Directory.CreateDirectory(path);
            return path;
        }

        // Prefer shared artifacts folder so VS Debug and api-run publish use the same uploads.
        var artifacts = FindRepoRelative(
            env.ContentRootPath,
            Path.Combine("artifacts", "api-run", "App_Data", "private-uploads"));
        if (artifacts is not null)
        {
            Directory.CreateDirectory(artifacts);
            return artifacts;
        }

        var local = Path.GetFullPath(Path.Combine(env.ContentRootPath, "App_Data", "private-uploads"));
        Directory.CreateDirectory(local);
        return local;
    }

    private IEnumerable<string> EnumerateLookupRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in new[]
                 {
                     StorageRoot,
                     Path.GetFullPath(Path.Combine(env.ContentRootPath, "App_Data", "private-uploads")),
                     FindRepoRelative(
                         env.ContentRootPath,
                         Path.Combine("artifacts", "api-run", "App_Data", "private-uploads")),
                 })
        {
            if (candidate is null || !seen.Add(candidate)) continue;
            yield return candidate;
        }
    }

    private static string? FindRepoRelative(string startDir, string relativeParts)
    {
        try
        {
            var dir = new DirectoryInfo(Path.GetFullPath(startDir));
            while (dir is not null)
            {
                var marker = Path.Combine(dir.FullName, "NamViet.ERP.sln");
                var candidate = Path.Combine(dir.FullName, relativeParts);
                if (File.Exists(marker) || Directory.Exists(candidate))
                    return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }
        catch
        {
            /* ignore path probe failures */
        }
        return null;
    }

    private static string ResolvePath(string storageRoot, string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var fullPath = Path.GetFullPath(Path.Combine(
            storageRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = storageRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullPath, storageRoot, StringComparison.OrdinalIgnoreCase))
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
