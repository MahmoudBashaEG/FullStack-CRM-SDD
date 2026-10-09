using Crm.Api.Authorization;
using Crm.Infrastructure;
using Crm.Infrastructure.Authentication;
using Crm.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Crm.IntegrationTests.Security;

[Collection("SQL")]
public sealed class AuthConfidentialityTests(SqlServerFixture sql)
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Expired_or_weak_certificate_is_rejected(bool expired)
    {
        using var fixture=new LocalAccountFixture(expired?3072:2048,expired?DateTimeOffset.UtcNow.AddDays(-1):null);
        using var provider=new ServiceCollection().AddLogging().AddInfrastructure(fixture.Configuration(sql.Connection)).BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(()=>provider.GetRequiredService<SigningCertificateProvider>());
    }
    [Fact] public void Missing_signing_config_fails_without_disclosing_credentials()
    {
        using var logs=new AuthLogCapture();using var services=new ServiceCollection().AddLogging(x=>x.AddProvider(logs)).AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:Crm"]=sql.Connection}).Build()).BuildServiceProvider();
        var error=Assert.Throws<InvalidOperationException>(()=>services.GetRequiredService<SigningCertificateProvider>());
        Assert.Equal("Required JWT configuration is missing.",error.Message);Assert.DoesNotContain(logs.Messages,x=>x.Contains("Password=",StringComparison.OrdinalIgnoreCase));
    }
    [Fact] public void Unknown_signer_is_not_generated_or_exported_as_fallback()
    {
        using var fixture=new LocalAccountFixture();var values=fixture.Configuration(sql.Connection).AsEnumerable().ToDictionary(x=>x.Key,x=>x.Value);values["Jwt:SigningCertificateThumbprint"]=new string('0',40);
        using var provider=new ServiceCollection().AddLogging().AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(values).Build()).BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(()=>provider.GetRequiredService<SigningCertificateProvider>());
    }
    [Fact] public async Task Invalid_tokens_never_echo_credentials_or_tokens()
    {
        using var jwt=new TestJwtFactory();await using var api=new CrmApiFactory(sql,jwt);using var client=api.CreateClient();var token=jwt.Token(expired:true);client.DefaultRequestHeaders.Authorization=new("Bearer",token);
        var response=await client.GetAsync("/api/ticket-reference-data/categories");var body=await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(token,body);Assert.DoesNotContain("PasswordHash",body);Assert.DoesNotContain("SecurityStamp",body);Assert.Equal(401,(int)response.StatusCode);
    }
}
