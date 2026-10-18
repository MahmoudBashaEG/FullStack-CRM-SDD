using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Collaboration.GetCollaborationTicket;

public sealed record GetCollaborationTicketQuery(StaffActor Actor, Guid TicketId) : IQuery<CollaborationTicketContext>;

public sealed class GetCollaborationTicketQueryHandler(ICollaborationRepository collaboration)
    : IQueryHandler<GetCollaborationTicketQuery, CollaborationTicketContext>
{
    public async ValueTask<CollaborationTicketContext> Handle(GetCollaborationTicketQuery query, CancellationToken cancellationToken) =>
        await collaboration.GetContextAsync(query.TicketId, query.Actor, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{query.TicketId}' was not found.");
}
