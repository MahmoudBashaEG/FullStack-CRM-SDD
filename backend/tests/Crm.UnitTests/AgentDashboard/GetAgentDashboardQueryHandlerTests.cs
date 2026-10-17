using Crm.Application.Abstractions.Persistence;
using Crm.Application.AgentDashboard;
using Crm.Application.AgentDashboard.GetAgentDashboard;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.AgentDashboard;

public sealed class GetAgentDashboardQueryHandlerTests
{
    [Fact]
    public async Task Handle_Agent_ReturnsOwnScopedOverlappingCountsActiveTotalAndBoundedQueue()
    {
        var agentId = Guid.NewGuid();
        var now = new DateTimeOffset(2030, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var first = Row(TicketPriority.Urgent, escalated: true);
        var second = Row(TicketPriority.High, escalated: false);
        var repository = new FakeAgentDashboardRepository(new AgentWorkloadCounts(3, 2, 1, 2), [first, second]);
        var handler = new GetAgentDashboardQueryHandler(repository, new FixedTimeProvider(now));

        var result = await handler.Handle(new GetAgentDashboardQuery(agentId), CancellationToken.None);

        Assert.Equal(agentId, repository.CountsRequestedFor);
        Assert.Equal(agentId, repository.QueueRequestedFor);
        Assert.Equal(10, repository.RequestedLimit);
        Assert.Equal(new AgentWorkloadSummary(3, 2, 5, 1, 2), result.Summary);
        Assert.Equal([first.Id, second.Id], result.AttentionQueue.Select(t => t.Id).ToArray());
        Assert.Equal(10, result.AttentionQueueLimit);
        Assert.Equal(now, result.GeneratedAtUtc);
    }

    private static AttentionTicket Row(TicketPriority priority, bool escalated) =>
        new(Guid.NewGuid(), "Subject", TicketCategory.Technical, priority, TicketStatus.Open, escalated,
            Guid.NewGuid(), "Ada Lovelace", "ada@example.com", DateTimeOffset.UnixEpoch);

    private sealed class FakeAgentDashboardRepository(AgentWorkloadCounts counts, IReadOnlyList<AttentionTicket> queue) : IAgentDashboardRepository
    {
        public Guid? CountsRequestedFor { get; private set; }
        public Guid? QueueRequestedFor { get; private set; }
        public int RequestedLimit { get; private set; }

        public Task<AgentWorkloadCounts> GetWorkloadCountsAsync(Guid agentUserId, CancellationToken cancellationToken = default)
        {
            CountsRequestedFor = agentUserId;
            return Task.FromResult(counts);
        }

        public Task<IReadOnlyList<AttentionTicket>> ListAttentionQueueAsync(Guid agentUserId, int limit, CancellationToken cancellationToken = default)
        {
            QueueRequestedFor = agentUserId;
            RequestedLimit = limit;
            return Task.FromResult(queue);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
