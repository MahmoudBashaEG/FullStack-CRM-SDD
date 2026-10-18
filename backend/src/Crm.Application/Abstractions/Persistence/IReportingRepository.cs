using Crm.Application.Reports;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// REPORT-001 read-only aggregates (SuperAdmin, organisation-wide; no scope). Every method is one grouped SQL statement
/// (participation: two counts) over AsNoTracking queries; nothing is written. Ranges are half-open [startUtc, endExclusiveUtc).
/// </summary>
public interface IReportingRepository
{
    /// <summary>Tickets created in range, grouped by UTC creation day and CURRENT status, category, priority.</summary>
    Task<IReadOnlyList<TicketCohortCell>> ListTicketCohortAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default);

    /// <summary>Tickets created in range, grouped by UTC creation day and the SlaEvaluator outcome of each target at "now" (R2).</summary>
    Task<IReadOnlyList<SlaCohortCell>> ListSlaCohortAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Open/InProgress tickets with at least one breached target (TicketRepository.IsBreached). Live.</summary>
    Task<int> CountActiveBreachedTicketsAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Accounts in the Agent role (active and inactive), ordered LastName, FirstName, Id.</summary>
    Task<IReadOnlyList<ReportAgent>> ListAgentsAsync(CancellationToken cancellationToken = default);

    /// <summary>All tickets grouped by current assignee (null = unassigned), status, priority. Live.</summary>
    Task<IReadOnlyList<WorkloadCell>> ListWorkloadAsync(CancellationToken cancellationToken = default);

    /// <summary>Open/InProgress tickets with IsBreached(now), grouped by current assignee — once per ticket (R6). Live.</summary>
    Task<IReadOnlyList<AssigneeCount>> ListOverdueActiveByAssigneeAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>StatusChanged → Resolved events in range grouped by recorded actor (R5). Assignment is never read.</summary>
    Task<IReadOnlyList<ResolutionActionCount>> ListResolutionActionsAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default);

    /// <summary>Feedback first submitted in range (TicketFeedback.CreatedAtUtc) grouped by CURRENT rating.</summary>
    Task<IReadOnlyList<RatingCount>> ListFeedbackRatingsAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default);

    /// <summary>R7: tickets created in range and currently Resolved/Closed; how many have a feedback row (any submission date).</summary>
    Task<FeedbackParticipationCounts> GetFeedbackParticipationAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default);

    /// <summary>Feedback first submitted in range, newest CreatedAtUtc first (Id tie-breaker), at most limit.</summary>
    Task<IReadOnlyList<RecentFeedback>> ListRecentFeedbackAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, int limit, CancellationToken cancellationToken = default);
}
