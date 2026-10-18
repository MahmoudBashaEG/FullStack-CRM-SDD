using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.KnowledgeBase.TicketLinks;

/// <summary>Ticket not in the caller's scope (collaborator-only, other Agent's ticket, missing) ⇒ 404.</summary>
public sealed record ListTicketKnowledgeArticlesQuery(StaffActor Actor, Guid TicketId)
    : IQuery<IReadOnlyList<TicketKnowledgeLinkDetails>>;

public sealed class ListTicketKnowledgeArticlesQueryHandler(ITicketKnowledgeLinkRepository links)
    : IQueryHandler<ListTicketKnowledgeArticlesQuery, IReadOnlyList<TicketKnowledgeLinkDetails>>
{
    public async ValueTask<IReadOnlyList<TicketKnowledgeLinkDetails>> Handle(
        ListTicketKnowledgeArticlesQuery query, CancellationToken cancellationToken)
    {
        if (!await links.TicketIsVisibleAsync(query.TicketId, query.Actor.AgentScope, cancellationToken))
        {
            throw new NotFoundException($"Ticket '{query.TicketId}' was not found.");
        }

        return await links.ListForTicketAsync(query.TicketId, cancellationToken);
    }
}
