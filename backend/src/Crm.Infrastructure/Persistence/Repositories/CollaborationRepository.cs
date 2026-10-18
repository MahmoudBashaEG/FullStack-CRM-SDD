using Crm.Application.Abstractions.Persistence;
using Crm.Application.Collaboration;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Domain.Collaboration;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class CollaborationRepository(CrmDbContext dbContext) : ICollaborationRepository
{
    // THE collaboration access rule, evaluated from current rows in every statement.
    // Does NOT use or change TicketRepository.Scope: collaborators never become ticket assignees.
    private IQueryable<Ticket> Accessible(StaffActor actor)
    {
        if (actor.IsSuperAdmin)
        {
            return dbContext.Tickets.AsNoTracking();
        }

        var agentId = actor.UserId;
        return dbContext.Tickets.AsNoTracking().Where(t =>
            t.AssignedAgentUserId == agentId
            || (t.AssignedAgentUserId != null
                && dbContext.TicketCollaborators.Any(c => c.TicketId == t.Id && c.AgentUserId == agentId && c.RemovedAtUtc == null)));
    }

    public Task<CollaborationTicketContext?> GetContextAsync(Guid ticketId, StaffActor actor, CancellationToken cancellationToken = default)
    {
        var isSuperAdmin = actor.IsSuperAdmin;
        var agentId = actor.UserId;
        return (from t in Accessible(actor).Where(t => t.Id == ticketId)
                from agent in dbContext.Users.Where(u => u.Id == t.AssignedAgentUserId).DefaultIfEmpty()
                select new CollaborationTicketContext(
                    t.Id, t.Subject, t.Status, t.Priority, t.AssignedAgentUserId,
                    agent == null ? null : agent.FirstName + " " + agent.LastName,
                    isSuperAdmin ? CollaborationAccess.SuperAdmin
                    : t.AssignedAgentUserId == agentId ? CollaborationAccess.PrimaryAgent
                    : CollaborationAccess.Collaborator))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<TicketMessageDetails>?> ListMessagesAsync(
        Guid ticketId, StaffActor actor, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var visible = Accessible(actor).Where(t => t.Id == ticketId);
        if (!await visible.AnyAsync(cancellationToken))
        {
            return null;
        }

        // The access predicate is part of both statements (count and page).
        var totalCount = await (from m in dbContext.TicketInternalMessages
                                join t in visible on m.TicketId equals t.Id
                                select m.Id).CountAsync(cancellationToken);
        var items = await (from m in dbContext.TicketInternalMessages.AsNoTracking()
                           join t in visible on m.TicketId equals t.Id
                           join author in dbContext.Users on m.AuthorUserId equals author.Id
                           orderby m.CreatedAtUtc descending, m.Id descending
                           select new TicketMessageDetails(
                               m.Id, m.TicketId, m.AuthorUserId, author.FirstName + " " + author.LastName, m.Body, m.CreatedAtUtc))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<TicketMessageDetails>(items, page, pageSize, totalCount);
    }

    public void AddMessage(TicketInternalMessage message) => dbContext.TicketInternalMessages.Add(message);

    public Task<TicketMessageDetails> GetMessageAsync(long id, CancellationToken cancellationToken = default) =>
        (from m in dbContext.TicketInternalMessages.AsNoTracking().Where(m => m.Id == id)
         join author in dbContext.Users on m.AuthorUserId equals author.Id
         select new TicketMessageDetails(
             m.Id, m.TicketId, m.AuthorUserId, author.FirstName + " " + author.LastName, m.Body, m.CreatedAtUtc))
        .SingleAsync(cancellationToken);

    public async Task<IReadOnlyList<TicketCollaboratorDetails>> ListCollaboratorsAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        await (from c in dbContext.TicketCollaborators.AsNoTracking().Where(c => c.TicketId == ticketId && c.RemovedAtUtc == null)
               join agent in dbContext.Users on c.AgentUserId equals agent.Id
               join addedBy in dbContext.Users on c.AddedByUserId equals addedBy.Id
               orderby agent.LastName, agent.FirstName, c.Id
               select new TicketCollaboratorDetails(
                   c.AgentUserId, agent.FirstName + " " + agent.LastName, agent.IsActive,
                   addedBy.FirstName + " " + addedBy.LastName, c.AddedAtUtc))
            .ToListAsync(cancellationToken);

    public Task<TicketCollaborator?> GetActiveMembershipAsync(Guid ticketId, Guid agentUserId, CancellationToken cancellationToken = default) =>
        dbContext.TicketCollaborators.SingleOrDefaultAsync(
            c => c.TicketId == ticketId && c.AgentUserId == agentUserId && c.RemovedAtUtc == null, cancellationToken);

    public void AddCollaborator(TicketCollaborator collaborator) => dbContext.TicketCollaborators.Add(collaborator);

    public async Task<PagedResult<MyCollaborationDetails>> ListMyCollaborationsAsync(
        Guid agentUserId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var memberships = dbContext.TicketCollaborators.AsNoTracking().Where(c => c.AgentUserId == agentUserId && c.RemovedAtUtc == null);
        var totalCount = await memberships.CountAsync(cancellationToken);
        var items = await (from c in memberships
                           join t in dbContext.Tickets on c.TicketId equals t.Id
                           orderby c.AddedAtUtc descending, c.Id descending
                           select new MyCollaborationDetails(
                               t.Id, t.Subject, t.Status, t.Priority,
                               t.AssignedAgentUserId == null,
                               t.AssignedAgentUserId == agentUserId,
                               c.AddedAtUtc,
                               dbContext.TicketInternalMessages.Where(m => m.TicketId == t.Id).Max(m => (DateTimeOffset?)m.CreatedAtUtc)))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<MyCollaborationDetails>(items, page, pageSize, totalCount);
    }
}
