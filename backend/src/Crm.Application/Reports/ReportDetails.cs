using Crm.Domain.Tickets;

namespace Crm.Application.Reports;

public sealed record ReportCount<TKey>(TKey Value, int Count);

public sealed record ReportTrendPoint(DateOnly Start, DateOnly End, int Count);

// ---- Raw aggregated cells returned by IReportingRepository (already grouped in SQL) ----

public sealed record TicketCohortCell(DateOnly Day, TicketStatus Status, TicketCategory Category, TicketPriority Priority, int Count);

/// <summary>R2: one outcome per tracked target; NotApplicable = ticket not SLA-tracked.</summary>
public enum ReportSlaOutcome
{
    NotApplicable = 0,
    Met = 1,
    Breached = 2,
    Pending = 3,
}

public sealed record SlaCohortCell(DateOnly Day, ReportSlaOutcome FirstResponse, ReportSlaOutcome Resolution, int Count);

public sealed record ReportAgent(Guid UserId, string Name, bool IsActive);

/// <summary>All tickets grouped by CURRENT assignee (null = unassigned), status and priority.</summary>
public sealed record WorkloadCell(Guid? AgentUserId, TicketStatus Status, TicketPriority Priority, int Count);

public sealed record AssigneeCount(Guid? AgentUserId, int Count);

public sealed record ResolutionActionCount(Guid ActorUserId, int Actions, int DistinctTickets);

public sealed record RatingCount(int Rating, int Count);

public sealed record FeedbackParticipationCounts(int EligibleTickets, int TicketsWithFeedback);

/// <summary>R13 minimal context: no customer, assignee or internal data.</summary>
public sealed record RecentFeedback(
    Guid TicketId, string TicketSubject, int Rating, string? Comment, DateTimeOffset SubmittedAtUtc, DateTimeOffset UpdatedAtUtc);

// ---- Report results ----

public sealed record TicketReport(
    ReportPeriod Period,
    int CreatedTickets,
    IReadOnlyList<ReportCount<TicketStatus>> ByStatus,
    IReadOnlyList<ReportCount<TicketCategory>> ByCategory,
    IReadOnlyList<ReportCount<TicketPriority>> ByPriority,
    IReadOnlyList<ReportTrendPoint> Trend,
    DateTimeOffset GeneratedAtUtc);

/// <summary>CohortTickets = NotApplicable + Met + Breached + Pending. Applicable = CohortTickets − NotApplicable. CompliancePercent = Met / (Met + Breached).</summary>
public sealed record SlaTargetSummary(int CohortTickets, int NotApplicable, int Applicable, int Met, int Breached, int Pending, double? CompliancePercent);

public sealed record SlaTrendBucket(
    DateOnly Start, DateOnly End,
    int ResponseMet, int ResponseBreached, double? ResponseCompliancePercent,
    int ResolutionMet, int ResolutionBreached, double? ResolutionCompliancePercent);

/// <summary>Live, not affected by the period. At-risk = SLA Warning over at most ScannedActiveTickets (R1).</summary>
public sealed record SlaLiveStatus(
    int ActiveTicketsWithBreach, int ResponseAtRisk, int ResolutionAtRisk,
    int ScannedActiveTickets, bool ScanLimitReached, int EnabledWarningRules, bool CalendarConfigured);

public sealed record SlaReport(
    ReportPeriod Period, SlaTargetSummary FirstResponse, SlaTargetSummary Resolution, IReadOnlyList<SlaTrendBucket> Trend,
    SlaLiveStatus Live, DateTimeOffset GeneratedAtUtc);

public sealed record AgentPerformanceRow(
    Guid AgentUserId, string Name, bool IsActive,
    int Open, int InProgress, int ActiveAssigned, int OverdueActive, int Resolved, int Closed,
    IReadOnlyList<ReportCount<TicketPriority>> ActiveByPriority,
    int ResolutionActions, int ResolvedTicketsDistinct);

/// <summary>OpenTickets/InProgressTickets cover ALL tickets (assigned or not) — the live totals used by the dashboard (R10).</summary>
public sealed record AgentPerformanceTotals(
    int OpenTickets, int InProgressTickets, int ActiveAssignedToAgents, int OverdueActiveAssignedToAgents,
    int UnassignedActive, int ResolutionActionsByAgents, int ResolutionActionsByOthers);

public sealed record AgentPerformanceReport(
    ReportPeriod Period, IReadOnlyList<AgentPerformanceRow> Agents, AgentPerformanceTotals Totals, DateTimeOffset GeneratedAtUtc);

public sealed record FeedbackParticipation(int EligibleTickets, int TicketsWithFeedback, double? Percent);

public sealed record CustomerSatisfactionReport(
    ReportPeriod Period, int FeedbackCount, double? AverageRating, int PositiveCount, double? PositivePercent,
    IReadOnlyList<ReportCount<int>> Distribution, FeedbackParticipation Participation,
    IReadOnlyList<RecentFeedback> Recent, int RecentLimit, DateTimeOffset GeneratedAtUtc);
