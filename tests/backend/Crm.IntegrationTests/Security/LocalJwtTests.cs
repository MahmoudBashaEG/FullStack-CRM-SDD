using System.IdentityModel.Tokens.Jwt;
using Crm.Application.Accounts.Contracts;
using Crm.Application.Authentication.Contracts;
using Crm.Application.Authentication.Ports;
using Crm.Domain.Accounts;
using Crm.Infrastructure;
using Crm.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
namespace Crm.IntegrationTests.Security;

[Collection("SQL")]
public sealed class LocalJwtTests(SqlServerFixture sql)
{
    [Fact] public void Shorter_session_lifetime_caps_access_expiry_and_cancellation_is_propagated()
    {
        using var fixture=new LocalAccountFixture();using var provider=new ServiceCollection().AddLogging().AddInfrastructure(fixture.Configuration(sql.Connection)).BuildServiceProvider();using var scope=provider.CreateScope();
        var issuer=scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();var expires=DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeSeconds());
        var session=new TrustedAuthentication(new(Guid.NewGuid(),"Private",AccountRole.Admin),Guid.NewGuid(),1,expires);
        Assert.Equal(expires,issuer.Issue(session,CancellationToken.None).ExpiresAtUtc);
        Assert.Throws<OperationCanceledException>(()=>issuer.Issue(session,new CancellationToken(true)));
    }
    [Fact] public void Configured_prior_public_key_allows_bounded_key_rollover()
    {
        using var prior=new LocalAccountFixture();using var current=new LocalAccountFixture();
        var values=current.Configuration(sql.Connection).AsEnumerable().ToDictionary(x=>x.Key,x=>x.Value);values["Jwt:PreviousValidationCertificateThumbprints:0"]=prior.Certificate.Thumbprint;
        using var provider=new ServiceCollection().AddLogging().AddInfrastructure(new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(values).Build()).BuildServiceProvider();
        Assert.Equal(2,provider.GetRequiredService<Crm.Infrastructure.Authentication.SigningCertificateProvider>().ValidationKeys.Count);
    }
    [Fact] public void Tokens_are_short_lived_minimal_local_and_signed()
    {
        using var fixture = new LocalAccountFixture();
        using var provider = new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System).AddInfrastructure(fixture.Configuration(sql.Connection)).BuildServiceProvider();
        using var scope = provider.CreateScope(); var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
        var start = DateTimeOffset.UtcNow;
        var result = issuer.Issue(new(new(Guid.NewGuid(), "Do not put my name in JWT", AccountRole.Agent),Guid.NewGuid(),1,start.AddDays(7)),CancellationToken.None);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Assert.Equal("RS256",token.Header.Alg); Assert.Equal("urn:crm:local",token.Issuer); Assert.Equal(["crm-api"],token.Audiences);
        Assert.Equal(TimeSpan.FromMinutes(5),token.ValidTo-token.ValidFrom);
        Assert.Equal(new DateTimeOffset(token.ValidTo),result.ExpiresAtUtc);
        Assert.DoesNotContain(token.Claims,x=>x.Type is "name" or "email" or "password" or "passwordHash" or "refreshToken");
        Assert.Equal("Agent", token.Claims.Single(x=>x.Type=="role").Value);
    }
    [Fact] public void Current_authentication_reader_is_shared_infrastructure()
    {
        using var fixture = new LocalAccountFixture();
        using var provider = new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System).AddInfrastructure(fixture.Configuration(sql.Connection)).BuildServiceProvider();
        using var scope = provider.CreateScope(); Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICurrentAuthenticationReader>());
    }
}
