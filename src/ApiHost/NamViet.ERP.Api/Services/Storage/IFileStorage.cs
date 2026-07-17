namespace NamViet.ERP.Api.Services.Storage;

public record StoredFile(string StorageKey, string FileName, long Size);
public record StoredFileContent(string FullPath, long Size);

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string relativePath, string contentType, CancellationToken ct = default);
    StoredFileContent? Get(string storageKey);
    Task<int> DeleteUnreferencedOlderThanAsync(
        IReadOnlySet<string> referencedKeys,
        DateTime olderThanUtc,
        CancellationToken ct = default);
}
