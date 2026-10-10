using System.Text.Json;
using Crm.Application.Accounts.Commands;
using Crm.Application.Accounts.Contracts;
using Crm.Application.Accounts.Ports;
using Crm.Domain.Accounts;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Tickets;
using Crm.IntegrationTests.Fixtures;
using Crm.IntegrationTests.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
namespace Crm.IntegrationTests.Accounts;

[Collection("SQL")]
public sealed class AgentManagementTests
{
    [Fact] public async Task Matching_legacy_name_never_links_and_local_Agent_saved_snapshots_remain_unchanged()
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);await using var db=sql.OpenContext();
        var legacy=new AgentReference{Id=Guid.NewGuid(),Issuer="urn:legacy",Subject="agent@fixture.test",DisplayName="Agent عربي",IsEligible=false};db.Agents.Add(legacy);await db.SaveChangesAsync();
        using var services=AdminBootstrapTests.Services(sql.Connection);using var scope=services.CreateScope();var mediator=scope.ServiceProvider.GetRequiredService<IMediator>();var agent=(await mediator.Send(Create())).Value!;Assert.NotEqual(legacy.Id,agent.AssignmentReferenceId);
        var actor=new ActorSnapshot("urn:crm:local:user",agent.UserId.ToString("D"),agent.DisplayName);var ticket=Ticket.Create("Saved Agent ticket","Preserved",new(Guid.Parse("00000000-0000-0000-0000-000000000001"),"Uncategorised"),TicketPriority.Normal,actor,DateTimeOffset.UtcNow);ticket.Assign(null,new(agent.AssignmentReferenceId,agent.DisplayName),actor,DateTimeOffset.UtcNow);db.Tickets.Add(ticket);db.History.AddRange(ticket.PendingHistory);await db.SaveChangesAsync();ticket.AcknowledgePersistedHistory();
        var before=JsonSerializer.Serialize(await db.History.AsNoTracking().OrderBy(x=>x.Revision).ThenBy(x=>x.Sequence).ToListAsync());var storedTicket=JsonSerializer.Serialize(await db.Tickets.AsNoTracking().SingleAsync());
        var rename=(await mediator.Send(new RenameAgentCommand(Admin,agent.UserId,"Future name",agent.Version))).Value!;var disable=(await mediator.Send(new SetAgentAvailabilityCommand(Admin,agent.UserId,false,rename.Version))).Value!;Assert.False(disable.IsActive);var enabled=(await mediator.Send(new SetAgentAvailabilityCommand(Admin,agent.UserId,true,disable.Version))).Value!;Assert.Equal(agent.AssignmentReferenceId,enabled.AssignmentReferenceId);
        Assert.Equal(before,JsonSerializer.Serialize(await db.History.AsNoTracking().OrderBy(x=>x.Revision).ThenBy(x=>x.Sequence).ToListAsync()));Assert.Equal(storedTicket,JsonSerializer.Serialize(await db.Tickets.AsNoTracking().SingleAsync()));var savedLegacy=await db.Agents.AsNoTracking().SingleAsync(x=>x.Id==legacy.Id);Assert.Null(savedLegacy.LocalUserId);Assert.False(savedLegacy.IsEligible);Assert.Equal("Agent عربي",savedLegacy.DisplayName);
    }
    private static AccountIdentity Admin=>new(Guid.NewGuid(),"Admin",AccountRole.Admin);
    private static CreateAgentCommand Create(string email="agent@fixture.test",string password=AgentTestSupport.Password)=>new(Admin," Agent عربي ",email,password);
    [Fact] public async Task Concurrent_equivalent_creation_commits_one_User_and_new_reference()
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);
        async Task<Crm.Application.Common.OperationResult<AgentAccountDto>> Run(string email){using var provider=AdminBootstrapTests.Services(sql.Connection);using var scope=provider.CreateScope();return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(Create(email));}
        var results=await Task.WhenAll(Run(" AGENT@fixture.test "),Run("agent@fixture.test"));var created=Assert.Single(results,x=>x.Succeeded).Value!;Assert.Equal("agent_conflict",Assert.Single(results,x=>!x.Succeeded).Error!.Code);
        await using var db=sql.OpenContext();var user=Assert.Single(await db.Users.ToListAsync());var reference=Assert.Single(await db.Agents.ToListAsync());
        Assert.Equal(AccountRole.Agent,user.AccountRole);Assert.True(user.IsActive);Assert.Equal(user.Id,reference.LocalUserId);Assert.NotEqual(user.Id,reference.Id);Assert.Equal(created.AssignmentReferenceId,reference.Id);Assert.True(reference.IsEligible);Assert.Equal("urn:crm:local:user",reference.Issuer);Assert.Equal(user.Id.ToString("D"),reference.Subject);Assert.Equal(user.DisplayName,reference.DisplayName);Assert.Equal(8,Convert.FromBase64String(created.Version).Length);Assert.Empty(await db.AuthenticationSessions.ToListAsync());
    }
    [Theory] [InlineData(14,false)] [InlineData(15,true)] [InlineData(128,true)] [InlineData(129,false)]
    public async Task Shared_password_boundaries_are_exact_and_atomic(int length,bool accepted)
    {
        foreach(var password in new[]{new string(' ',length),string.Concat(Enumerable.Repeat("😀",length))}){
            await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);using var services=AdminBootstrapTests.Services(sql.Connection);using var scope=services.CreateScope();var result=await scope.ServiceProvider.GetRequiredService<IMediator>().Send(Create(password:password));Assert.Equal(accepted,result.Succeeded);
            await using var db=sql.OpenContext();Assert.Equal(accepted?1:0,await db.Users.CountAsync());Assert.Equal(accepted?1:0,await db.Agents.CountAsync());if(accepted)Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>().CheckPasswordAsync(await db.Users.SingleAsync(),password));}
    }
    private sealed class FailReference(bool cancel):SaveChangesInterceptor
    {
        private bool referenceWrite;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {referenceWrite=data.Context!.ChangeTracker.Entries<AgentReference>().Any(x=>x.State is EntityState.Added or EntityState.Modified);return base.SavingChangesAsync(data,result,ct);}
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data,int result,CancellationToken ct=default)
        {if(referenceWrite){if(cancel)throw new OperationCanceledException();throw new InvalidOperationException("Injected failure after reference persistence");}return base.SavedChangesAsync(data,result,ct);}
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Reference_failure_after_Identity_save_rolls_back_creation(bool cancel)
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);using var services=AdminBootstrapTests.Services(sql.Connection,new FailReference(cancel));using var scope=services.CreateScope();await Assert.ThrowsAnyAsync<Exception>(()=>scope.ServiceProvider.GetRequiredService<IMediator>().Send(Create()));
        await using var db=sql.OpenContext();Assert.Empty(await db.Users.ToListAsync());Assert.Empty(await db.Agents.ToListAsync());using var clean=AdminBootstrapTests.Services(sql.Connection);using var retry=clean.CreateScope();Assert.True((await retry.ServiceProvider.GetRequiredService<IMediator>().Send(Create())).Succeeded);
    }
    [Fact] public async Task Rename_and_availability_preserve_legacy_assignments_and_history_and_compare_stale_before_noop()
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);using var services=AdminBootstrapTests.Services(sql.Connection);using var scope=services.CreateScope();var mediator=scope.ServiceProvider.GetRequiredService<IMediator>();var created=(await mediator.Send(Create())).Value!;
        await using var db=sql.OpenContext();var legacy=new AgentReference{Id=Guid.NewGuid(),Issuer="urn:legacy",Subject="old-agent",DisplayName="Old Agent",IsEligible=false};db.Agents.Add(legacy);
        var ticket=Ticket.Create("Preserved","Saved details",new(Guid.Parse("00000000-0000-0000-0000-000000000001"),"Uncategorised"),TicketPriority.Normal,new("urn:legacy","actor","Saved Actor"),DateTimeOffset.UtcNow);ticket.Assign(null,new(legacy.Id,legacy.DisplayName),new("urn:legacy","actor","Saved Actor"),DateTimeOffset.UtcNow);db.Tickets.Add(ticket);db.History.AddRange(ticket.PendingHistory);await db.SaveChangesAsync();ticket.AcknowledgePersistedHistory();
        var beforeTicket=JsonSerializer.Serialize(await db.Tickets.AsNoTracking().SingleAsync());var beforeHistory=JsonSerializer.Serialize(await db.History.AsNoTracking().OrderBy(x=>x.Revision).ThenBy(x=>x.Sequence).ToListAsync());var beforeLegacy=JsonSerializer.Serialize(legacy);var before=await db.Users.AsNoTracking().SingleAsync();
        var renamed=(await mediator.Send(new RenameAgentCommand(Admin,created.UserId," Renamed عربي ",created.Version))).Value!;Assert.NotEqual(created.Version,renamed.Version);Assert.Equal(created.AssignmentReferenceId,renamed.AssignmentReferenceId);
        Assert.Equal("account_changed",(await mediator.Send(new RenameAgentCommand(Admin,created.UserId,renamed.DisplayName,created.Version))).Error!.Code);
        var noop=(await mediator.Send(new RenameAgentCommand(Admin,created.UserId,renamed.DisplayName,renamed.Version))).Value!;Assert.Equal(renamed.Version,noop.Version);
        var disabled=(await mediator.Send(new SetAgentAvailabilityCommand(Admin,created.UserId,false,noop.Version))).Value!;var enabled=(await mediator.Send(new SetAgentAvailabilityCommand(Admin,created.UserId,true,disabled.Version))).Value!;
        var after=await db.Users.AsNoTracking().SingleAsync();Assert.Equal(before.Id,after.Id);Assert.Equal(before.Email,after.Email);Assert.Equal(before.PasswordHash,after.PasswordHash);Assert.Equal(AccountRole.Agent,after.AccountRole);Assert.True(after.IsActive);Assert.Equal(2,after.AuthVersion);Assert.NotEqual(before.SecurityStamp,after.SecurityStamp);Assert.Equal(created.AssignmentReferenceId,enabled.AssignmentReferenceId);
        Assert.Equal(beforeTicket,JsonSerializer.Serialize(await db.Tickets.AsNoTracking().SingleAsync()));Assert.Equal(beforeHistory,JsonSerializer.Serialize(await db.History.AsNoTracking().OrderBy(x=>x.Revision).ThenBy(x=>x.Sequence).ToListAsync()));Assert.Equal(beforeLegacy,JsonSerializer.Serialize(await db.Agents.AsNoTracking().SingleAsync(x=>x.Id==legacy.Id)));Assert.Equal("Renamed عربي",(await db.Agents.AsNoTracking().SingleAsync(x=>x.LocalUserId==created.UserId)).DisplayName);
    }
    [Fact] public async Task Concurrent_Admin_updates_with_one_version_have_one_winner()
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);AgentAccountDto agent;using(var provider=AdminBootstrapTests.Services(sql.Connection))using(var scope=provider.CreateScope())agent=(await scope.ServiceProvider.GetRequiredService<IMediator>().Send(Create())).Value!;
        async Task<Crm.Application.Common.OperationResult<AgentAccountDto>> Run(string name){using var provider=AdminBootstrapTests.Services(sql.Connection);using var scope=provider.CreateScope();return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RenameAgentCommand(Admin,agent.UserId,name,agent.Version));}
        var results=await Task.WhenAll(Run("First"),Run("Second"));Assert.Single(results,x=>x.Succeeded);Assert.Equal("account_changed",Assert.Single(results,x=>!x.Succeeded).Error!.Code);await using var db=sql.OpenContext();Assert.Equal((await db.Users.SingleAsync()).DisplayName,(await db.Agents.SingleAsync()).DisplayName);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Failed_disable_rolls_back_user_sessions_and_reference(bool cancel)
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);AgentAccountDto agent;using(var provider=AdminBootstrapTests.Services(sql.Connection))using(var scope=provider.CreateScope())agent=(await scope.ServiceProvider.GetRequiredService<IMediator>().Send(Create())).Value!;
        await using var db=sql.OpenContext();db.AuthenticationSessions.Add(new(){UserId=agent.UserId,AuthVersionAtCreation=1,CreatedAtUtc=DateTimeOffset.UtcNow,ExpiresAtUtc=DateTimeOffset.UtcNow.AddDays(7)});await db.SaveChangesAsync();var before=JsonSerializer.Serialize(await db.Users.AsNoTracking().SingleAsync());
        using var services=AdminBootstrapTests.Services(sql.Connection,new FailReference(cancel));using var faulty=services.CreateScope();await Assert.ThrowsAnyAsync<Exception>(()=>faulty.ServiceProvider.GetRequiredService<IMediator>().Send(new SetAgentAvailabilityCommand(Admin,agent.UserId,false,agent.Version)));
        Assert.Equal(before,JsonSerializer.Serialize(await db.Users.AsNoTracking().SingleAsync()));Assert.True((await db.Agents.AsNoTracking().SingleAsync()).IsEligible);Assert.Null((await db.AuthenticationSessions.AsNoTracking().SingleAsync()).RevokedAtUtc);
    }
}
