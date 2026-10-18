using Crm.Application.Collaboration;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Domain.Collaboration;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Collaboration access is evaluated from CURRENT state in SQL on every call (never cached):
/// SuperAdmin ⇒ any ticket; Agent ⇒ ticket assigned to them, or assigned to someone AND an active membership exists for them.
/// Methods taking StaffActor return null when the ticket is missing or not accessible.
/// </summary>
public interface ICollaborationRepository
{
    Task<CollaborationTicketContext?> GetContextAsync(Guid ticketId, StaffActor actor, CancellationToken cancellationToken = default);

    /// <summary>Newest first (CreatedAtUtc DESC, Id DESC). The access predicate is part of BOTH statements.</summary>
    Task<PagedResult<TicketMessageDetails>?> ListMessagesAsync(
        Guid ticketId, StaffActor actor, int page, int pageSize, CancellationToken cancellationToken = default);

    void AddMessage(TicketInternalMessage message);
    Task<TicketMessageDetails> GetMessageAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Active memberships only, ordered by Agent last name, first name.</summary>
    Task<IReadOnlyList<TicketCollaboratorDetails>> ListCollaboratorsAsync(Guid ticketId, CancellationToken cancellationToken = default);

    /// <summary>Tracked active membership, or null.</summary>
    Task<TicketCollaborator?> GetActiveMembershipAsync(Guid ticketId, Guid agentUserId, CancellationToken cancellationToken = default);

    void AddCollaborator(TicketCollaborator collaborator);

    /// <summary>Active memberships of agentUserId, newest invitation first (AddedAtUtc DESC, Id DESC).</summary>
    Task<PagedResult<MyCollaborationDetails>> ListMyCollaborationsAsync(
        Guid agentUserId, int page, int pageSize, CancellationToken cancellationToken = default);
}
