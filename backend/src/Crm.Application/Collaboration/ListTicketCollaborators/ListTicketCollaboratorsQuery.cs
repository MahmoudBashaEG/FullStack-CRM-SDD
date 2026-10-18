using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Collaboration.ListTicketCollaborators;

/// <summary>Everyone with discussion access may see who is in the discussion.</summary>
public sealed record ListTicketCollaboratorsQuery(StaffActor Actor, Guid TicketId) : IQuery<IReadOnlyList<TicketCollaboratorDetails>>;

public sealed class ListTicketCollaboratorsQueryHandler(ICollaborationRepository collaboration)
    : IQueryHandler<ListTicketCollaboratorsQuery, IReadOnlyList<TicketCollaboratorDetails>>
{
    public async ValueTask<IReadOnlyList<TicketCollaboratorDetails>> Handle(
        ListTicketCollaboratorsQuery query, CancellationToken cancellationToken)
    {
        _ = await collaboration.GetContextAsync(query.TicketId, query.Actor, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{query.TicketId}' was not found.");
        return await collaboration.ListCollaboratorsAsync(query.TicketId, cancellationToken);
    }
}
