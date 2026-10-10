using Crm.Application.Accounts.Commands;
using Crm.Application.Accounts.Queries;
using Crm.Application.Accounts.Contracts;
using Crm.Application.Accounts.Ports;
using Crm.Application.Common;
using Crm.Domain.Accounts;
namespace Crm.Application.Tests.Accounts;
public sealed class AgentManagementTests
{
    [Theory] [InlineData(" \t")] [InlineData("\u0085")] [InlineData("\u00a0\u202f")]
    public async Task Create_and_rename_use_canonical_boundary_names(string whitespace)
    {
        var name=string.Concat(Enumerable.Repeat("😀",200));const string email="\ufeffagent@example.test\ufeff",password="  exact عربي password  ";var store=new Store();var handler=new AgentCommandHandler(store);
        Assert.True((await handler.Handle(new CreateAgentCommand(Admin,whitespace+name+whitespace,email,password),default)).Succeeded);
        Assert.Equal(name,store.Account!.DisplayName);Assert.Equal(email,store.Account.Email);Assert.Equal(password,store.Account.Password);Assert.Equal(AccountRole.Agent,store.Account.Role);
        Assert.True((await handler.Handle(new RenameAgentCommand(Admin,Guid.NewGuid(),whitespace+name+whitespace,"AAAAAAAAAAA="),default)).Succeeded);Assert.Equal(name,store.Name);
    }
    private static AccountIdentity Admin=>new(Guid.NewGuid(),"Admin",AccountRole.Admin);
    private sealed class Store:IAgentAccountStore
    {
        public NewAccount? Account;public int Calls;public byte[]? Version;public string? Name;public bool Conflict;
        public Task<OperationResult<AgentAccountDto>> CreateAsync(NewAccount account,CancellationToken ct){Calls++;Account=account;return Result();}
        public Task<OperationResult<AgentAccountDto>> GetAsync(Guid id,CancellationToken ct){Calls++;return Task.FromResult(OperationResult<AgentAccountDto>.Fail("agent_not_found",404));}
        public Task<AgentPage> ListAsync(int page,int pageSize,CancellationToken ct){Calls++;return Task.FromResult(new AgentPage([],page,pageSize,0));}
        public Task<OperationResult<AgentAccountDto>> RenameAsync(Guid id,string name,byte[] version,CancellationToken ct){Calls++;Version=version;Name=name;return Result();}
        public Task<OperationResult<AgentAccountDto>> SetAvailabilityAsync(Guid id,bool active,byte[] version,CancellationToken ct){Calls++;Version=version;return Result();}
        private Task<OperationResult<AgentAccountDto>> Result()=>Task.FromResult(Conflict?OperationResult<AgentAccountDto>.Fail("account_changed",409):OperationResult<AgentAccountDto>.Success(new(Guid.NewGuid(),"Agent","agent@fixture.test",true,Guid.NewGuid(),Convert.ToBase64String(new byte[8]))));
    }
    [Theory] [InlineData(AccountRole.Customer)] [InlineData(AccountRole.Agent)]
    public async Task All_operations_require_trusted_Admin(AccountRole role)
    {
        var store=new Store();var actor=new AccountIdentity(Guid.NewGuid(),"Caller",role);var commands=new AgentCommandHandler(store);var queries=new AgentQueryHandler(store);var id=Guid.NewGuid();
        Assert.Equal(403,(await commands.Handle(new CreateAgentCommand(actor,"Agent","agent@fixture.test","123456789012345"),default)).Error!.Status);
        Assert.Equal(403,(await commands.Handle(new RenameAgentCommand(actor,id,"Agent","AAAAAAAAAAA="),default)).Error!.Status);
        Assert.Equal(403,(await commands.Handle(new SetAgentAvailabilityCommand(actor,id,false,"AAAAAAAAAAA="),default)).Error!.Status);
        Assert.Equal(403,(await queries.Handle(new GetAgentQuery(actor,id),default)).Error!.Status);Assert.Equal(403,(await queries.Handle(new ListAgentsQuery(actor),default)).Error!.Status);Assert.Equal(0,store.Calls);
    }
    [Fact] public async Task Missing_actor_and_invalid_inputs_do_not_call_store()
    {
        var store=new Store();var handler=new AgentCommandHandler(store);
        Assert.Equal(401,(await handler.Handle(new CreateAgentCommand(null,"Agent","agent@fixture.test","123456789012345"),default)).Error!.Status);
        foreach(var command in new[]{new CreateAgentCommand(Admin," ","agent@fixture.test","123456789012345"),new CreateAgentCommand(Admin,"Agent","bad","123456789012345"),new CreateAgentCommand(Admin,"Agent","agent@fixture.test","short")})Assert.Equal(400,(await handler.Handle(command,default)).Error!.Status);Assert.Equal(0,store.Calls);
    }
    [Fact] public async Task Create_assigns_Agent_and_preserves_common_normalization_and_exact_password()
    {
        var store=new Store();Assert.True((await new AgentCommandHandler(store).Handle(new CreateAgentCommand(Admin," Agent "," agent@fixture.test ","  عربي password  "),default)).Succeeded);
        Assert.Equal(AccountRole.Agent,store.Account!.Role);Assert.Equal("Agent",store.Account.DisplayName);Assert.Equal("agent@fixture.test",store.Account.Email);Assert.Equal("  عربي password  ",store.Account.Password);
    }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("bad")] [InlineData("AAAA")] [InlineData(" AAAAAAAAAAA=")]
    public async Task Invalid_versions_rejected_before_mutation(string? version)
    {
        var store=new Store();var handler=new AgentCommandHandler(store);
        Assert.Equal(400,(await handler.Handle(new RenameAgentCommand(Admin,Guid.NewGuid(),"Agent",version),default)).Error!.Status);Assert.Equal(400,(await handler.Handle(new SetAgentAvailabilityCommand(Admin,Guid.NewGuid(),false,version),default)).Error!.Status);Assert.Equal(0,store.Calls);
    }
    [Fact] public async Task Stale_noop_conflicts_and_missing_targets_are_preserved()
    {
        var store=new Store{Conflict=true};var id=Guid.NewGuid();var handler=new AgentCommandHandler(store);
        Assert.Equal(409,(await handler.Handle(new RenameAgentCommand(Admin,id," Agent ","AAAAAAAAAAA="),default)).Error!.Status);Assert.Equal("Agent",store.Name);Assert.Equal(new byte[8],store.Version);
        Assert.Equal(409,(await handler.Handle(new SetAgentAvailabilityCommand(Admin,id,true,"AAAAAAAAAAA="),default)).Error!.Status);
        Assert.Equal(404,(await new AgentQueryHandler(store).Handle(new GetAgentQuery(Admin,id),default)).Error!.Status);
    }
    [Theory] [InlineData(0,25)] [InlineData(1,0)] [InlineData(1,101)]
    public async Task Invalid_paging_does_not_read(int page,int size)
    {var store=new Store();Assert.Equal(400,(await new AgentQueryHandler(store).Handle(new ListAgentsQuery(Admin,page,size),default)).Error!.Status);Assert.Equal(0,store.Calls);}
    [Fact] public async Task Cancellation_precedes_authorization_and_persistence()
    {using var cancellation=new CancellationTokenSource();cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new AgentCommandHandler(new Store()).Handle(new CreateAgentCommand(Admin,"Agent","agent@fixture.test","123456789012345"),cancellation.Token));}
}
