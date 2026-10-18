using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Sla;
using Mediator;

namespace Crm.Application.Sla.GetBusinessCalendar;

/// <summary>Not configured yet => IsConfigured false with null values and empty lists.</summary>
public sealed record GetBusinessCalendarQuery : IQuery<BusinessCalendarDetails>;

public sealed class GetBusinessCalendarQueryHandler(ISlaConfigurationRepository configuration)
    : IQueryHandler<GetBusinessCalendarQuery, BusinessCalendarDetails>
{
    public async ValueTask<BusinessCalendarDetails> Handle(GetBusinessCalendarQuery query, CancellationToken cancellationToken) =>
        await BusinessCalendarMapper.ToDetailsAsync(await configuration.FindCalendarAsync(cancellationToken), configuration, cancellationToken);
}

internal static class BusinessCalendarMapper
{
    public static async Task<BusinessCalendarDetails> ToDetailsAsync(
        BusinessCalendar? calendar, ISlaConfigurationRepository configuration, CancellationToken cancellationToken)
    {
        if (calendar is null)
        {
            return new BusinessCalendarDetails(false, null, [], null, null, [], null, null);
        }

        return new BusinessCalendarDetails(
            true,
            calendar.TimeZoneId,
            calendar.WorkingDays.OrderBy(d => d).ToList(),
            calendar.OpensAt,
            calendar.ClosesAt,
            calendar.Holidays.OrderBy(h => h.Date).ToList(),
            calendar.UpdatedAtUtc,
            await configuration.FindUserNameAsync(calendar.UpdatedByUserId, cancellationToken));
    }
}
