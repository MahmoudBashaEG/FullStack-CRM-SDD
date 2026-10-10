using Crm.Application.Accounts.Commands;
using Crm.Application.Accounts.Contracts;
using Crm.Domain.Accounts;
using Crm.IntegrationTests.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Crm.IntegrationTests.Fixtures;
internal static class LocalAgentFixture
{
    public static async Task<Guid> CreateAsync(string connection,string label,bool active=true)
    {
        using var services=AdminBootstrapTests.Services(connection);using var scope=services.CreateScope();var sender=scope.ServiceProvider.GetRequiredService<IMediator>();var actor=new AccountIdentity(Guid.NewGuid(),"Private operator",AccountRole.Admin);
        var result=await sender.Send(new CreateAgentCommand(actor,label,Guid.NewGuid().ToString("N")+"@fixture.test",Guid.NewGuid().ToString("N")));Assert.True(result.Succeeded);
        if(!active)Assert.True((await sender.Send(new SetAgentAvailabilityCommand(actor,result.Value!.UserId,false,result.Value.Version))).Succeeded);return result.Value!.AssignmentReferenceId;
    }
    public static async Task UpdateAsync(string connection,Guid reference,string label,bool active,CancellationToken ct=default)
    {
        using var services=AdminBootstrapTests.Services(connection);using var scope=services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<Crm.Infrastructure.Persistence.CrmDbContext>();var link=await db.Agents.AsNoTracking().SingleAsync(x=>x.Id==reference,ct);Assert.NotNull(link.LocalUserId);
        var sender=scope.ServiceProvider.GetRequiredService<IMediator>();var actor=new AccountIdentity(Guid.NewGuid(),"Private operator",AccountRole.Admin);var user=await db.Users.AsNoTracking().SingleAsync(x=>x.Id==link.LocalUserId,ct);
        var renamed=await sender.Send(new RenameAgentCommand(actor,user.Id,label,Convert.ToBase64String(user.RowVersion)),ct);Assert.True(renamed.Succeeded);var availability=await sender.Send(new SetAgentAvailabilityCommand(actor,user.Id,active,renamed.Value!.Version),ct);Assert.True(availability.Succeeded);
    }
}
