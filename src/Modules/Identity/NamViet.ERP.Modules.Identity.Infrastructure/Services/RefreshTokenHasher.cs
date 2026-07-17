using System.Security.Cryptography;
using System.Text;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Services;

public interface IRefreshTokenHasher
{
    string Hash(string token);
}

public sealed class RefreshTokenHasher : IRefreshTokenHasher
{
    public string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
