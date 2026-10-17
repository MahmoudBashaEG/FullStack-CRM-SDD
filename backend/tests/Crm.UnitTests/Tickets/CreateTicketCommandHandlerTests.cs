using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Application.Tickets;
using Crm.Application.Tickets.CreateTicket;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public sealed class CreateTicketCommandHandlerTests
{
    [Fact]
    public async Task Handle_AgentCreatesTicket_AssignsItToTheAgentAsOpenAndSavesOnce()
    {
        var agentId = Guid.CreateVersion7();
        var customerId = Guid.CreateVersion7();
        var tickets = new FakeTicketRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateTicketCommandHandler(
            tickets, new FakeCustomerRepository(customerId), new UnusedIdentityService(), unitOfWork, TimeProvider.System);

        var result = await handler.Handle(
            new CreateTicketCommand(
                new TicketActor(agentId, IsSuperAdmin: false),
                customerId,
                "  Cannot sign in ",
                " The portal rejects my password. ",
                TicketCategory.Technical,
                Priority: null,
                AssignedAgentUserId: null),
            CancellationToken.None);

        var stored = Assert.Single(tickets.Added);
        Assert.NotEqual(Guid.Empty, stored.Id);
        Assert.Equal(customerId, stored.CustomerId);
        Assert.Equal("Cannot sign in", stored.Subject);
        Assert.Equal("The portal rejects my password.", stored.Description);
        Assert.Equal(TicketCategory.Technical, stored.Category);
        Assert.Equal(TicketPriority.Medium, stored.Priority);   // default
        Assert.Equal(TicketStatus.Open, stored.Status);         // always Open on creation
        Assert.Equal(agentId, stored.CreatedByUserId);          // from the actor, not the client
        Assert.Equal(agentId, stored.AssignedAgentUserId);      // Agent self-assignment
        Assert.Equal(stored.CreatedAtUtc, stored.UpdatedAtUtc);
        Assert.Equal(stored.Id, result.Id);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class FakeTicketRepository : ITicketRepository
    {
        public List<Ticket> Added { get; } = [];
        public void Add(Ticket ticket) => Added.Add(ticket);

        public Task<TicketDetails?> FindDetailsByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default)
        {
            var t = Added.Single(x => x.Id == id);
            return Task.FromResult<TicketDetails?>(new TicketDetails(
                t.Id, t.Subject, t.Description, t.Category, t.Priority, t.Status, t.CustomerId, "Ada Lovelace", "ada@example.com",
                t.CreatedByUserId, "Agent One", t.AssignedAgentUserId, "Agent One", t.CreatedAtUtc, t.UpdatedAtUtc,
                false, null, null, null, null));
        }

        public Task<Ticket?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<TicketDetails>> ListAsync(TicketListFilter filter, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<TicketActivityDetails>?> ListActivityAsync(Guid ticketId, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeCustomerRepository(Guid existingId) : ICustomerRepository
    {
        public Task<CustomerDetails?> FindDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomerDetails?>(id == existingId
                ? new CustomerDetails(id, "Ada", "Lovelace", "ada@example.com", null, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
                : null);

        public void Add(Customer customer) => throw new NotSupportedException();
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerDetails?> FindDetailsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<CustomerDetails>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    // An Agent's ticket needs no account lookup: any call here would mean a client-supplied assignee was considered.
    private sealed class UnusedIdentityService : IIdentityService
    {
        public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount?> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount> CreateUserAsync(NewUserAccount account, string password, string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserAccount>> ListByRoleAsync(string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount> UpdateProfileAsync(Guid userId, UserProfileUpdate update, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.FromResult(1); }
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
