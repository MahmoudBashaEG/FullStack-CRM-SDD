using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Crm.Api.Authorization;
using Crm.Domain.Accounts;
using Crm.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Crm.IntegrationTests.Security;

[Collection("SQL")]
public sealed class LocalAuthorizationFoundationTests(SqlServerFixture sql)
{
    [Theory] [InlineData("sub")] [InlineData("sid")] [InlineData("av")] [InlineData("role")] [InlineData("jti")] [InlineData("iat")] [InlineData("nbf")]
    public async Task Signed_tokens_missing_required_identity_claims_are_rejected(string claim)
    {
        using var jwt=new TestJwtFactory();await using var api=new CrmApiFactory(sql,jwt);using var client=api.CreateClient();client.DefaultRequestHeaders.Authorization=new("Bearer",jwt.AlterSignedToken(jwt.Token(),claim,null));
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);
    }
    [Fact] public async Task Account_state_database_failure_is_503_not_success_or_invalid_credentials()
    {
        using var jwt=new TestJwtFactory();
        var missing=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sql.Connection){InitialCatalog="CrmTicketsIntegration_"+Guid.NewGuid().ToString("N")};
        await using var api=new CrmApiFactory(sql,jwt,new Dictionary<string,string?>{["ConnectionStrings:Crm"]=missing.ConnectionString});using var client=api.CreateClient();client.DefaultRequestHeaders.Authorization=new("Bearer",jwt.Token());
        var result=await client.GetAsync("/api/ticket-reference-data/categories");Assert.Equal(HttpStatusCode.ServiceUnavailable,result.StatusCode);Assert.Contains("service_unavailable",await result.Content.ReadAsStringAsync());
    }
    [Theory] [InlineData(AccountRole.Customer,403)] [InlineData(AccountRole.Agent,200)] [InlineData(AccountRole.Admin,200)]
    public async Task Shared_queue_uses_authoritative_local_role(AccountRole role,int status)
    {
        using var jwt=new TestJwtFactory();await using var api=new CrmApiFactory(sql,jwt);using var client=api.CreateClient();client.DefaultRequestHeaders.Authorization=new("Bearer",jwt.TokenForRole(role));
        Assert.Equal(status,(int)(await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);
    }
    [Fact] public async Task State_is_checked_on_every_request_and_reenable_does_not_revive_old_token()
    {
        using var jwt=new TestJwtFactory();await using var api=new CrmApiFactory(sql,jwt);using var client=api.CreateClient();var token=jwt.Token();client.DefaultRequestHeaders.Authorization=new("Bearer",token);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);
        var id=Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(x=>x.Type=="sub").Value);
        await using(var db=sql.OpenContext()){var user=await db.Users.SingleAsync(x=>x.Id==id);user.IsActive=false;user.AuthVersion++;await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);
        await using(var db=sql.OpenContext()){(await db.Users.SingleAsync(x=>x.Id==id)).IsActive=true;await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);
    }
    [Fact] public async Task Revoked_session_is_denied_while_an_independent_session_survives()
    {
        using var jwt=new TestJwtFactory();await using var api=new CrmApiFactory(sql,jwt);using var client=api.CreateClient();var token=jwt.Token();var other=jwt.Token("other");
        var session=Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(x=>x.Type=="sid").Value);
        await using(var db=sql.OpenContext()){(await db.AuthenticationSessions.SingleAsync(x=>x.Id==session)).RevokedAtUtc=DateTimeOffset.UtcNow;await db.SaveChangesAsync();}
        client.DefaultRequestHeaders.Authorization=new("Bearer",token);Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);
        client.DefaultRequestHeaders.Authorization=new("Bearer",other);Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);
    }
    [Fact] public async Task Missing_expired_wrong_signature_issuer_and_audience_are_safe_401()
    {
        using var jwt=new TestJwtFactory();await using var api=new CrmApiFactory(sql,jwt);using var client=api.CreateClient();using var outsider=new TestJwtFactory();outsider.Bind(sql.Connection);
        foreach(var token in new string?[]{null,jwt.Token(expired:true),jwt.Token(issuer:"urn:other"),jwt.Token(audience:"other"),outsider.Token()})
        {
            client.DefaultRequestHeaders.Authorization=token is null?null:new("Bearer",token);
            var response=await client.GetAsync("/api/ticket-reference-data/categories"); Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
            Assert.Equal("application/problem+json",response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("no-store",response.Headers.CacheControl?.ToString());var body=await response.Content.ReadAsStringAsync();if(token is not null)Assert.DoesNotContain(token,body);
        }
    }
    [Theory] [InlineData(AccountRole.Customer,false)] [InlineData(AccountRole.Agent,false)] [InlineData(AccountRole.Admin,true)]
    public async Task Admin_policy_is_reusable_and_does_not_grant_agent_privileges(AccountRole role,bool expected)
    {
        using var jwt=new TestJwtFactory();await using var api=new CrmApiFactory(sql,jwt);using var client=api.CreateClient();var token=new JwtSecurityTokenHandler().ReadJwtToken(jwt.TokenForRole(role));
        var principal=new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(token.Claims,"fixture","sub","role"));
        using var scope=api.Services.CreateScope();var authorization=scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        Assert.Equal(expected,(await authorization.AuthorizeAsync(principal,null,CrmPolicies.AdminAgents)).Succeeded);
    }
}
