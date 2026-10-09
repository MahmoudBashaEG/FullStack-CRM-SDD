using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography.X509Certificates;
using Crm.Application.Accounts.Contracts;
using Crm.Application.Authentication.Contracts;
using Crm.Application.Authentication.Ports;
using Crm.Domain.Accounts;
using Crm.Infrastructure;
using Crm.Infrastructure.Authentication;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
namespace Crm.IntegrationTests.Fixtures;

public sealed class TestJwtFactory : IDisposable
{
    private readonly LocalAccountFixture signing = new();
    private readonly Dictionary<string,TrustedAuthentication> prepared = [];
    private string? connection;
    public string PublicKeyPath { get; } = Path.Combine(Path.GetTempPath(), $"crm-public-{Guid.NewGuid():N}.pem");
    public TestJwtFactory()
    {
        using var key = signing.Certificate.GetRSAPublicKey()!;
        File.WriteAllText(PublicKeyPath,key.ExportSubjectPublicKeyInfoPem());
    }
    public IConfiguration Configuration(string sql) => signing.Configuration(sql);
    public void Bind(string sql) => connection = sql;
    public string Token(string subject = "staff-a", bool allowed = true, string? name = "Test Staff", string issuer = "urn:crm:local", string audience = "crm-api", bool expired = false)
        => Task.Run(() => TokenAsync(subject,allowed,name,issuer,audience,expired)).GetAwaiter().GetResult();
    public string TokenForRole(AccountRole role) => Task.Run(() => TokenAsync("role-test",true,"Role Test","urn:crm:local","crm-api",false,role)).GetAwaiter().GetResult();
    public string AlterSignedToken(string token, string claim, object? value)
    {
        var parsed=new JwtSecurityTokenHandler().ReadJwtToken(token);
        if (value is null) parsed.Payload.Remove(claim); else parsed.Payload[claim]=value;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(new Microsoft.IdentityModel.Tokens.SigningCredentials(new Microsoft.IdentityModel.Tokens.X509SecurityKey(signing.Certificate){KeyId=signing.Certificate.Thumbprint},Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256)),parsed.Payload));
    }
    private async Task<string> TokenAsync(string subject, bool allowed, string? name, string issuer, string audience, bool expired, AccountRole? roleOverride = null)
    {
        if (connection is null) throw new InvalidOperationException("Bind a private SQL fixture before issuing local tokens.");
        using var provider = new ServiceCollection().AddLogging().AddInfrastructure(signing.Configuration(connection)).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var role = roleOverride ?? (allowed ? AccountRole.Agent : AccountRole.Customer);
        var identityKey = subject + "|" + role + "|" + name;
        if (!prepared.TryGetValue(identityKey,out var authentication))
        {
            var user = new LocalUser { DisplayName = string.IsNullOrWhiteSpace(name) ? subject : name, AccountRole = role };
            user.Email = user.UserName = user.Id.ToString("N") + "@fixture.test";
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>();
            var result = await manager.CreateAsync(user,Guid.NewGuid().ToString("N")).ConfigureAwait(false);
            if (!result.Succeeded) throw new InvalidOperationException("Private fixture account setup failed.");
            var session = new AuthenticationSession { UserId=user.Id, AuthVersionAtCreation=user.AuthVersion, CreatedAtUtc=DateTimeOffset.UtcNow, ExpiresAtUtc=DateTimeOffset.UtcNow.AddDays(7) };
            var database = scope.ServiceProvider.GetRequiredService<CrmDbContext>(); database.AuthenticationSessions.Add(session); await database.SaveChangesAsync().ConfigureAwait(false);
            authentication = new(new(user.Id,user.DisplayName,user.AccountRole),session.Id,user.AuthVersion,session.ExpiresAtUtc); prepared.Add(identityKey,authentication);
        }
        var valid = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>().Issue(authentication,CancellationToken.None);
        if (issuer == "urn:crm:local" && audience == "crm-api" && !expired) return valid.AccessToken;
        // Deliberate cryptographic negative fixtures remain test-only; not another auth scheme.
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(valid.AccessToken);
        parsed.Payload["iss"] = issuer; parsed.Payload["aud"] = audience;
        if (expired) { parsed.Payload["nbf"] = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds(); parsed.Payload["exp"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(); }
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(scope.ServiceProvider.GetRequiredService<SigningCertificateProvider>().Credentials),parsed.Payload));
    }
    public void Dispose() { signing.Dispose(); File.Delete(PublicKeyPath); }
}
