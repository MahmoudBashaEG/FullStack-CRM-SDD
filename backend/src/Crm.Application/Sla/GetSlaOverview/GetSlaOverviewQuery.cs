using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Validation;
using Mediator;

namespace Crm.Application.Sla.GetSlaOverview;

/// <summary>Counts cover the first SlaRules.OverviewScanLimit active tracked tickets (oldest due first); lists are capped at OverviewListLimit.</summary>
public sealed record GetSlaOverviewQuery : IQuery<SlaOverview>;

public sealed class GetSlaOverviewQueryHandler(
    SlaEvaluator evaluator,
    ISlaMonitoringRepository monitoring,
    IAutomationRuleRepository rules) : IQueryHandler<GetSlaOverviewQuery, SlaOverview>
{
    public async ValueTask<SlaOverview> Handle(GetSlaOverviewQuery query, CancellationToken cancellationToken)
    {
        var context = await evaluator.CreateContextAsync(cancellationToken);
        var rows = await monitoring.ListActiveTrackedAsync(SlaRules.OverviewScanLimit, cancellationToken);

        var evaluated = rows
            .Select(r => new SlaOverviewTicket(r.TicketId, r.Subject, r.Status, r.AssignedAgentName, SlaEvaluator.Evaluate(r.Sla, context)))
            .ToList();
        var warning = evaluated.Where(t => t.Sla.Indicator == SlaIndicator.Warning).ToList();
        var breached = evaluated.Where(t => t.Sla.Indicator == SlaIndicator.Breached).ToList();
        var onTrack = evaluated.Count(t => t.Sla.Indicator == SlaIndicator.OnTrack);

        return new SlaOverview(
            warning.Count,
            breached.Count,
            onTrack,
            warning.Take(SlaRules.OverviewListLimit).ToList(),
            breached.Take(SlaRules.OverviewListLimit).ToList(),
            await rules.ListRecentExecutionsAsync(SlaRules.RecentExecutionsLimit, cancellationToken),
            context.Now);
    }
}
