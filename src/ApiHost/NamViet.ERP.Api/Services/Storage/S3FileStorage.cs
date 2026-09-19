using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

namespace NamViet.ERP.Api.Services.Storage;

public class S3FileStorageOptions
{
    public const string SectionName = "FileStorage:S3";

    public string? ServiceUrl { get; set; }
    public string BucketName { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public bool ForcePathStyle { get; set; } = true;
    public string? KeyPrefix { get; set; }
}

public sealed class S3FileStorage(
    IAmazonS3 s3Client,
    S3FileStorageOptions options,
    ILogger<S3FileStorage> logger) : IFileStorage
{
    private readonly string _bucketName = options.BucketName;
    private readonly string _prefix = string.IsNullOrWhiteSpace(options.KeyPrefix)
        ? string.Empty
        : options.KeyPrefix.Trim().Trim('/') + "/";

    public async Task<StoredFile> SaveAsync(
        Stream content,
        string relativePath,
        string contentType,
        CancellationToken ct = default)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var s3Key = _prefix + normalized;

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            InputStream = content,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            AutoCloseStream = false,
        };

        await s3Client.PutObjectAsync(request, ct);

        var fileName = Path.GetFileName(normalized);
        var size = content.CanSeek ? content.Length : 0;
        return new StoredFile(normalized, fileName, size);
    }

    public StoredFileContent? Get(string storageKey)
    {
        // Remote object storage does not provide a local filesystem path.
        return null;
    }

    public async Task<StoredFileStream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return null;

        var normalized = storageKey.Replace('\\', '/').TrimStart('/');
        var s3Key = _prefix + normalized;

        try
        {
            var response = await s3Client.GetObjectAsync(_bucketName, s3Key, ct);
            return new StoredFileStream(
                response.ResponseStream,
                response.Headers.ContentType,
                response.ContentLength);
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.NotFound
            || string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ex.ErrorCode, "NotFound", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("S3 object not found: {Key} in bucket {Bucket}", s3Key, _bucketName);
            return null;
        }
    }

    public Task<int> DeleteUnreferencedOlderThanAsync(
        IReadOnlySet<string> referencedKeys,
        DateTime olderThanUtc,
        CancellationToken ct = default)
    {
        // Object cleanup on cloud storage is typically managed via bucket lifecycle policies.
        return Task.FromResult(0);
    }
}
