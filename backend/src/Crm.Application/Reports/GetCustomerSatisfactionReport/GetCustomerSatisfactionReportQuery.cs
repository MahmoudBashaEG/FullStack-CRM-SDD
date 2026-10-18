using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Validation;
using Crm.Domain.Tickets;
using FluentValidation;
using Mediator;

namespace Crm.Application.Reports.GetCustomerSatisfactionReport;

public sealed record GetCustomerSatisfactionReportQuery(ReportPreset Preset, DateOnly? From, DateOnly? To)
    : IQuery<CustomerSatisfactionReport>, IReportPeriodQuery;

public sealed class GetCustomerSatisfactionReportQueryHandler(IReportingRepository reporting, TimeProvider timeProvider)
    : IQueryHandler<GetCustomerSatisfactionReportQuery, CustomerSatisfactionReport>
{
    public async ValueTask<CustomerSatisfactionReport> Handle(GetCustomerSatisfactionReportQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var period = ReportPeriod.Resolve(query.Preset, query.From, query.To, ReportPeriod.TodayUtc(now));

        var ratings = await reporting.ListFeedbackRatingsAsync(period.StartUtc, period.EndExclusiveUtc, cancellationToken);
        var count = ratings.Sum(r => r.Count);
        var positive = ratings.Where(r => r.Rating >= 4).Sum(r => r.Count);
        double? average = count == 0 ? null : Math.Round((double)ratings.Sum(r => r.Rating * r.Count) / count, 2);
        var distribution = Enumerable.Range(TicketFeedback.MinRating, TicketFeedback.MaxRating)
            .Select(r => new ReportCount<int>(r, ratings.Where(x => x.Rating == r).Sum(x => x.Count)))
            .ToList();

        var participation = await reporting.GetFeedbackParticipationAsync(period.StartUtc, period.EndExclusiveUtc, cancellationToken);
        var recent = await reporting.ListRecentFeedbackAsync(
            period.StartUtc, period.EndExclusiveUtc, ReportRules.RecentFeedbackLimit, cancellationToken);

        return new CustomerSatisfactionReport(
            period, count, average, positive, ReportMath.Percent(positive, count), distribution,
            new FeedbackParticipation(
                participation.EligibleTickets, participation.TicketsWithFeedback,
                ReportMath.Percent(participation.TicketsWithFeedback, participation.EligibleTickets)),
            recent, ReportRules.RecentFeedbackLimit, now);
    }
}

public sealed class GetCustomerSatisfactionReportValidator : AbstractValidator<GetCustomerSatisfactionReportQuery>
{
    public GetCustomerSatisfactionReportValidator(TimeProvider timeProvider) => this.AddReportPeriodRules(timeProvider);
}
