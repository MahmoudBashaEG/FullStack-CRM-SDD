using Crm.Application.Common;
using Crm.Application.Tickets.Commands;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using Crm.Domain.Tickets;
namespace Crm.Application.Tests.Tickets;
public sealed class LocalActorTests
{
    private sealed class Source:ICurrentActor {public Actor Value=new("urn:crm:local:user",Guid.NewGuid().ToString("D"),"Original name");public Actor Get()=>Value;}
    private sealed class Store:ITicketMutationStore
    {
        public List<Ticket> Saved=[];
        public Task<OperationResult<TicketDetailDto>> CreateAsync(Guid categoryId,Func<TicketCategoryDto?,OperationResult<Ticket>> create,CancellationToken ct){var ticket=create(new(categoryId,"Uncategorised",true)).Value!;Saved.Add(ticket);return Task.FromResult(OperationResult<TicketDetailDto>.Success(new(ticket.Reference,ticket.Subject,ticket.Description,new(categoryId,"Uncategorised",true),"Normal","Open",null,ticket.CreatedAtUtc,ticket.UpdatedAtUtc,"AAAAAAAAAAA=")));}
        public Task<OperationResult<TicketDetailDto>> MutateAsync(string reference,byte[] version,Func<TicketMutationContext,CancellationToken,Task<Failure?>> mutation,CancellationToken ct)=>throw new NotSupportedException();
    }
    [Fact] public async Task Future_actor_names_do_not_rewrite_earlier_snapshots_or_change_stable_identity()
    {
        var source=new Source();var store=new Store();var handler=new CreateTicketHandler(store,source,TimeProvider.System);await handler.Handle(new("First","Description",null,null),default);var original=store.Saved[0].PendingHistory.Single();source.Value=source.Value with{DisplayName="New name"};await handler.Handle(new("Second","Description",null,null),default);
        Assert.Equal("Original name",original.ActorDisplayName);Assert.Equal("New name",store.Saved[1].PendingHistory.Single().ActorDisplayName);Assert.Equal(original.ActorSubject,store.Saved[1].PendingHistory.Single().ActorSubject);
    }
}
