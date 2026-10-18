using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Validation;
using Crm.Domain.AgentTasks;
using Mediator;

namespace Crm.Application.AgentTasks.GetAgentTaskSummary;

/// <summary>Agent dashboard tasks section: counts over ALL the Agent's Pending, unarchived tasks plus the next few by due time.</summary>
public sealed record GetAgentTaskSummaryQuery(Guid AgentUserId) : IQuery<AgentTaskSummary>;

public sealed class GetAgentTaskSummaryQueryHandler(IAgentTaskRepository tasks, TimeProvider timeProvider)
    : IQueryHandler<GetAgentTaskSummaryQuery, AgentTaskSummary>
{
    public async ValueTask<AgentTaskSummary> Handle(GetAgentTaskSummaryQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var counts = await tasks.GetPendingCountsAsync(query.AgentUserId, now, cancellationToken);
        var next = await tasks.ListAsync(
            new AgentTaskListFilter(null, AgentTaskStatus.Pending, null, false, null, null),
            new StaffActor(query.AgentUserId, false), now, 1, AgentTaskRules.DashboardPreviewLimit, cancellationToken);
        return new AgentTaskSummary(counts, next.Items, AgentTaskRules.DashboardPreviewLimit, now);
    }
}
