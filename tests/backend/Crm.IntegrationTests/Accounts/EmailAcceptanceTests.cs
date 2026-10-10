using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Domain.Accounts;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
namespace Crm.IntegrationTests.Accounts;

[Collection("SQL")]
public sealed class EmailAcceptanceTests
{
    [Theory]
    [InlineData(false, "ordinary@example.test")]
    [InlineData(false, "\"multi part\"@example.test")]
    [InlineData(true, "ordinary@example.test")]
    [InlineData(true, "\"multi part\"@example.test")]
    [InlineData(false, "ordinary@example.test", "\u0085")]
    [InlineData(false, "\"multi part\"@example.test", "\u0085")]
    [InlineData(true, "ordinary@example.test", "\u0085")]
    [InlineData(true, "\"multi part\"@example.test", "\u0085")]
    public async Task Creation_preserves_supported_email_and_normalized_uniqueness(bool agent, string email, string whitespace="  ")
    {
        const string password="  عربي password  ";
        await using var api=await AuthenticationApiFixture.CreateAsync();
        string? adminToken=agent?await api.SignInAsync(await api.PrepareAsync(AccountRole.Admin,password),password):null;
        await api.ObtainCsrfAsync();
        var path=agent?"/api/agents":"/api/auth/signup";
        using var created=await api.SendAsync(path,HttpMethod.Post,new{displayName=" User عربي ",email=whitespace+email+whitespace,password},adminToken);
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);Assert.False(created.Headers.Contains("Set-Cookie"));
        await using var db=api.Sql.OpenContext();
        var user=await db.Users.SingleAsync(x=>x.Email==email);
        Assert.Equal(agent?AccountRole.Agent:AccountRole.Customer,user.AccountRole);Assert.True(user.IsActive);Assert.Equal(email.Normalize().ToUpperInvariant(),user.NormalizedEmail);
        Assert.Equal(agent?1:0,await db.Agents.CountAsync(x=>x.LocalUserId==user.Id));
        Assert.Empty(await db.AuthenticationSessions.Where(x=>x.UserId==user.Id).ToListAsync());
        using var duplicate=await api.SendAsync(path,HttpMethod.Post,new{displayName="Other",email=whitespace+email.ToUpperInvariant()+whitespace,password},adminToken);
        Assert.Equal(agent?HttpStatusCode.Conflict:HttpStatusCode.BadRequest,duplicate.StatusCode);
        Assert.Equal(agent?"agent_conflict":"registration_failed",(await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var invalid=await api.SendAsync(path,HttpMethod.Post,new{displayName="Other",email="Name <invalid@example.test>",password},adminToken);
        Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
        using var internalControl=await api.SendAsync(path,HttpMethod.Post,new{displayName="Other",email="user\u0085@example.test",password},adminToken);Assert.Equal(HttpStatusCode.BadRequest,internalControl.StatusCode);
        Assert.Equal(agent?2:1,await db.Users.CountAsync());
        var token=await api.SignInAsync(user,password);
        using var me=await api.SendAsync("/api/auth/me",token:token);Assert.Equal(HttpStatusCode.OK,me.StatusCode);
        Assert.Equal(agent?"Agent":"Customer",(await me.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());
        using var forbidden=await api.SendAsync("/api/agents",HttpMethod.Post,new{displayName="Denied",email="denied@example.test",password},token);
        Assert.Equal(HttpStatusCode.Forbidden,forbidden.StatusCode);
    }
}
