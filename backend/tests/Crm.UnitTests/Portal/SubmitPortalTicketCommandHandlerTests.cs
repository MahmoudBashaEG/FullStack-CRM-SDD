using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Application.Portal;
using Crm.Application.Portal.SubmitTicket;
using Crm.Application.Sla;
using Crm.Application.Tickets;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Portal;

public sealed class SubmitPortalTicketCommandHandlerTests
{
    [Fact]
    public async Task Handle_LinkedCustomerSubmitsTicket_DerivesProfileOpensMediumUnassignedAndRunsCreationAutomationOnce()
    {
        var userId = Guid.CreateVersion7();      // Identity account (JWT sub)
        var customerId = Guid.CreateVersion7();  // linked CRM profile — a different id
        var tickets = new FakeTicketRepository();
        var automation = new RecordingTicketAutomation(tickets);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new SubmitPortalTicketCommandHandler(
            new FakeCustomerRepository(userId, customerId), tickets, automation, new FakePortalTicketRepository(tickets), unitOfWork, TimeProvider.System);

        var result = await handler.Handle(
            new SubmitPortalTicketCommand(userId, "  Invoice is wrong ", " I was charged twice. ", TicketCategory.Billing), CancellationToken.None);

        var stored = Assert.Single(tickets.Added);
        Assert.Equal(customerId, stored.CustomerId);           // resolved through Customers.UserId, never the user id
        Assert.Equal(userId, stored.CreatedByUserId);          // the customer's own account, no staff actor
        Assert.Null(stored.AssignedAgentUserId);               // no self-assignment (P2); rules may assign later in automation
        Assert.Equal(TicketStatus.Open, stored.Status);
        Assert.Equal(TicketPriority.Medium, stored.Priority);  // CP2
        Assert.Equal(TicketCategory.Billing, stored.Category);
        Assert.Equal("Invoice is wrong", stored.Subject);
        Assert.Equal("I was charged twice.", stored.Description);
        Assert.Equal(TicketActivityType.Created, Assert.Single(stored.Activities).Type);
        Assert.Same(stored, Assert.Single(automation.Applied)); // the SLA/assignment hook ran exactly once...
        Assert.Equal(0, automation.AddedCountWhenApplied);      // ...before the ticket was added and saved
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(stored.Id, result.Id);
        Assert.False(result.CanSubmitFeedback);
    }

    private sealed class FakeCustomerRepository(Guid userId, Guid customerId) : ICustomerRepository
    {
        public Task<CustomerDetails?> FindDetailsByUserIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomerDetails?>(id == userId
                ? new CustomerDetails(customerId, "Ada", "Lovelace", "ada@example.com", null, null, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
                : null);

        public void Add(Customer customer) => throw new NotSupportedException();
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Customer?> GetByUserIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerDetails?> FindDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<CustomerDetails>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    // Only Add is supported: proves the portal never goes through a staff read path.
    private sealed class FakeTicketRepository : ITicketRepository
    {
        public List<Ticket> Added { get; } = [];
        public void Add(Ticket ticket) => Added.Add(ticket);

        public Task<TicketDetails?> FindDetailsByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Ticket?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<TicketDetails>> ListAsync(TicketListFilter filter, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<TicketActivityDetails>?> ListActivityAsync(Guid ticketId, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingTicketAutomation(FakeTicketRepository tickets) : ITicketCreationAutomation
    {
        public List<Ticket> Applied { get; } = [];
        public int AddedCountWhenApplied { get; private set; } = -1;

        public Task ApplyAsync(Ticket ticket, CancellationToken cancellationToken)
        {
            Applied.Add(ticket);
            AddedCountWhenApplied = tickets.Added.Count;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePortalTicketRepository(FakeTicketRepository tickets) : IPortalTicketRepository
    {
        public Task<PortalTicketDetails?> FindAsync(Guid ticketId, Guid customerId, CancellationToken cancellationToken = default)
        {
            var t = tickets.Added.Single(x => x.Id == ticketId && x.CustomerId == customerId);
            return Task.FromResult<PortalTicketDetails?>(new PortalTicketDetails(t.Id, t.Subject, t.Description, t.Category, t.Status, t.CreatedAtUtc, null));
        }

        public Task<PagedResult<PortalTicketSummary>> ListAsync(Guid customerId, PortalTicketListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortalTicketHistory?> FindHistoryAsync(Guid ticketId, Guid customerId, int maxEvents, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void AddFeedback(TicketFeedback feedback) => throw new NotSupportedException();
        public Task<TicketFeedback?> GetFeedbackAsync(Guid ticketId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.FromResult(1); }
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
