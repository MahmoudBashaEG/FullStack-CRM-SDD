using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
namespace Crm.IntegrationTests.Fixtures;

// Generates and removes only this fixture's own signing credential, never the HTTPS key.
public sealed class LocalAccountFixture : IDisposable
{
    public X509Certificate2 Certificate { get; }
    public LocalAccountFixture(int keySize = 3072, DateTimeOffset? expires = null)
    {
        using var rsa = RSA.Create(keySize);
        var request = new CertificateRequest("CN=CRM Owned Foundation Test " + Guid.NewGuid().ToString("N"), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var temporary = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-10), expires ?? DateTimeOffset.UtcNow.AddDays(7));
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Certificate = X509CertificateLoader.LoadPkcs12(temporary.Export(X509ContentType.Pkcs12, secret), secret, X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet);
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser); store.Open(OpenFlags.ReadWrite); store.Add(Certificate);
    }
    public IConfiguration Configuration(string connection) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    {
        ["ConnectionStrings:Crm"] = connection, ["Jwt:Issuer"] = "urn:crm:local", ["Jwt:Audience"] = "crm-api",
        ["Jwt:SigningCertificateThumbprint"] = Certificate.Thumbprint, ["Jwt:SigningKeyId"] = Certificate.Thumbprint, ["Jwt:CertificateStoreLocation"] = "CurrentUser"
    }).Build();
    public void Dispose()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser); store.Open(OpenFlags.ReadWrite); store.Remove(Certificate);
        using var key = Certificate.GetRSAPrivateKey(); if (key is RSACng cng && !cng.Key.IsEphemeral) cng.Key.Delete();
        Certificate.Dispose();
    }
}
