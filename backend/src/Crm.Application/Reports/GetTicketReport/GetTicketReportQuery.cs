using Crm.Application.Abstractions.Persistence;
using FluentValidation;
using Mediator;

namespace Crm.Application.Reports.GetTicketReport;

public sealed record GetTicketReportQuery(ReportPreset Preset, DateOnly? From, DateOnly? To) : IQuery<TicketReport>, IReportPeriodQuery;

public sealed class GetTicketReportQueryHandler(IReportingRepository reporting, TimeProvider timeProvider)
    : IQueryHandler<GetTicketReportQuery, TicketReport>
{
    public async ValueTask<TicketReport> Handle(GetTicketReportQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var period = ReportPeriod.Resolve(query.Preset, query.From, query.To, ReportPeriod.TodayUtc(now));
        var cells = await reporting.ListTicketCohortAsync(period.StartUtc, period.EndExclusiveUtc, cancellationToken);

        // One statement ⇒ volume, each distribution and the trend are sums of the same cells and always reconcile.
        var buckets = ReportMath.Buckets(period);
        var trend = new int[buckets.Count];
        foreach (var cell in cells)
        {
            var index = ReportMath.BucketIndex(period, cell.Day);
            if (index >= 0)
            {
                trend[index] += cell.Count;
            }
        }

        return new TicketReport(
            period,
            cells.Sum(c => c.Count),
            ReportMath.CountByMember(cells, c => c.Status, c => c.Count),
            ReportMath.CountByMember(cells, c => c.Category, c => c.Count),
            ReportMath.CountByMember(cells, c => c.Priority, c => c.Count),
            buckets.Select((b, i) => new ReportTrendPoint(b.Start, b.End, trend[i])).ToList(),
            now);
    }
}

public sealed class GetTicketReportValidator : AbstractValidator<GetTicketReportQuery>
{
    public GetTicketReportValidator(TimeProvider timeProvider) => this.AddReportPeriodRules(timeProvider);
}
