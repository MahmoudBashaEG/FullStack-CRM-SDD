using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;
namespace Crm.Infrastructure.Authentication;

public sealed class SigningCertificateProvider : IDisposable
{
    private readonly List<X509Certificate2> certificates = [];
    public SigningCredentials Credentials { get; }
    public IReadOnlyList<SecurityKey> ValidationKeys { get; }
    public SigningCertificateProvider(JwtOptions options, TimeProvider clock)
    {
        try
        {
            var current = Load(options.SigningCertificateThumbprint, options.StoreLocation, clock.GetUtcNow(), true);
            certificates.Add(current);
            var key = new X509SecurityKey(current) { KeyId = options.SigningKeyId };
            Credentials = new(key, SecurityAlgorithms.RsaSha256);
            var keys = new List<SecurityKey> { key };
            foreach (var thumbprint in options.PreviousThumbprints.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (thumbprint.Equals(current.Thumbprint, StringComparison.OrdinalIgnoreCase)) continue;
                var previous = Load(thumbprint, options.StoreLocation, clock.GetUtcNow(), false);
                certificates.Add(previous); keys.Add(new X509SecurityKey(previous) { KeyId = previous.Thumbprint });
            }
            ValidationKeys = keys;
        }
        catch { Dispose(); throw; }
    }
    private static X509Certificate2 Load(string thumbprint, StoreLocation location, DateTimeOffset now, bool privateKey)
    {
        using var store = new X509Store(StoreName.My, location); store.Open(OpenFlags.ReadOnly);
        var matches = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
        if (matches.Count != 1) { foreach (var item in matches) item.Dispose(); throw new InvalidOperationException("JWT signing certificate is unavailable."); }
        var certificate = matches[0];
        using var rsa = privateKey ? certificate.GetRSAPrivateKey() : certificate.GetRSAPublicKey();
        if (rsa is null || rsa.KeySize < 3072 || now.UtcDateTime < certificate.NotBefore.ToUniversalTime() || now.UtcDateTime >= certificate.NotAfter.ToUniversalTime())
        { certificate.Dispose(); throw new InvalidOperationException("JWT signing certificate is invalid."); }
        return certificate;
    }
    public void Dispose() { foreach (var certificate in certificates) certificate.Dispose(); certificates.Clear(); }
}
