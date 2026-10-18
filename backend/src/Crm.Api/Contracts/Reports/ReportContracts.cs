using Crm.Application.Reports;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Reports;

public sealed record ReportPeriodResponse(
    ReportPreset Preset, DateOnly From, DateOnly To, DateTimeOffset StartUtc, DateTimeOffset EndExclusiveUtc, int BucketDays, int Days)
{
    public static ReportPeriodResponse Of(ReportPeriod p) => new(p.Preset, p.From, p.To, p.StartUtc, p.EndExclusiveUtc, p.BucketDays, p.Days);
}

public sealed record ReportCountResponse<TKey>(TKey Value, int Count);

public sealed record ReportTrendPointResponse(DateOnly Start, DateOnly End, int Count);

public sealed record TicketReportResponse(
    ReportPeriodResponse Period, int CreatedTickets,
    IReadOnlyList<ReportCountResponse<TicketStatus>> ByStatus,
    IReadOnlyList<ReportCountResponse<TicketCategory>> ByCategory,
    IReadOnlyList<ReportCountResponse<TicketPriority>> ByPriority,
    IReadOnlyList<ReportTrendPointResponse> Trend, DateTimeOffset GeneratedAtUtc)
{
    public static TicketReportResponse From(TicketReport r) => new(
        ReportPeriodResponse.Of(r.Period), r.CreatedTickets,
        r.ByStatus.Select(c => new ReportCountResponse<TicketStatus>(c.Value, c.Count)).ToList(),
        r.ByCategory.Select(c => new ReportCountResponse<TicketCategory>(c.Value, c.Count)).ToList(),
        r.ByPriority.Select(c => new ReportCountResponse<TicketPriority>(c.Value, c.Count)).ToList(),
        r.Trend.Select(t => new ReportTrendPointResponse(t.Start, t.End, t.Count)).ToList(), r.GeneratedAtUtc);
}

/// <summary>CompliancePercent is 0-100 with 1 decimal, null = N/A (nothing met or breached yet).</summary>
public sealed record SlaTargetSummaryResponse(
    int CohortTickets, int NotApplicable, int Applicable, int Met, int Breached, int Pending, double? CompliancePercent)
{
    public static SlaTargetSummaryResponse From(SlaTargetSummary s) =>
        new(s.CohortTickets, s.NotApplicable, s.Applicable, s.Met, s.Breached, s.Pending, s.CompliancePercent);
}

public sealed record SlaTrendBucketResponse(
    DateOnly Start, DateOnly End,
    int ResponseMet, int ResponseBreached, double? ResponseCompliancePercent,
    int ResolutionMet, int ResolutionBreached, double? ResolutionCompliancePercent)
{
    public static SlaTrendBucketResponse From(SlaTrendBucket b) => new(
        b.Start, b.End, b.ResponseMet, b.ResponseBreached, b.ResponseCompliancePercent,
        b.ResolutionMet, b.ResolutionBreached, b.ResolutionCompliancePercent);
}

/// <summary>Live: not affected by the report period.</summary>
public sealed record SlaLiveStatusResponse(
    int ActiveTicketsWithBreach, int ResponseAtRisk, int ResolutionAtRisk,
    int ScannedActiveTickets, bool ScanLimitReached, int EnabledWarningRules, bool CalendarConfigured)
{
    public static SlaLiveStatusResponse From(SlaLiveStatus l) => new(
        l.ActiveTicketsWithBreach, l.ResponseAtRisk, l.ResolutionAtRisk,
        l.ScannedActiveTickets, l.ScanLimitReached, l.EnabledWarningRules, l.CalendarConfigured);
}

public sealed record SlaReportResponse(
    ReportPeriodResponse Period, SlaTargetSummaryResponse FirstResponse, SlaTargetSummaryResponse Resolution,
    IReadOnlyList<SlaTrendBucketResponse> Trend, SlaLiveStatusResponse Live, DateTimeOffset GeneratedAtUtc)
{
    public static SlaReportResponse From(SlaReport r) => new(
        ReportPeriodResponse.Of(r.Period), SlaTargetSummaryResponse.From(r.FirstResponse), SlaTargetSummaryResponse.From(r.Resolution),
        r.Trend.Select(SlaTrendBucketResponse.From).ToList(), SlaLiveStatusResponse.From(r.Live), r.GeneratedAtUtc);
}

