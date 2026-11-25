namespace NamViet.ERP.Api.Services.Storage;

public static class FileStorageProviderSelection
{
    public static (bool UseS3, S3FileStorageOptions S3Options) Resolve(IConfiguration configuration)
    {
        var provider = configuration["FileStorage:Provider"]?.Trim();

        var s3Options = configuration.GetSection(S3FileStorageOptions.SectionName)
                            .Get<S3FileStorageOptions>()
                        ?? new S3FileStorageOptions();

        var hasS3Credentials = !string.IsNullOrWhiteSpace(s3Options.BucketName)
                               && !string.IsNullOrWhiteSpace(s3Options.ServiceUrl)
                               && !string.IsNullOrWhiteSpace(s3Options.AccessKey)
                               && !string.IsNullOrWhiteSpace(s3Options.SecretKey);

        var useS3 = (string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase)
                     || (hasS3Credentials && !string.Equals(provider, "LocalForce", StringComparison.OrdinalIgnoreCase)))
                    && !string.IsNullOrWhiteSpace(s3Options.BucketName);

        return (useS3, s3Options);
    }
}
