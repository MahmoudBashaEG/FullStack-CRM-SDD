using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Identity;
using Mediator;

namespace Crm.Application.Collaboration.ListCollaboratorCandidates;

/// <summary>Agents cannot call /agents, so managers get this narrow picker: active Agents except the primary and current collaborators.</summary>
public sealed record ListCollaboratorCandidatesQuery(StaffActor Actor, Guid TicketId) : IQuery<IReadOnlyList<CollaboratorCandidate>>;

public sealed class ListCollaboratorCandidatesQueryHandler(ICollaborationRepository collaboration, IIdentityService identity)
    : IQueryHandler<ListCollaboratorCandidatesQuery, IReadOnlyList<CollaboratorCandidate>>
{
    public async ValueTask<IReadOnlyList<CollaboratorCandidate>> Handle(
        ListCollaboratorCandidatesQuery query, CancellationToken cancellationToken)
    {
        var context = await collaboration.GetContextAsync(query.TicketId, query.Actor, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{query.TicketId}' was not found.");
        if (!context.CanManage)
        {
            throw new ForbiddenException("Only SuperAdmin or the ticket's assigned Agent can manage collaborators.");
        }

        var current = (await collaboration.ListCollaboratorsAsync(query.TicketId, cancellationToken))
            .Select(c => c.AgentUserId).ToHashSet();
        var agents = await identity.ListByRoleAsync(UserRoles.Agent, cancellationToken);
        return agents
            .Where(a => a.IsActive && a.Id != context.AssignedAgentUserId && a.Id != query.Actor.UserId && !current.Contains(a.Id))
            .Select(a => new CollaboratorCandidate(a.Id, $"{a.FirstName} {a.LastName}"))
            .ToList();
    }
}
