using System.IdentityModel.Tokens.Jwt;
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
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Application;
using Crm.Application.Accounts.Ports;
namespace Crm.IntegrationTests.Fixtures;

public sealed class TestJwtFactory : IDisposable
{
    private readonly LocalAccountFixture signing = new();
    private readonly Dictionary<string,Guid> prepared = [];
    private readonly Dictionary<string,string> signedInTokens = [];
    private string? connection;
    public Func<HttpClient>? SignInClient {get;set;}
    public Guid UserId(string subject="staff-a",bool allowed=true,string? name="Test Staff")=>prepared[subject+"|"+(allowed?AccountRole.Agent:AccountRole.Customer)+"|"+name];
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
        using var provider = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(signing.Configuration(connection)).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var role = roleOverride ?? (allowed ? AccountRole.Agent : AccountRole.Customer);
        var identityKey = subject + "|" + role + "|" + name;
        if (!prepared.ContainsKey(identityKey))
        {
            var user = new LocalUser { DisplayName = string.IsNullOrWhiteSpace(name) ? subject : name, AccountRole = role };
            user.Email = user.UserName = user.Id.ToString("N") + "@fixture.test";
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>();
            var password=Guid.NewGuid().ToString("N");
            var result = await manager.CreateAsync(user,password).ConfigureAwait(false);
            if (!result.Succeeded) throw new InvalidOperationException("Private fixture account setup failed.");
            // Real authentication use case establishes the session; cryptographic negative
            // fixtures alone alter the resulting JWT below. No fake authentication scheme.
            string issued;
            if(SignInClient is not null){
                using var client=SignInClient();client.DefaultRequestHeaders.Add("Origin","https://localhost:4200");using var csrf=await client.GetAsync("/api/auth/csrf").ConfigureAwait(false);csrf.EnsureSuccessStatusCode();var pair=await csrf.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",pair.GetProperty("requestToken").GetString());
                using var login=await client.PostAsJsonAsync("/api/auth/signin",new{email=user.Email,password}).ConfigureAwait(false);login.EnsureSuccessStatusCode();issued=(await login.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false)).GetProperty("accessToken").GetString()!;
            }else{
                // Isolated cryptographic/migration fixtures issue through the same real
                // authentication use case, never a synthetic success scheme.
                var issue=await scope.ServiceProvider.GetRequiredService<IAuthenticationOperations>().SignInAsync(user.Email,password,null,CancellationToken.None).ConfigureAwait(false);if(!issue.Succeeded)throw new InvalidOperationException("Actual fixture signin failed.");issued=issue.Value!.Access.AccessToken;
            }
            prepared.Add(identityKey,user.Id);
            signedInTokens.Add(identityKey,issued);
        }
        var valid = signedInTokens[identityKey];
        if (issuer == "urn:crm:local" && audience == "crm-api" && !expired) return valid;
        // Deliberate cryptographic negative fixtures remain test-only; not another auth scheme.
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(valid);
        parsed.Payload["iss"] = issuer; parsed.Payload["aud"] = audience;
        if (expired) { parsed.Payload["nbf"] = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds(); parsed.Payload["exp"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(); }
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(scope.ServiceProvider.GetRequiredService<SigningCertificateProvider>().Credentials),parsed.Payload));
    }
    public void Dispose() => signing.Dispose();
}
