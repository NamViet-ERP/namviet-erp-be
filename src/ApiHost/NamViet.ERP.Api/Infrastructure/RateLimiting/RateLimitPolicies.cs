namespace NamViet.ERP.Api.Infrastructure.RateLimiting;

public static class RateLimitPolicies
{
    /// <summary>Login / refresh — chống brute force.</summary>
    public const string Auth = "auth";

    /// <summary>Public write endpoints (apply, onboard).</summary>
    public const string PublicWrite = "public-write";

    /// <summary>Anonymous file upload.</summary>
    public const string PublicUpload = "public-upload";

    /// <summary>Anonymous read endpoints (recruitment lookups).</summary>
    public const string PublicRead = "public-read";
}
