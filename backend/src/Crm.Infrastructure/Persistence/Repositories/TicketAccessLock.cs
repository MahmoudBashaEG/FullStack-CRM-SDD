using Crm.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class TicketAccessLock(CrmDbContext dbContext) : ITicketAccessLock
{
    private sealed class AssignmentRow
    {
        public Guid TicketId { get; init; }
        public Guid? AssignedAgentUserId { get; init; }
    }

    // UPDLOCK: also under READ_COMMITTED_SNAPSHOT, reads the latest COMMITTED row and blocks writers until commit.
    // HOLDLOCK: keeps the lock (and the key range when no row matches) to the end of the transaction. Reads only.
    public async Task<TicketAssignmentLock?> LockTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        EnsureTransaction();
        var rows = await dbContext.Database.SqlQuery<AssignmentRow>($"""
            SELECT [Id] AS [TicketId], [AssignedAgentUserId]
            FROM [Tickets] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
            WHERE [Id] = {ticketId}
            """).ToListAsync(cancellationToken); // materialize as written: no EF subquery wrapping around the hinted statement
        return rows.SingleOrDefault() is { } row ? new TicketAssignmentLock(row.TicketId, row.AssignedAgentUserId) : null;
    }

    public async Task<bool> LockActiveMembershipAsync(Guid ticketId, Guid agentUserId, CancellationToken cancellationToken = default)
    {
        EnsureTransaction();
        var rows = await dbContext.Database.SqlQuery<int>($"""
            SELECT 1 AS [Value]
            FROM [TicketCollaborators] WITH (UPDLOCK, HOLDLOCK, ROWLOCK, INDEX([UX_TicketCollaborators_ActiveMembership]))
            WHERE [TicketId] = {ticketId} AND [AgentUserId] = {agentUserId} AND [RemovedAtUtc] IS NULL
            """).ToListAsync(cancellationToken);
        return rows.Count > 0;
    }

    private void EnsureTransaction()
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("ITicketAccessLock must be used inside an ITransactionalCommand.");
        }
    }
}
