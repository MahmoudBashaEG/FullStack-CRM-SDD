using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Application.Accounts.Contracts;
using Crm.Domain.Accounts;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
namespace Crm.IntegrationTests.Accounts;
[Collection("SQL")]
public sealed class AgentEndpointTests
{
    [Fact] public async Task Existing_Customer_or_Admin_cannot_be_promoted_or_managed_as_an_Agent()
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();var admin=await api.PrepareAsync(AccountRole.Admin,AgentTestSupport.Password);var customer=await api.PrepareAsync(AccountRole.Customer,AgentTestSupport.Password);var token=await api.SignInAsync(admin,AgentTestSupport.Password);await using var db=api.Sql.OpenContext();var before=JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x=>x.Id).ToListAsync());
        foreach(var user in new[]{admin,customer}){
            using var conflict=await api.SendAsync("/api/agents",HttpMethod.Post,new{displayName="Agent",email=" "+user.Email!.ToUpperInvariant()+" ",password=AgentTestSupport.Password},token);Assert.Equal(HttpStatusCode.Conflict,conflict.StatusCode);
            using var rename=await api.SendAsync($"/api/agents/{user.Id}",HttpMethod.Patch,new{displayName="Changed",version=Convert.ToBase64String(user.RowVersion)},token);Assert.Equal(HttpStatusCode.NotFound,rename.StatusCode);
            using var disable=await api.SendAsync($"/api/agents/{user.Id}/availability",HttpMethod.Put,new{isActive=false,version=Convert.ToBase64String(user.RowVersion)},token);Assert.Equal(HttpStatusCode.NotFound,disable.StatusCode);
        }
        Assert.Equal(before,JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x=>x.Id).ToListAsync()));Assert.Empty(await db.Agents.ToListAsync());
    }
    [Fact] public async Task Listing_orders_and_pages_active_and_disabled_Agents_and_handles_large_pages()
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();var token=await api.SignInAsync(await api.PrepareAsync(AccountRole.Admin,AgentTestSupport.Password),AgentTestSupport.Password);
        var z=await AgentTestSupport.CreateAsync(api,token,"Zulu");var a=await AgentTestSupport.CreateAsync(api,token,"Alpha");var another=await AgentTestSupport.CreateAsync(api,token,"Alpha");
        using var disable=await api.SendAsync($"/api/agents/{z.UserId}/availability",HttpMethod.Put,new{isActive=false,version=z.Version},token);Assert.Equal(HttpStatusCode.OK,disable.StatusCode);
        using var first=await api.SendAsync("/api/agents?page=1&pageSize=2",token:token);Assert.Equal(HttpStatusCode.OK,first.StatusCode);var page=(await first.Content.ReadFromJsonAsync<AgentPage>())!;Assert.Equal(3,page.TotalCount);
        // SQL Server uniqueidentifier ordering is the SqlGuid byte order.
        var expected=new[]{a.UserId,another.UserId}.OrderBy(x=>new System.Data.SqlTypes.SqlGuid(x));Assert.Equal(expected,page.Items.Select(x=>x.UserId));
        using var second=await api.SendAsync("/api/agents?page=2&pageSize=2",token:token);var tail=(await second.Content.ReadFromJsonAsync<AgentPage>())!;Assert.Equal(z.UserId,Assert.Single(tail.Items).UserId);Assert.False(tail.Items[0].IsActive);
        using var huge=await api.SendAsync("/api/agents?page=2147483647&pageSize=100",token:token);Assert.Equal(HttpStatusCode.OK,huge.StatusCode);Assert.Empty((await huge.Content.ReadFromJsonAsync<AgentPage>())!.Items);
    }
    [Theory] [InlineData(AccountRole.Customer)] [InlineData(AccountRole.Agent)]
    public async Task All_routes_deny_non_Admin_before_payload_or_existence(AccountRole role)
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();var user=await api.PrepareAsync(role,AgentTestSupport.Password);var token=await api.SignInAsync(user,AgentTestSupport.Password);var id=Guid.NewGuid();
        foreach(var (path,method,body) in new(string,HttpMethod,object?)[]{("/api/agents",HttpMethod.Get,null),("/api/agents",HttpMethod.Post,new{role="Admin"}),($"/api/agents/{id}",HttpMethod.Get,null),($"/api/agents/{id}",HttpMethod.Patch,new{email="forbidden"}),($"/api/agents/{id}/availability",HttpMethod.Put,new{isActive=false})}){using var denied=await api.SendAsync(path,method,body,token);Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);}
        using var visitor=await api.SendAsync("/api/agents");Assert.Equal(HttpStatusCode.Unauthorized,visitor.StatusCode);
        await using var db=api.Sql.OpenContext();Assert.Single(await db.Users.ToListAsync());Assert.Empty(await db.Agents.ToListAsync());
    }
    [Fact] public async Task Create_Location_detail_list_and_updates_expose_only_safe_Agent_data()
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();var admin=await api.PrepareAsync(AccountRole.Admin,AgentTestSupport.Password);var token=await api.SignInAsync(admin,AgentTestSupport.Password);await api.PrepareAsync(AccountRole.Customer,AgentTestSupport.Password);
        using var response=await api.SendAsync("/api/agents",HttpMethod.Post,new{displayName="Agent",email="agent@fixture.test",password=AgentTestSupport.Password},token);
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);var json=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(new[]{"assignmentReferenceId","displayName","email","isActive","role","userId","version"},json.EnumerateObject().Select(x=>x.Name).Order().ToArray());Assert.Equal("Agent",json.GetProperty("role").GetString());Assert.False(response.Headers.Contains("Set-Cookie"));var agent=JsonSerializer.Deserialize<AgentAccountDto>(json.GetRawText(),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;Assert.Equal($"/api/agents/{agent.UserId}",response.Headers.Location!.ToString());
        using var detail=await api.SendAsync(response.Headers.Location.ToString(),token:token);Assert.Equal(HttpStatusCode.OK,detail.StatusCode);Assert.Equal(agent,await detail.Content.ReadFromJsonAsync<AgentAccountDto>());
        using var list=await api.SendAsync("/api/agents?page=1&pageSize=1",token:token);Assert.Equal(HttpStatusCode.OK,list.StatusCode);var page=await list.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(1,page.GetProperty("totalCount").GetInt32());Assert.Single(page.GetProperty("items").EnumerateArray());Assert.Equal("no-store",list.Headers.CacheControl!.ToString());
        using var renamed=await api.SendAsync($"/api/agents/{agent.UserId}",HttpMethod.Patch,new{displayName="New name",version=agent.Version},token);Assert.Equal(HttpStatusCode.OK,renamed.StatusCode);
        using var stale=await api.SendAsync($"/api/agents/{agent.UserId}",HttpMethod.Patch,new{displayName="New name",version=agent.Version},token);Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode);Assert.Equal("account_changed",(await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var duplicate=await api.SendAsync("/api/agents",HttpMethod.Post,new{displayName="Other",email=" AGENT@fixture.test ",password=AgentTestSupport.Password},token);Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);Assert.Equal("agent_conflict",(await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.DoesNotContain(api.Logs.Messages,x=>x.Contains(AgentTestSupport.Password,StringComparison.Ordinal));
    }
    [Theory] [InlineData("?page=0")] [InlineData("?pageSize=0")] [InlineData("?pageSize=101")] [InlineData("?page=bad")] [InlineData("?search=unexpected")] [InlineData("?page=1&page=2")]
    public async Task Invalid_or_unsupported_paging_is_400(string query)
    {await using var api=await AuthenticationApiFixture.CreateAsync();var token=await api.SignInAsync(await api.PrepareAsync(AccountRole.Admin,AgentTestSupport.Password),AgentTestSupport.Password);using var response=await api.SendAsync("/api/agents"+query,token:token);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);}
    [Fact] public async Task Forbidden_operations_and_non_Agent_targets_leave_accounts_unchanged()
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();var admin=await api.PrepareAsync(AccountRole.Admin,AgentTestSupport.Password);var token=await api.SignInAsync(admin,AgentTestSupport.Password);var agent=await AgentTestSupport.CreateAsync(api,token);await using var db=api.Sql.OpenContext();var before=JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x=>x.Id).ToListAsync());
        foreach(var body in new object[]{new{displayName="Changed",version=agent.Version,email="other@fixture.test"},new{displayName="Changed",version=agent.Version,password="new password value"},new{displayName="Changed",version=agent.Version,role="Admin"}}){using var rejected=await api.SendAsync($"/api/agents/{agent.UserId}",HttpMethod.Patch,body,token);Assert.Equal(HttpStatusCode.BadRequest,rejected.StatusCode);}
        using var overrideRole=await api.SendAsync("/api/agents",HttpMethod.Post,new{displayName="Agent",email="other@fixture.test",password=AgentTestSupport.Password,role="Admin"},token);Assert.Equal(HttpStatusCode.BadRequest,overrideRole.StatusCode);
        using var deleted=await api.SendAsync($"/api/agents/{agent.UserId}",HttpMethod.Delete,token:token);Assert.Equal(HttpStatusCode.MethodNotAllowed,deleted.StatusCode);
        foreach(var id in new[]{admin.Id,Guid.NewGuid()}){using var target=await api.SendAsync($"/api/agents/{id}",HttpMethod.Patch,new{displayName="Changed",version=agent.Version},token);Assert.Equal(HttpStatusCode.NotFound,target.StatusCode);}
        Assert.Equal(before,JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x=>x.Id).ToListAsync()));
    }
}
