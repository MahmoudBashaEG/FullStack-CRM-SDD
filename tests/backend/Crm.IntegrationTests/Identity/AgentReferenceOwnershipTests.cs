using Crm.Infrastructure;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Tickets;
using Crm.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Crm.IntegrationTests.Identity;

[Collection("SQL")]
public sealed class AgentReferenceOwnershipTests(SqlServerFixture sql)
{
    [Fact] public async Task New_assignment_link_is_unique_and_legacy_identity_is_not_rebound()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:Crm"]=sql.Connection}).Build();
        using var provider=new ServiceCollection().AddLogging().AddInfrastructure(config).BuildServiceProvider();using var scope=provider.CreateScope();
        var user=IdentityPersistenceTests.User();Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>().CreateAsync(user,Guid.NewGuid().ToString("N"))).Succeeded);
        var first=new AgentReference{Id=Guid.NewGuid(),LocalUserId=user.Id,Issuer="urn:crm:local:user",Subject=user.Id.ToString("D"),DisplayName=user.DisplayName,IsEligible=true};
        await using(var db=sql.OpenContext()){db.Agents.Add(first);await db.SaveChangesAsync();}
        await using(var db=sql.OpenContext()){db.Agents.Add(new(){Id=Guid.NewGuid(),LocalUserId=user.Id,Issuer="urn:crm:local:user",Subject="different",DisplayName="Duplicate",IsEligible=true});await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());}
        await using(var db=sql.OpenContext())
        {
            var id=Guid.NewGuid();db.Agents.Add(new(){Id=id,Issuer="https://legacy.example",Subject=Guid.NewGuid().ToString(),DisplayName="Saved Legacy",IsEligible=false});await db.SaveChangesAsync();
            Assert.Null((await db.Agents.SingleAsync(x=>x.Id==id)).LocalUserId);
        }
    }
}
