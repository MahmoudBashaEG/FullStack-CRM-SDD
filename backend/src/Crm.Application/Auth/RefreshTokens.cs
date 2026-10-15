using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Crm.Application.Auth;

/// <summary>Opaque refresh tokens: 256 random bits; only the SHA-256 hash is ever persisted.</summary>
public static class RefreshTokens
{
    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
