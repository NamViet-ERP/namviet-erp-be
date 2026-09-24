namespace NamViet.ERP.Api.Services.Storage;

public record StoredFile(string StorageKey, string FileName, long Size);
public record StoredFileContent(string FullPath, long Size);
public record StoredFileStream(Stream Stream, string? ContentType, long Size)
{
    public string ResolveContentType(string? fallbackFileName = null)
    {
        if (!string.IsNullOrWhiteSpace(ContentType)
            && !string.Equals(ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return ContentType;
        }

        if (!string.IsNullOrWhiteSpace(fallbackFileName))
        {
            var guessed = GuessContentType(fallbackFileName);
            if (!string.Equals(guessed, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                return guessed;
            }
        }

        return !string.IsNullOrWhiteSpace(ContentType) ? ContentType : "application/octet-stream";
    }

    public static string GuessContentType(string pathOrFileName)
    {
        var ext = Path.GetExtension(pathOrFileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream",
        };
    }
}

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string relativePath, string contentType, CancellationToken ct = default);
    StoredFileContent? Get(string storageKey);
    Task<StoredFileStream?> OpenReadAsync(string storageKey, CancellationToken ct = default);
    Task<int> DeleteUnreferencedOlderThanAsync(
        IReadOnlySet<string> referencedKeys,
        DateTime olderThanUtc,
        CancellationToken ct = default);
}
