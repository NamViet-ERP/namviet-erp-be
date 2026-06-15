namespace NamViet.ERP.Api.Services.Storage;

public class LocalFileStorage(IWebHostEnvironment env) : IFileStorage
{
    private const string UploadRoot = "uploads";

    private string WebRoot =>
        env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

    public async Task<StoredFile> SaveAsync(
        Stream content, string relativePath, string contentType, CancellationToken ct = default)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var fullPath = Path.Combine(WebRoot, UploadRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct);

        var fileName = Path.GetFileName(fullPath);
        var size = new FileInfo(fullPath).Length;
        var url = $"/{UploadRoot}/{normalized}";
        return new StoredFile(url, fileName, size);
    }
}