public sealed record AgentPerformanceRowResponse(
    Guid AgentUserId, string Name, bool IsActive,
    int Open, int InProgress, int ActiveAssigned, int OverdueActive, int Resolved, int Closed,
    IReadOnlyList<ReportCountResponse<TicketPriority>> ActiveByPriority,
    int ResolutionActions, int ResolvedTicketsDistinct)
{
    public static AgentPerformanceRowResponse From(AgentPerformanceRow a) => new(
        a.AgentUserId, a.Name, a.IsActive, a.Open, a.InProgress, a.ActiveAssigned, a.OverdueActive, a.Resolved, a.Closed,
        a.ActiveByPriority.Select(c => new ReportCountResponse<TicketPriority>(c.Value, c.Count)).ToList(),
        a.ResolutionActions, a.ResolvedTicketsDistinct);
}

public sealed record AgentPerformanceTotalsResponse(
    int OpenTickets, int InProgressTickets, int ActiveAssignedToAgents, int OverdueActiveAssignedToAgents,
    int UnassignedActive, int ResolutionActionsByAgents, int ResolutionActionsByOthers)
{
    public static AgentPerformanceTotalsResponse From(AgentPerformanceTotals t) => new(
        t.OpenTickets, t.InProgressTickets, t.ActiveAssignedToAgents, t.OverdueActiveAssignedToAgents,
        t.UnassignedActive, t.ResolutionActionsByAgents, t.ResolutionActionsByOthers);
}

public sealed record AgentPerformanceReportResponse(
    ReportPeriodResponse Period, IReadOnlyList<AgentPerformanceRowResponse> Agents, AgentPerformanceTotalsResponse Totals,
    DateTimeOffset GeneratedAtUtc)
{
    public static AgentPerformanceReportResponse From(AgentPerformanceReport r) => new(
        ReportPeriodResponse.Of(r.Period), r.Agents.Select(AgentPerformanceRowResponse.From).ToList(),
        AgentPerformanceTotalsResponse.From(r.Totals), r.GeneratedAtUtc);
}

public sealed record FeedbackParticipationResponse(int EligibleTickets, int TicketsWithFeedback, double? Percent)
{
    public static FeedbackParticipationResponse From(FeedbackParticipation p) => new(p.EligibleTickets, p.TicketsWithFeedback, p.Percent);
}

/// <summary>Ticket id + subject only; no customer, assignee or internal data.</summary>
public sealed record RecentFeedbackResponse(
    Guid TicketId, string TicketSubject, int Rating, string? Comment, DateTimeOffset SubmittedAtUtc, DateTimeOffset UpdatedAtUtc)
{
    public static RecentFeedbackResponse From(RecentFeedback f) =>
        new(f.TicketId, f.TicketSubject, f.Rating, f.Comment, f.SubmittedAtUtc, f.UpdatedAtUtc);
}

public sealed record CustomerSatisfactionReportResponse(
    ReportPeriodResponse Period, int FeedbackCount, double? AverageRating, int PositiveCount, double? PositivePercent,
    IReadOnlyList<ReportCountResponse<int>> Distribution, FeedbackParticipationResponse Participation,
    IReadOnlyList<RecentFeedbackResponse> Recent, int RecentLimit, DateTimeOffset GeneratedAtUtc)
{
    public static CustomerSatisfactionReportResponse From(CustomerSatisfactionReport r) => new(
        ReportPeriodResponse.Of(r.Period), r.FeedbackCount, r.AverageRating, r.PositiveCount, r.PositivePercent,
        r.Distribution.Select(c => new ReportCountResponse<int>(c.Value, c.Count)).ToList(),
        FeedbackParticipationResponse.From(r.Participation),
        r.Recent.Select(RecentFeedbackResponse.From).ToList(), r.RecentLimit, r.GeneratedAtUtc);
}
