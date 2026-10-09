using System.Text.Json;
using Crm.Infrastructure.Tickets;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
namespace Crm.IntegrationTests.Identity;

[Collection("SQL")]
public sealed class ReferenceProvisioningFoundationTests(SqlServerFixture sql)
{
    [Fact] public void Nonempty_legacy_agent_inputs_are_rejected()
    {
        var input=JsonSerializer.Serialize(new { schemaVersion=1, agents=new[] {new {id=Guid.NewGuid(),displayName="Legacy",issuer="https://example.test",subject="legacy",isEligible=true}} });
        Assert.Throws<JsonException>(()=>ReferenceProvisioningInput.Parse(input));
    }
    [Fact] public async Task Parser_bypass_cannot_mutate_agents_or_stage_categories()
    {
        var category=Guid.NewGuid(); var agent=Guid.NewGuid(); await using var database=sql.OpenContext();
        var input=new ReferenceProvisioningInput {SchemaVersion=1,Categories=[new(category,"Not saved",true)],Agents=[new(agent,"https://example.test","legacy","Legacy",true)]};
        await Assert.ThrowsAsync<ProvisioningValidationException>(()=>new ReferenceProvisioner(database).ApplyAsync(input,CancellationToken.None));
        await using var after=sql.OpenContext(); Assert.False(await after.Categories.AnyAsync(x=>x.Id==category)); Assert.False(await after.Agents.AnyAsync(x=>x.Id==agent));
    }
    [Fact] public async Task Category_only_empty_agent_array_remains_idempotent()
    {
        var id=Guid.NewGuid(); var input=ReferenceProvisioningInput.Parse(JsonSerializer.Serialize(new {schemaVersion=1,categories=new[]{new{id,displayName="Category",isAvailable=true}},agents=Array.Empty<object>()}));
        await using(var database=sql.OpenContext()) Assert.Equal(1,(await new ReferenceProvisioner(database).ApplyAsync(input,CancellationToken.None)).Inserted);
        await using(var database=sql.OpenContext()) Assert.Equal(1,(await new ReferenceProvisioner(database).ApplyAsync(input,CancellationToken.None)).Unchanged);
    }
}
