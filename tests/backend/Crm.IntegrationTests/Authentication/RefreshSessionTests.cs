using System.Net;
using Crm.Domain.Accounts;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
namespace Crm.IntegrationTests.Authentication;

[Collection("SQL")]
public sealed class RefreshSessionTests
{
    [Fact] public async Task Rotation_stores_only_digests_and_replay_revokes_only_its_family()
    {
        await using var f=await AuthenticationApiFixture.CreateAsync();var password=Guid.NewGuid().ToString("N");var user=await f.PrepareAsync(AccountRole.Admin,password);var first=await f.SignInAsync(user,password);var old=f.Cookies["__Host-crm-refresh"];
        using var rotated=await f.SendAsync("/api/auth/refresh",HttpMethod.Post,new{});Assert.Equal(HttpStatusCode.OK,rotated.StatusCode);var successor=f.Cookies["__Host-crm-refresh"];
        f.Cookies.Remove("__Host-crm-refresh");var independent=await f.SignInAsync(user,password);var independentCookie=f.Cookies["__Host-crm-refresh"];
        f.Cookies["__Host-crm-refresh"]=old;using var replay=await f.SendAsync("/api/auth/refresh",HttpMethod.Post,new{});Assert.Equal(HttpStatusCode.Unauthorized,replay.StatusCode);
        using var denied=await f.SendAsync("/api/auth/me",token:first);Assert.Equal(HttpStatusCode.Unauthorized,denied.StatusCode);
        f.Cookies["__Host-crm-refresh"]=successor;using var successorDenied=await f.SendAsync("/api/auth/refresh",HttpMethod.Post,new{});Assert.Equal(HttpStatusCode.Unauthorized,successorDenied.StatusCode);
        using var survives=await f.SendAsync("/api/auth/me",token:independent);Assert.Equal(HttpStatusCode.OK,survives.StatusCode);
        f.Cookies["__Host-crm-refresh"]=independentCookie;
        await using var db=f.Sql.OpenContext();var credentials=await db.RefreshCredentials.ToListAsync();Assert.Equal(3,credentials.Count);Assert.All(credentials,x=>Assert.Equal(32,x.TokenHash.Length));Assert.All(await db.AuthenticationSessions.ToListAsync(),x=>Assert.Equal(TimeSpan.FromDays(7),x.ExpiresAtUtc-x.CreatedAtUtc));
    }
    [Fact] public async Task Simultaneous_refresh_has_one_successor_and_replay_rejection()
    {
        await using var f=await AuthenticationApiFixture.CreateAsync();var password=Guid.NewGuid().ToString("N");await f.SignInAsync(await f.PrepareAsync(AccountRole.Agent,password),password);
        var results=await Task.WhenAll(f.SendAsync("/api/auth/refresh",HttpMethod.Post,new{},updateCookies:false),f.SendAsync("/api/auth/refresh",HttpMethod.Post,new{},updateCookies:false));
        Assert.Single(results,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Unauthorized);foreach(var r in results)r.Dispose();
        await using var db=f.Sql.OpenContext();Assert.Equal(2,await db.RefreshCredentials.CountAsync());Assert.NotNull((await db.AuthenticationSessions.SingleAsync()).RevokedAtUtc);
    }
}
