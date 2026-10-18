using Crm.Application.Abstractions.Persistence;
using Crm.Application.Automation;
using Crm.Application.Reports;
using Crm.Application.Reports.GetSlaReport;
using Crm.Application.Sla;
using Crm.Domain.Automation;
using Crm.Domain.Sla;

namespace Crm.UnitTests.Reports;

public sealed class GetSlaReportQueryHandlerTests
{
    [Fact]
    public async Task Handle_Last7Days_ClassifiesCohortIntoRatesExcludingPendingAndNotApplicableWithNaForUndecidedBuckets()
    {
        var now = new DateTimeOffset(2030, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var d4 = new DateOnly(2030, 1, 4);
        var reporting = new FakeReportingRepository(
        [
            new SlaCohortCell(d4, ReportSlaOutcome.Met, ReportSlaOutcome.Met, 3),
            new SlaCohortCell(d4, ReportSlaOutcome.Breached, ReportSlaOutcome.Pending, 1),       // late response stays breached
            new SlaCohortCell(d4.AddDays(1), ReportSlaOutcome.NotApplicable, ReportSlaOutcome.NotApplicable, 2), // untracked
            new SlaCohortCell(d4.AddDays(2), ReportSlaOutcome.Pending, ReportSlaOutcome.Pending, 1),
        ], activeBreached: 4);
        var clock = new FixedTimeProvider(now);
        var handler = new GetSlaReportQueryHandler(
            reporting, new EmptyMonitoringRepository(),
            new SlaEvaluator(new NoCalendarConfiguration(), new NoWarningRules(), clock), clock);

        var result = await handler.Handle(new GetSlaReportQuery(ReportPreset.Last7Days, null, null), CancellationToken.None);

        // Half-open UTC range of the 7 days ending today, and "now" passed for the pending/breached split.
        Assert.Equal(new DateTimeOffset(2030, 1, 4, 0, 0, 0, TimeSpan.Zero), reporting.StartUtc);
        Assert.Equal(new DateTimeOffset(2030, 1, 11, 0, 0, 0, TimeSpan.Zero), reporting.EndExclusiveUtc);
        Assert.Equal(now, reporting.Now);

        Assert.Equal(new SlaTargetSummary(7, 2, 5, 3, 1, 1, 75.0), result.FirstResponse);   // 3 / (3 + 1)
        Assert.Equal(new SlaTargetSummary(7, 2, 5, 3, 0, 2, 100.0), result.Resolution);     // pending excluded
        Assert.Equal(7, result.Trend.Count);                                                 // daily buckets
        Assert.Equal(75.0, result.Trend[0].ResponseCompliancePercent);
        Assert.Null(result.Trend[1].ResponseCompliancePercent);                              // only not-applicable ⇒ N/A, not 0 %
        Assert.Null(result.Trend[3].ResponseCompliancePercent);                              // empty day ⇒ N/A
        Assert.Equal(0, result.Trend[3].ResponseMet + result.Trend[3].ResponseBreached);
        Assert.Equal(new SlaLiveStatus(4, 0, 0, 0, false, 0, false), result.Live);
        Assert.Equal(now, result.GeneratedAtUtc);
    }

    private sealed class FakeReportingRepository(IReadOnlyList<SlaCohortCell> cells, int activeBreached) : IReportingRepository
    {
        public DateTimeOffset StartUtc { get; private set; }
        public DateTimeOffset EndExclusiveUtc { get; private set; }
        public DateTimeOffset Now { get; private set; }

        public Task<IReadOnlyList<SlaCohortCell>> ListSlaCohortAsync(
            DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            StartUtc = startUtc;
            EndExclusiveUtc = endExclusiveUtc;
            Now = now;
            return Task.FromResult(cells);
        }

        public Task<int> CountActiveBreachedTicketsAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
            Task.FromResult(activeBreached);

        public Task<IReadOnlyList<TicketCohortCell>> ListTicketCohortAsync(DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReportAgent>> ListAgentsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkloadCell>> ListWorkloadAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssigneeCount>> ListOverdueActiveByAssigneeAsync(DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ResolutionActionCount>> ListResolutionActionsAsync(DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RatingCount>> ListFeedbackRatingsAsync(DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FeedbackParticipationCounts> GetFeedbackParticipationAsync(DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RecentFeedback>> ListRecentFeedbackAsync(DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class EmptyMonitoringRepository : ISlaMonitoringRepository
    {
        public Task<IReadOnlyList<SlaOverviewRow>> ListActiveTrackedAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SlaOverviewRow>>([]);

        public Task<IReadOnlyList<SlaCandidate>> ListWarningCandidatesAsync(SlaCandidateQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SlaCandidate>> ListBreachCandidatesAsync(SlaCandidateQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoCalendarConfiguration : ISlaConfigurationRepository
    {
        public Task<BusinessCalendar?> FindCalendarAsync(CancellationToken cancellationToken = default) => Task.FromResult<BusinessCalendar?>(null);

        public Task<SlaPolicy?> FindPolicyAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Add(SlaPolicy policy) => throw new NotSupportedException();
        public void Add(BusinessCalendar calendar) => throw new NotSupportedException();
        public Task<string?> FindUserNameAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoWarningRules : IAutomationRuleRepository
    {
        public Task<IReadOnlyList<AutomationRule>> ListEnabledAsync(AutomationTrigger trigger, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AutomationRule>>([]);

        public void Add(AutomationRule rule) => throw new NotSupportedException();
        public void AddExecution(AutomationRuleExecution execution) => throw new NotSupportedException();
        public void AddAlert(SlaAlert alert) => throw new NotSupportedException();
        public Task<AutomationRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AutomationRuleDetails>> ListDetailsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AutomationRuleDetails?> FindDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AutomationExecutionDetails>> ListRecentExecutionsAsync(int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
