using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Validation;
using Crm.Application.Sla;
using FluentValidation;
using Mediator;

namespace Crm.Application.Reports.GetSlaReport;

public sealed record GetSlaReportQuery(ReportPreset Preset, DateOnly? From, DateOnly? To) : IQuery<SlaReport>, IReportPeriodQuery;

/// <summary>
/// Cohort outcomes (R2) for tickets created in the period, plus a LIVE block (R1): breached active tickets over all active tickets
/// and at-risk targets via the shared SlaEvaluator over the same bounded scan as the /sla overview. Read-only.
/// </summary>
public sealed class GetSlaReportQueryHandler(
    IReportingRepository reporting,
    ISlaMonitoringRepository monitoring,
    SlaEvaluator evaluator,
    TimeProvider timeProvider) : IQueryHandler<GetSlaReportQuery, SlaReport>
{
    public async ValueTask<SlaReport> Handle(GetSlaReportQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var period = ReportPeriod.Resolve(query.Preset, query.From, query.To, ReportPeriod.TodayUtc(now));
        var cells = await reporting.ListSlaCohortAsync(period.StartUtc, period.EndExclusiveUtc, now, cancellationToken);

        var buckets = ReportMath.Buckets(period);
        var trend = buckets.Select(b => (b.Start, b.End, Cells: new List<SlaCohortCell>())).ToList();
        foreach (var cell in cells)
        {
            var index = ReportMath.BucketIndex(period, cell.Day);
            if (index >= 0)
            {
                trend[index].Cells.Add(cell);
            }
        }

        var context = await evaluator.CreateContextAsync(cancellationToken);
        var active = await monitoring.ListActiveTrackedAsync(SlaRules.OverviewScanLimit, cancellationToken);
        var views = active.Select(r => SlaEvaluator.Evaluate(r.Sla, context)).ToList();
        var live = new SlaLiveStatus(
            await reporting.CountActiveBreachedTicketsAsync(now, cancellationToken),
            views.Count(v => v.FirstResponse.IsWarning),
            views.Count(v => v.Resolution.IsWarning),
            active.Count,
            active.Count >= SlaRules.OverviewScanLimit,
            context.WarningRules.Count,
            context.Calendar is not null);

        return new SlaReport(
            period,
            Summarize(cells, c => c.FirstResponse),
            Summarize(cells, c => c.Resolution),
            trend.Select(b =>
            {
                var (responseMet, responseBreached) = Decided(b.Cells, c => c.FirstResponse);
                var (resolutionMet, resolutionBreached) = Decided(b.Cells, c => c.Resolution);
                return new SlaTrendBucket(
                    b.Start, b.End,
                    responseMet, responseBreached, ReportMath.Percent(responseMet, responseMet + responseBreached),
                    resolutionMet, resolutionBreached, ReportMath.Percent(resolutionMet, resolutionMet + resolutionBreached));
            }).ToList(),
            live,
            now);
    }

    private static SlaTargetSummary Summarize(IReadOnlyList<SlaCohortCell> cells, Func<SlaCohortCell, ReportSlaOutcome> outcome)
    {
        int Count(ReportSlaOutcome o) => cells.Where(c => outcome(c) == o).Sum(c => c.Count);
        var total = cells.Sum(c => c.Count);
        var notApplicable = Count(ReportSlaOutcome.NotApplicable);
        var met = Count(ReportSlaOutcome.Met);
        var breached = Count(ReportSlaOutcome.Breached);
        return new SlaTargetSummary(
            total, notApplicable, total - notApplicable, met, breached, Count(ReportSlaOutcome.Pending), ReportMath.Percent(met, met + breached));
    }

    private static (int Met, int Breached) Decided(IEnumerable<SlaCohortCell> cells, Func<SlaCohortCell, ReportSlaOutcome> outcome)
    {
        var list = cells.ToList();
        return (list.Where(c => outcome(c) == ReportSlaOutcome.Met).Sum(c => c.Count),
                list.Where(c => outcome(c) == ReportSlaOutcome.Breached).Sum(c => c.Count));
    }
}

public sealed class GetSlaReportValidator : AbstractValidator<GetSlaReportQuery>
{
    public GetSlaReportValidator(TimeProvider timeProvider) => this.AddReportPeriodRules(timeProvider);
}
