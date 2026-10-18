using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.AgentTasks;
using Crm.Application.AgentTasks.CreateAgentTask;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Domain.AgentTasks;

namespace Crm.UnitTests.AgentTasks;

public sealed class CreateAgentTaskCommandHandlerTests
{
    [Fact]
    public async Task Handle_AgentCreatesTicketLinkedTaskWithReminder_OwnsPendingTaskAndSchedulesFirstReminderInOneSave()
    {
        var agentId = Guid.CreateVersion7();
        var ticketId = Guid.CreateVersion7();
        var now = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var due = now.AddDays(2);
        var remindAt = now.AddDays(1);
        var actor = new StaffActor(agentId, IsSuperAdmin: false);
        var tasks = new FakeAgentTaskRepository();
        var ticketLock = new FakeTicketAccessLock(new TicketAssignmentLock(ticketId, agentId));
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateAgentTaskCommandHandler(tasks, ticketLock, new FakeIdentityService(), unitOfWork, new FixedTimeProvider(now));

        var result = await handler.Handle(
            new CreateAgentTaskCommand(actor, "  Call back customer  ", "  ", null, ticketId, due, remindAt),
            CancellationToken.None);

        Assert.Equal(ticketId, ticketLock.LockedTicketId);           // link authorized from the LOCKED current assignment
        var task = Assert.IsType<AgentTask>(tasks.Added);
        Assert.Equal("Call back customer", task.Title);              // trimmed
        Assert.Null(task.Description);                               // whitespace ⇒ null
        Assert.Equal(agentId, task.AssignedAgentUserId);             // owner and creator come from the actor
        Assert.Equal(agentId, task.CreatedByUserId);
        Assert.Equal(AgentTaskStatus.Pending, task.Status);
        Assert.Equal(ticketId, task.TicketId);
        Assert.Equal(due, task.DueAtUtc);
        Assert.Equal(remindAt, task.RemindAtUtc);
        Assert.Equal(1, task.ReminderGeneration);                    // first reminder generation
        Assert.Equal(now, task.CreatedAtUtc);
        Assert.Equal(now, task.UpdatedAtUtc);
        Assert.Null(task.CompletedAtUtc);
        Assert.False(task.IsArchived);
        Assert.Equal(1, unitOfWork.SaveCount);                       // explicit save; the pipeline owns the transaction
        Assert.Equal(task.Id, tasks.DetailsRequestedFor);            // read back under the same actor and time
        Assert.Equal(actor, tasks.DetailsActor);
        Assert.Equal(now, tasks.DetailsNow);
        Assert.Equal(task.Id, result.Id);
        Assert.Equal(FakeAgentTaskRepository.StoredVersion, result.Version);
    }

    private sealed class FakeAgentTaskRepository : IAgentTaskRepository
    {
        public static readonly byte[] StoredVersion = [0, 0, 0, 0, 0, 0, 0, 1];

        public AgentTask? Added { get; private set; }
        public Guid? DetailsRequestedFor { get; private set; }
        public StaffActor? DetailsActor { get; private set; }
        public DateTimeOffset? DetailsNow { get; private set; }

        public void Add(AgentTask task) => Added = task;

        public Task<AgentTaskDetails?> FindDetailsByIdAsync(Guid id, StaffActor actor, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            DetailsRequestedFor = id;
            DetailsActor = actor;
            DetailsNow = now;
            var t = Added!;
            return Task.FromResult<AgentTaskDetails?>(new AgentTaskDetails(
                t.Id, t.Title, t.Description, t.Status, t.AssignedAgentUserId, "Agent One", t.CreatedByUserId, "Agent One",
                t.DueAtUtc, t.RemindAtUtc, null, LinkedTicketState.Accessible, t.TicketId, "Ticket subject", false, true,
                t.CreatedAtUtc, t.UpdatedAtUtc, t.CompletedAtUtc, t.IsArchived, t.ArchivedAtUtc, StoredVersion));
        }

        public Task<AgentTask?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<AgentTaskDetails>> ListAsync(AgentTaskListFilter filter, StaffActor actor, DateTimeOffset now, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgentTaskCounts> GetPendingCountsAsync(Guid agentUserId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LinkableTicket>> ListLinkableTicketsAsync(Guid ownerUserId, string? search, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeTicketAccessLock(TicketAssignmentLock snapshot) : ITicketAccessLock
    {
        public Guid? LockedTicketId { get; private set; }

        public Task<TicketAssignmentLock?> LockTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
        {
            LockedTicketId = ticketId;
            return Task.FromResult<TicketAssignmentLock?>(snapshot);
        }

        public Task<bool> LockActiveMembershipAsync(Guid ticketId, Guid agentUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    // Agents never pick an owner, so the identity service must not be touched at all.
    private sealed class FakeIdentityService : IIdentityService
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
            throw new NotSupportedException(); // the pipeline, not the handler, owns the transaction
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
