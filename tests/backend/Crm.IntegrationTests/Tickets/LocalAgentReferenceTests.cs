using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Domain.Accounts;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Tickets;
using Crm.IntegrationTests.Accounts;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
namespace Crm.IntegrationTests.Tickets;
[Collection("SQL")]
public sealed class LocalAgentReferenceTests
{
    [Fact] public async Task Effective_eligibility_excludes_unlinked_or_inactive_accounts_and_saved_references_remain_mutable()
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();var token=await api.SignInAsync(await api.PrepareAsync(AccountRole.Admin,AgentTestSupport.Password),AgentTestSupport.Password);var agent=await AgentTestSupport.CreateAsync(api,token);
        await using var db=api.Sql.OpenContext();var legacy=new AgentReference{Id=Guid.NewGuid(),Issuer="urn:legacy",Subject="old",DisplayName="Saved legacy",IsEligible=true};db.Agents.Add(legacy);await db.SaveChangesAsync();
        using var options=await api.SendAsync("/api/ticket-reference-data/agents",token:token);var choices=await options.Content.ReadFromJsonAsync<JsonElement>();Assert.DoesNotContain(choices.EnumerateArray(),x=>x.GetProperty("id").GetGuid()==legacy.Id);Assert.Contains(choices.EnumerateArray(),x=>x.GetProperty("id").GetGuid()==agent.AssignmentReferenceId);
        var ticket=Ticket.Create("Legacy saved","Preserved",new(Guid.Parse("00000000-0000-0000-0000-000000000001"),"Uncategorised"),TicketPriority.Normal,new("urn:legacy","old","Old Actor"),DateTimeOffset.UtcNow);ticket.Assign(null,new(legacy.Id,legacy.DisplayName),new("urn:legacy","old","Old Actor"),DateTimeOffset.UtcNow);db.Tickets.Add(ticket);db.History.AddRange(ticket.PendingHistory);await db.SaveChangesAsync();
        using var get=await api.SendAsync($"/api/tickets/{ticket.Reference}",token:token);var detail=await get.Content.ReadFromJsonAsync<JsonElement>();Assert.False(detail.GetProperty("assignedAgent").GetProperty("isEligible").GetBoolean());
        using var edit=await api.SendAsync($"/api/tickets/{ticket.Reference}/details",HttpMethod.Patch,new{expectedVersion=detail.GetProperty("editVersion").GetString(),subject="Unrelated edit"},token);Assert.Equal(HttpStatusCode.OK,edit.StatusCode);var updated=await edit.Content.ReadFromJsonAsync<JsonElement>();
        using var clear=await api.SendAsync($"/api/tickets/{ticket.Reference}/assignment",HttpMethod.Put,new{expectedVersion=updated.GetProperty("editVersion").GetString(),agentId=(Guid?)null},token);Assert.Equal(HttpStatusCode.OK,clear.StatusCode);
        var cleared=await clear.Content.ReadFromJsonAsync<JsonElement>();using var reject=await api.SendAsync($"/api/tickets/{ticket.Reference}/assignment",HttpMethod.Put,new{expectedVersion=cleared.GetProperty("editVersion").GetString(),agentId=legacy.Id},token);Assert.Equal(HttpStatusCode.Conflict,reject.StatusCode);
        var user=await db.Users.SingleAsync(x=>x.Id==agent.UserId);user.IsActive=false;await db.SaveChangesAsync();using var inactive=await api.SendAsync("/api/ticket-reference-data/agents",token:token);Assert.DoesNotContain((await inactive.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray(),x=>x.GetProperty("id").GetGuid()==agent.AssignmentReferenceId);
    }
}
