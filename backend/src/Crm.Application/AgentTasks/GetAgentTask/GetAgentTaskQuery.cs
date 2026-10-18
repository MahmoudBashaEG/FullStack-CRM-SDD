using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.AgentTasks.GetAgentTask;

/// <summary>Agent: own tasks only (others ⇒ 404). SuperAdmin: any task.</summary>
public sealed record GetAgentTaskQuery(StaffActor Actor, Guid Id) : IQuery<AgentTaskDetails>;

public sealed class GetAgentTaskQueryHandler(IAgentTaskRepository tasks, TimeProvider timeProvider)
    : IQueryHandler<GetAgentTaskQuery, AgentTaskDetails>
{
    public async ValueTask<AgentTaskDetails> Handle(GetAgentTaskQuery query, CancellationToken cancellationToken) =>
        await tasks.FindDetailsByIdAsync(query.Id, query.Actor, timeProvider.GetUtcNow(), cancellationToken)
            ?? throw new NotFoundException($"Task '{query.Id}' was not found.");
}
