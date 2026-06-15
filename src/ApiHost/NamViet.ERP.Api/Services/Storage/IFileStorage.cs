namespace NamViet.ERP.Api.Services.Storage;

public record StoredFile(string Url, string FileName, long Size);

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string relativePath, string contentType, CancellationToken ct = default);
}
