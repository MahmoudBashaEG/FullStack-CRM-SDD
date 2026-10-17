using Crm.Application.AgentDashboard;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Read-only DASH-001 projections. Both methods are restricted in SQL to tickets whose CURRENT AssignedAgentUserId
/// equals agentUserId (same filter as ITicketRepository's agentScope) and to Open/InProgress tickets.
/// </summary>
public interface IAgentDashboardRepository
{
    /// <summary>One aggregate statement; all zeros when the Agent has no active tickets.</summary>
    Task<AgentWorkloadCounts> GetWorkloadCountsAsync(Guid agentUserId, CancellationToken cancellationToken = default);

    /// <summary>Escalated first, then Urgent > High > Medium > Low, then oldest UpdatedAtUtc, then Id. Ordered and limited in SQL.</summary>
    Task<IReadOnlyList<AttentionTicket>> ListAttentionQueueAsync(Guid agentUserId, int limit, CancellationToken cancellationToken = default);
}
