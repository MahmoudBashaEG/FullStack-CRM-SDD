using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Domain.Accounts;
using Crm.IntegrationTests.Fixtures;
namespace Crm.IntegrationTests.Authentication;

[Collection("SQL")]
public sealed class AuthEndpointTests
{
    [Theory] [InlineData(AccountRole.Admin)] [InlineData(AccountRole.Agent)] [InlineData(AccountRole.Customer)]
    public async Task Actual_signin_me_refresh_and_signout_enforce_cookie_role_and_context(AccountRole role)
    {
        await using var fixture=await AuthenticationApiFixture.CreateAsync();var password=Guid.NewGuid().ToString("N");var user=await fixture.PrepareAsync(role,password);var token=await fixture.SignInAsync(user,password);
        using var me=await fixture.SendAsync("/api/auth/me",token:token);Assert.Equal(HttpStatusCode.OK,me.StatusCode);var identity=await me.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(user.Id,identity.GetProperty("userId").GetGuid());Assert.Equal(role.ToString(),identity.GetProperty("role").GetString());Assert.Equal(3,identity.EnumerateObject().Count());
        using var queue=await fixture.SendAsync("/api/ticket-reference-data/categories",token:token);Assert.Equal(role==AccountRole.Customer?HttpStatusCode.Forbidden:HttpStatusCode.OK,queue.StatusCode);
        var oldCookie=fixture.Cookies["__Host-crm-refresh"];
        using var refresh=await fixture.SendAsync("/api/auth/refresh",HttpMethod.Post,new{});Assert.Equal(HttpStatusCode.OK,refresh.StatusCode);Assert.NotEqual(oldCookie,fixture.Cookies["__Host-crm-refresh"]);
        var setCookie=Assert.Single(refresh.Headers.GetValues("Set-Cookie"),x=>x.StartsWith("__Host-crm-refresh="));Assert.Contains("secure",setCookie);Assert.Contains("httponly",setCookie);Assert.Contains("samesite=strict",setCookie);Assert.Contains("path=/",setCookie);Assert.DoesNotContain("domain=",setCookie);
        using var signout=await fixture.SendAsync("/api/auth/signout",HttpMethod.Post,new{},token);Assert.Equal(HttpStatusCode.NoContent,signout.StatusCode);Assert.False(fixture.Cookies.ContainsKey("__Host-crm-refresh"));
        using var denied=await fixture.SendAsync("/api/auth/me",token:token);Assert.Equal(HttpStatusCode.Unauthorized,denied.StatusCode);
        using var repeated=await fixture.SendAsync("/api/auth/signout",HttpMethod.Post,new{},token);Assert.Equal(HttpStatusCode.NoContent,repeated.StatusCode);
        using var reload=await fixture.SendAsync("/api/auth/refresh",HttpMethod.Post,new{});Assert.Equal(HttpStatusCode.Unauthorized,reload.StatusCode);Assert.Equal("no-store",reload.Headers.CacheControl!.ToString());
    }
    [Fact] public async Task Csrf_origin_bearer_and_strict_body_boundaries_are_enforced()
    {
        await using var f=await AuthenticationApiFixture.CreateAsync();await f.ObtainCsrfAsync();
        foreach(var origin in new[]{"https://attacker.test","https://localhost:4200.evil"}){using var r=await f.SendAsync("/api/auth/refresh",HttpMethod.Post,new{},origin:origin);Assert.Equal(HttpStatusCode.Forbidden,r.StatusCode);}
        using var missing=await f.SendAsync("/api/auth/refresh",HttpMethod.Post,new{},csrf:false);Assert.Equal(HttpStatusCode.Forbidden,missing.StatusCode);
        using var bearer=await f.SendAsync("/api/auth/refresh",HttpMethod.Post,new{},token:"invalid");Assert.Equal(HttpStatusCode.BadRequest,bearer.StatusCode);
        using var unknown=await f.SendAsync("/api/auth/refresh",HttpMethod.Post,new{unexpected=true});Assert.Equal(HttpStatusCode.BadRequest,unknown.StatusCode);
        using var publicAdmin=await f.SendAsync("/api/auth/bootstrap",HttpMethod.Post,new{});Assert.NotEqual(HttpStatusCode.OK,publicAdmin.StatusCode);
    }
    [Fact] public async Task Bearer_signout_fallback_and_mismatched_live_contexts_do_not_revoke_others()
    {
        await using var f=await AuthenticationApiFixture.CreateAsync();var password=Guid.NewGuid().ToString("N");var user=await f.PrepareAsync(AccountRole.Admin,password);
        var first=await f.SignInAsync(user,password);var firstCookie=f.Cookies["__Host-crm-refresh"];f.Cookies.Remove("__Host-crm-refresh");
        var second=await f.SignInAsync(user,password);
        using var mismatch=await f.SendAsync("/api/auth/signout",HttpMethod.Post,new{},first);Assert.Equal(HttpStatusCode.Conflict,mismatch.StatusCode);
        using var stillFirst=await f.SendAsync("/api/auth/me",token:first);Assert.Equal(HttpStatusCode.OK,stillFirst.StatusCode);
        f.Cookies.Remove("__Host-crm-refresh");using var fallback=await f.SendAsync("/api/auth/signout",HttpMethod.Post,new{},first);Assert.Equal(HttpStatusCode.NoContent,fallback.StatusCode);
        using var other=await f.SendAsync("/api/auth/me",token:second);Assert.Equal(HttpStatusCode.OK,other.StatusCode);
        f.Cookies["__Host-crm-refresh"]=firstCookie;using var revoked=await f.SendAsync("/api/auth/refresh",HttpMethod.Post,new{});Assert.Equal(HttpStatusCode.Unauthorized,revoked.StatusCode);
    }
    [Fact] public async Task Signin_throttle_returns_safe_429_and_retry_after()
    {
        await using var f=await AuthenticationApiFixture.CreateAsync();await f.ObtainCsrfAsync();
        for(var attempt=0;attempt<10;attempt++){f.Client.DefaultRequestHeaders.Remove("X-Forwarded-For");f.Client.DefaultRequestHeaders.Add("X-Forwarded-For","203.0.113."+(attempt+1));using var response=await f.SendAsync("/api/auth/signin",HttpMethod.Post,new{email="missing@fixture.test",password="123456789012345"});Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);}
        using var limited=await f.SendAsync("/api/auth/signin",HttpMethod.Post,new{email="missing@fixture.test",password="123456789012345"});Assert.Equal((HttpStatusCode)429,limited.StatusCode);Assert.NotNull(limited.Headers.RetryAfter);Assert.Equal("rate_limited",(await limited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
    [Fact] public async Task Refresh_limit_is_partitioned_by_trusted_session_and_does_not_consume_rejected_input()
    {
        await using var f=await AuthenticationApiFixture.CreateAsync();var password=Guid.NewGuid().ToString("N");var user=await f.PrepareAsync(AccountRole.Admin,password);await f.SignInAsync(user,password);
        for(var attempt=0;attempt<30;attempt++){using var response=await f.SendAsync("/api/auth/refresh",HttpMethod.Post,new{});Assert.Equal(HttpStatusCode.OK,response.StatusCode);}
        var before=f.Cookies["__Host-crm-refresh"];using var limited=await f.SendAsync("/api/auth/refresh",HttpMethod.Post,new{});Assert.Equal((HttpStatusCode)429,limited.StatusCode);Assert.Equal(before,f.Cookies["__Host-crm-refresh"]);
        f.Cookies.Remove("__Host-crm-refresh");await f.SignInAsync(user,password);using var independent=await f.SendAsync("/api/auth/refresh",HttpMethod.Post,new{});Assert.Equal(HttpStatusCode.OK,independent.StatusCode);
    }
    [Fact] public async Task Raw_duplicate_properties_wrong_media_and_query_credentials_are_rejected()
    {
        await using var f=await AuthenticationApiFixture.CreateAsync();await f.ObtainCsrfAsync();
        using var duplicateRequest=new HttpRequestMessage(HttpMethod.Post,"/api/auth/signin"){Content=new StringContent("{\"email\":\"a@fixture.test\",\"email\":\"b@fixture.test\",\"password\":\"123456789012345\"}",System.Text.Encoding.UTF8,"application/json")};
        using var duplicate=await f.Client.SendAsync(duplicateRequest);Assert.Equal(HttpStatusCode.BadRequest,duplicate.StatusCode);
        using var mediaRequest=new HttpRequestMessage(HttpMethod.Post,"/api/auth/refresh"){Content=new StringContent("{}")};using var media=await f.Client.SendAsync(mediaRequest);Assert.Equal(HttpStatusCode.UnsupportedMediaType,media.StatusCode);
        using var query=await f.SendAsync("/api/auth/csrf?password=not-a-secret");Assert.Equal(HttpStatusCode.BadRequest,query.StatusCode);
        using var noBearer=await f.SendAsync("/api/auth/me");Assert.Equal(HttpStatusCode.Unauthorized,noBearer.StatusCode);
    }
}
