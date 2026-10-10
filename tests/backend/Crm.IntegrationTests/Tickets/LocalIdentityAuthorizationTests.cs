using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Domain.Accounts;
using Crm.IntegrationTests.Fixtures;
namespace Crm.IntegrationTests.Tickets;
[Collection("SQL")]
public sealed class LocalIdentityAuthorizationTests
{
    [Theory] [InlineData(AccountRole.Agent)] [InlineData(AccountRole.Admin)]
    public async Task Successful_mutations_record_trusted_local_UserId_and_name(AccountRole role)
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();const string password="Private synthetic password";var user=await api.PrepareAsync(role,password);var token=await api.SignInAsync(user,password);
        using var create=await api.SendAsync("/api/tickets",HttpMethod.Post,new{subject="Local actor",description="Shared queue"},token);Assert.Equal(HttpStatusCode.Created,create.StatusCode);var ticket=await create.Content.ReadFromJsonAsync<JsonElement>();var reference=ticket.GetProperty("reference").GetString();
        using var history=await api.SendAsync($"/api/tickets/{reference}/history",token:token);var actor=(await history.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items")[0].GetProperty("actor");Assert.Equal("urn:crm:local:user",actor.GetProperty("issuer").GetString());Assert.Equal(user.Id.ToString("D"),actor.GetProperty("subject").GetString());Assert.Equal(user.DisplayName,actor.GetProperty("displayName").GetString());
        using var spoof=await api.SendAsync("/api/tickets",HttpMethod.Post,new{subject="Forged",description="Rejected",actor=new{subject=Guid.NewGuid(),displayName="Other",role="Admin"}},token);Assert.Equal(HttpStatusCode.BadRequest,spoof.StatusCode);
    }
    [Theory] [InlineData(AccountRole.Customer)] [InlineData(AccountRole.Agent)] [InlineData(AccountRole.Admin)]
    public async Task Actor_requires_trusted_snapshot_for_any_role_and_ignores_raw_claims(AccountRole role)
    {
        var id=Guid.NewGuid();var context=new Microsoft.AspNetCore.Http.DefaultHttpContext();context.User=new(new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim("sub","forged"),new System.Security.Claims.Claim("name","forged")},"untrusted"));
        var source=new Crm.Api.Authorization.CurrentActor(new Microsoft.AspNetCore.Http.HttpContextAccessor{HttpContext=context});Assert.Throws<InvalidOperationException>(()=>source.Get());
        context.Items[typeof(Crm.Application.Authentication.Contracts.TrustedAuthentication)]=new Crm.Application.Authentication.Contracts.TrustedAuthentication(new(id,"Current name",role),Guid.NewGuid(),1,DateTimeOffset.UtcNow.AddDays(1));var actor=source.Get();Assert.Equal(id.ToString("D"),actor.Subject);Assert.Equal("urn:crm:local:user",actor.Issuer);Assert.Equal("Current name",actor.DisplayName);
    }
}
