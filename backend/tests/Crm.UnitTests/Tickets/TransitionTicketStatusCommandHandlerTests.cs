using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Tickets;
using Crm.Application.Tickets.TransitionTicket;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public sealed class TransitionTicketStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_AssignedAgentResolvesEscalatedTicket_RecordsTransitionAndAutoClearsEscalationInOneSave()
    {
        var agentId = Guid.CreateVersion7();
        var createdAt = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var ticket = Ticket.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Cannot sign in", "Password rejected.",
            TicketCategory.Technical, TicketPriority.High, agentId, agentId, createdAt);
        ticket.TransitionTo(TicketStatus.InProgress, agentId, createdAt.AddMinutes(5));
        ticket.Escalate("Customer blocked for two days.", agentId, createdAt.AddMinutes(10));
        var now = createdAt.AddHours(1);
        var tickets = new FakeTicketRepository(ticket);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new TransitionTicketStatusCommandHandler(tickets, unitOfWork, new FixedTimeProvider(now));

        var result = await handler.Handle(
            new TransitionTicketStatusCommand(
                new TicketActor(agentId, IsSuperAdmin: false), ticket.Id, TicketStatus.InProgress, TicketStatus.Resolved),
            CancellationToken.None);

        Assert.Equal(agentId, tickets.LoadedWithScope);            // loaded under the Agent's own scope
        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.False(ticket.IsEscalated);                          // auto-cleared on Resolve
        Assert.Null(ticket.EscalationReason);
        Assert.Null(ticket.EscalatedByUserId);
        Assert.Equal(now, ticket.UpdatedAtUtc);
        Assert.Equal(agentId, ticket.AssignedAgentUserId);         // assignment untouched
        Assert.Equal(1, unitOfWork.SaveCount);                     // ticket + events saved together

        var events = ticket.Activities.ToList();                   // Created, StatusChanged, Escalated, StatusChanged, EscalationAutoCleared
        Assert.Equal(5, events.Count);
        var resolved = events[3];
        Assert.Equal(TicketActivityType.StatusChanged, resolved.Type);
        Assert.Equal("InProgress", resolved.OldValue);
        Assert.Equal("Resolved", resolved.NewValue);
        Assert.Equal(agentId, resolved.ActorUserId);
        Assert.Equal(now, resolved.OccurredAtUtc);
        var cleared = events[4];
        Assert.Equal(TicketActivityType.EscalationAutoCleared, cleared.Type);
        Assert.Equal(agentId, cleared.ActorUserId);
        Assert.Equal(now, cleared.OccurredAtUtc);
        Assert.Equal(TicketStatus.Resolved, result.Status);
        Assert.False(result.IsEscalated);
    }

    private sealed class FakeTicketRepository(Ticket ticket) : ITicketRepository
    {
        public Guid? LoadedWithScope { get; private set; }

        public Task<Ticket?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default)
        {
            LoadedWithScope = agentScope;
            var visible = id == ticket.Id && (agentScope is null || agentScope == ticket.AssignedAgentUserId);
            return Task.FromResult(visible ? ticket : null);
        }

        public Task<TicketDetails?> FindDetailsByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default) =>
            Task.FromResult<TicketDetails?>(new TicketDetails(
                ticket.Id, ticket.Subject, ticket.Description, ticket.Category, ticket.Priority, ticket.Status, ticket.CustomerId,
                "Ada Lovelace", "ada@example.com", ticket.CreatedByUserId, "Agent One", ticket.AssignedAgentUserId, "Agent One",
                ticket.CreatedAtUtc, ticket.UpdatedAtUtc, ticket.IsEscalated, ticket.EscalationReason, ticket.EscalatedAtUtc,
                ticket.EscalatedByUserId, null));

        public void Add(Ticket added) => throw new NotSupportedException();
        public Task<PagedResult<TicketDetails>> ListAsync(TicketListFilter filter, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<TicketActivityDetails>?> ListActivityAsync(Guid ticketId, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.FromResult(1); }
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(); // not a transactional command
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
