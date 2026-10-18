using Crm.Application.Sla;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>Worker and overview queries over SLA-tracked Open/InProgress tickets. No tracking; no scope (system / SuperAdmin only).</summary>
public interface ISlaMonitoringRepository
{
    /// <summary>Pending target (not completed), due instant in the future. Breach and warning queries both apply, IN SQL and before Take(limit):
    /// Open/InProgress, tracked, due >= query.EnabledSinceUtc, the rule's CategoryCondition / PriorityCondition / AssignedCondition (each only
    /// when set), no execution for (rule, ticket, kind), Id NOT IN query.ExcludedTicketIds. Ordered due ASC, Id ASC (deterministic).</summary>
    Task<IReadOnlyList<SlaCandidate>> ListWarningCandidatesAsync(SlaCandidateQuery query, CancellationToken cancellationToken = default);

    /// <summary>due &lt;= now AND (completed IS NULL OR completed > due), plus every filter listed above.</summary>
    Task<IReadOnlyList<SlaCandidate>> ListBreachCandidatesAsync(SlaCandidateQuery query, CancellationToken cancellationToken = default);

    /// <summary>Tracked Open/InProgress tickets with at least one uncompleted target, for the overview (at most limit, oldest due first).</summary>
    Task<IReadOnlyList<SlaOverviewRow>> ListActiveTrackedAsync(int limit, CancellationToken cancellationToken = default);
}

public sealed record SlaCandidate(Guid TicketId, TicketCategory Category, TicketPriority Priority, Guid? AssignedAgentUserId, DateTimeOffset DueAtUtc);

/// <summary>F2: one page for one (rule, kind). ExcludedTicketIds = tickets already attempted in this pass (failed/skipped) — never retried twice in a pass.</summary>
public sealed record SlaCandidateQuery(
    SlaKind Kind, TicketCategory? CategoryCondition, TicketPriority? PriorityCondition, bool? AssignedCondition,
    Guid RuleId, DateTimeOffset Now, DateTimeOffset EnabledSinceUtc, IReadOnlyCollection<Guid> ExcludedTicketIds, int Limit);

public sealed record SlaOverviewRow(Guid TicketId, string Subject, TicketStatus Status, string? AssignedAgentName, TicketSlaInput Sla);
