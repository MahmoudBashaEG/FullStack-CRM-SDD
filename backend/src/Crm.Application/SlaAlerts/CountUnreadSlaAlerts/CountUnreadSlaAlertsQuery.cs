using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Mediator;

namespace Crm.Application.SlaAlerts.CountUnreadSlaAlerts;

public sealed record CountUnreadSlaAlertsQuery(StaffActor Actor) : IQuery<int>;

public sealed class CountUnreadSlaAlertsQueryHandler(ISlaAlertRepository alerts) : IQueryHandler<CountUnreadSlaAlertsQuery, int>
{
    public async ValueTask<int> Handle(CountUnreadSlaAlertsQuery query, CancellationToken cancellationToken) =>
        await alerts.CountUnreadAsync(query.Actor, cancellationToken);
}
