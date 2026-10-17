using Crm.Application.Abstractions.Persistence;
using Mediator;

namespace Crm.Application.AgentDashboard.GetAgentDashboard;

/// <summary>AgentUserId comes from the validated JWT (sub) only — never from the request.</summary>
public sealed record GetAgentDashboardQuery(Guid AgentUserId) : IQuery<AgentDashboardSnapshot>;

public sealed class GetAgentDashboardQueryHandler(IAgentDashboardRepository dashboard, TimeProvider timeProvider)
    : IQueryHandler<GetAgentDashboardQuery, AgentDashboardSnapshot>
{
    public async ValueTask<AgentDashboardSnapshot> Handle(GetAgentDashboardQuery query, CancellationToken cancellationToken)
    {
        var counts = await dashboard.GetWorkloadCountsAsync(query.AgentUserId, cancellationToken);
        var queue = await dashboard.ListAttentionQueueAsync(query.AgentUserId, AgentDashboardRules.AttentionQueueLimit, cancellationToken);

        var summary = new AgentWorkloadSummary(
            counts.Open, counts.InProgress, counts.Open + counts.InProgress, counts.Escalated, counts.HighOrUrgent);
        return new AgentDashboardSnapshot(summary, queue, AgentDashboardRules.AttentionQueueLimit, timeProvider.GetUtcNow());
    }
}
