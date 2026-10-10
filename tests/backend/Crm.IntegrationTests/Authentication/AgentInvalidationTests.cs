using System.Net;
using System.Net.Http.Json;
using Crm.Domain.Accounts;
using Crm.Application.Accounts.Contracts;
using Crm.IntegrationTests.Accounts;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
namespace Crm.IntegrationTests.Authentication;
[Collection("SQL")]
public sealed class AgentInvalidationTests
{
    [Fact] public async Task Disable_revokes_all_live_families_and_reenable_requires_fresh_signin()
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();var admin=await api.PrepareAsync(AccountRole.Admin,AgentTestSupport.Password);var adminToken=await api.SignInAsync(admin,AgentTestSupport.Password);var agent=await AgentTestSupport.CreateAsync(api,adminToken);
        await using var db=api.Sql.OpenContext();var user=await db.Users.AsNoTracking().SingleAsync(x=>x.Id==agent.UserId);api.Cookies.Clear();var first=await api.SignInAsync(user,AgentTestSupport.Password);var firstCookie=api.Cookies["__Host-crm-refresh"];api.Cookies.Clear();var second=await api.SignInAsync(user,AgentTestSupport.Password);var secondCookie=api.Cookies["__Host-crm-refresh"];
        foreach(var token in new[]{first,second}){using var live=await api.SendAsync("/api/auth/me",token:token);Assert.Equal(HttpStatusCode.OK,live.StatusCode);}
        using var disable=await api.SendAsync($"/api/agents/{agent.UserId}/availability",HttpMethod.Put,new{isActive=false,version=agent.Version},adminToken);Assert.Equal(HttpStatusCode.OK,disable.StatusCode);var disabled=(await disable.Content.ReadFromJsonAsync<AgentAccountDto>())!;
        foreach(var token in new[]{first,second}){Assert.True(new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo>DateTime.UtcNow);using var denied=await api.SendAsync("/api/auth/me",token:token);Assert.Equal(HttpStatusCode.Unauthorized,denied.StatusCode);}
        foreach(var cookie in new[]{firstCookie,secondCookie}){api.Cookies["__Host-crm-refresh"]=cookie;using var refresh=await api.SendAsync("/api/auth/refresh",HttpMethod.Post,new{});Assert.Equal(HttpStatusCode.Unauthorized,refresh.StatusCode);}
        using var signin=await api.SendAsync("/api/auth/signin",HttpMethod.Post,new{email=agent.Email,password=AgentTestSupport.Password});Assert.Equal(HttpStatusCode.Unauthorized,signin.StatusCode);
        var stored=await db.Users.AsNoTracking().SingleAsync(x=>x.Id==agent.UserId);Assert.False(stored.IsActive);Assert.Equal(2,stored.AuthVersion);Assert.NotEqual(user.SecurityStamp,stored.SecurityStamp);Assert.False((await db.Agents.AsNoTracking().SingleAsync()).IsEligible);Assert.All(await db.AuthenticationSessions.AsNoTracking().Where(x=>x.UserId==agent.UserId).ToListAsync(),x=>Assert.NotNull(x.RevokedAtUtc));
        using var enable=await api.SendAsync($"/api/agents/{agent.UserId}/availability",HttpMethod.Put,new{isActive=true,version=disabled.Version},adminToken);Assert.Equal(HttpStatusCode.OK,enable.StatusCode);Assert.Equal(agent.AssignmentReferenceId,(await enable.Content.ReadFromJsonAsync<AgentAccountDto>())!.AssignmentReferenceId);
        using var old=await api.SendAsync("/api/auth/me",token:first);Assert.Equal(HttpStatusCode.Unauthorized,old.StatusCode);api.Cookies["__Host-crm-refresh"]=firstCookie;using var oldRefresh=await api.SendAsync("/api/auth/refresh",HttpMethod.Post,new{});Assert.Equal(HttpStatusCode.Unauthorized,oldRefresh.StatusCode);
        api.Cookies.Clear();var fresh=await api.SignInAsync(stored,AgentTestSupport.Password);using var current=await api.SendAsync("/api/auth/me",token:fresh);Assert.Equal(HttpStatusCode.OK,current.StatusCode);Assert.True((await db.Agents.AsNoTracking().SingleAsync()).IsEligible);
    }
    [Fact] public async Task Disable_racing_refresh_or_signin_cannot_leave_usable_authentication()
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();var adminToken=await api.SignInAsync(await api.PrepareAsync(AccountRole.Admin,AgentTestSupport.Password),AgentTestSupport.Password);var agent=await AgentTestSupport.CreateAsync(api,adminToken);
        await using var db=api.Sql.OpenContext();var user=await db.Users.SingleAsync(x=>x.Id==agent.UserId);api.Cookies.Clear();var old=await api.SignInAsync(user,AgentTestSupport.Password);
        var responses=await Task.WhenAll(api.SendAsync($"/api/agents/{agent.UserId}/availability",HttpMethod.Put,new{isActive=false,version=agent.Version},adminToken,updateCookies:false),api.SendAsync("/api/auth/refresh",HttpMethod.Post,new{},updateCookies:false),api.SendAsync("/api/auth/signin",HttpMethod.Post,new{email=agent.Email,password=AgentTestSupport.Password},updateCookies:false));
        try{Assert.Equal(HttpStatusCode.OK,responses[0].StatusCode);foreach(var response in responses.Skip(1)){Assert.Contains(response.StatusCode,new[]{HttpStatusCode.OK,HttpStatusCode.Unauthorized});if(response.StatusCode==HttpStatusCode.OK){var json=await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();using var denied=await api.SendAsync("/api/auth/me",token:json.GetProperty("accessToken").GetString());Assert.Equal(HttpStatusCode.Unauthorized,denied.StatusCode);}}}finally{foreach(var response in responses)response.Dispose();}
        using var earlier=await api.SendAsync("/api/auth/me",token:old);Assert.Equal(HttpStatusCode.Unauthorized,earlier.StatusCode);Assert.All(await db.AuthenticationSessions.AsNoTracking().Where(x=>x.UserId==agent.UserId).ToListAsync(),session=>Assert.NotNull(session.RevokedAtUtc));
    }
}
