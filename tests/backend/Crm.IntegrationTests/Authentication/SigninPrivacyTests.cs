using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Domain.Accounts;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
namespace Crm.IntegrationTests.Authentication;

[Collection("SQL")]
public sealed class SigninPrivacyTests
{
    [Fact] public async Task Wrong_nonexistent_disabled_are_identical_and_failed_signin_preserves_context()
    {
        await using var f=await AuthenticationApiFixture.CreateAsync();var password="  Spaces عربي exact  ";var active=await f.PrepareAsync(AccountRole.Admin,password);var disabled=await f.PrepareAsync(AccountRole.Agent,password,false);var token=await f.SignInAsync(active,password);var cookie=f.Cookies["__Host-crm-refresh"];
        foreach(var email in new[]{active.Email,disabled.Email,"absent@fixture.test"})
        {
            using var failed=await f.SendAsync("/api/auth/signin",HttpMethod.Post,new{email,password=email==active.Email?password.Trim():password});Assert.Equal(HttpStatusCode.Unauthorized,failed.StatusCode);
            var problem=await failed.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("invalid_credentials",problem.GetProperty("code").GetString());Assert.Equal("Email or password is incorrect.",problem.GetProperty("title").GetString());Assert.DoesNotContain(password,problem.ToString());Assert.Equal(cookie,f.Cookies["__Host-crm-refresh"]);
        }
        using var me=await f.SendAsync("/api/auth/me",token:token);Assert.Equal(HttpStatusCode.OK,me.StatusCode);
        await using var db=f.Sql.OpenContext();var user=await db.Users.SingleAsync(x=>x.Id==active.Id);user.IsActive=false;await db.SaveChangesAsync();
        using var denied=await f.SendAsync("/api/auth/me",token:token);Assert.Equal(HttpStatusCode.Unauthorized,denied.StatusCode);using var refresh=await f.SendAsync("/api/auth/refresh",HttpMethod.Post,new{});Assert.Equal(HttpStatusCode.Unauthorized,refresh.StatusCode);
        Assert.True(f.Logs.Messages.All(message=>!message.Contains(password,StringComparison.Ordinal)&&!message.Contains(token,StringComparison.Ordinal)&&!message.Contains(cookie,StringComparison.Ordinal)),"Authentication material appeared in captured logs.");
    }
}
