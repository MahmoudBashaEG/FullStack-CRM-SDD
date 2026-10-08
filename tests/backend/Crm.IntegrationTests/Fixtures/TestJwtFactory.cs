using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Crm.IntegrationTests.Fixtures;

public sealed class TestJwtFactory : IDisposable
{
    private readonly RSA rsa = RSA.Create(2048);
    public string PublicKeyPath { get; } = Path.Combine(Path.GetTempPath(), $"crm-public-{Guid.NewGuid():N}.pem");
    public TestJwtFactory() => File.WriteAllText(PublicKeyPath, rsa.ExportSubjectPublicKeyInfoPem());
    public string Token(string subject = "staff-a", bool allowed = true, string? name = "Test Staff", string issuer = "https://example.test", string audience = "crm-tickets-tests", bool expired = false)
    {
        var claims = new List<Claim> { new("sub", subject) };
        if (name is not null) claims.Add(new("name", name));
        if (allowed) claims.Add(new("crm_support_queue", "shared"));
        var now = DateTime.UtcNow;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer, audience, claims, now.AddMinutes(-10), expired ? now.AddMinutes(-1) : now.AddMinutes(30), new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)));
    }
    public void Dispose() { rsa.Dispose(); File.Delete(PublicKeyPath); }
}
