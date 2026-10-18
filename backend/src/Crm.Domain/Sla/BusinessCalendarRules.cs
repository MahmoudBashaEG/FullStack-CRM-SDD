namespace Crm.Domain.Sla;

/// <summary>Resolved calendar. Local windows are [date + OpensAt, date + ClosesAt) on working, non-holiday local dates.</summary>
public sealed record BusinessCalendarRules(
    TimeZoneInfo TimeZone, IReadOnlySet<DayOfWeek> WorkingDays, TimeOnly OpensAt, TimeOnly ClosesAt, IReadOnlySet<DateOnly> Holidays)
{
    public bool TryGetWindowUtc(DateOnly localDate, out DateTimeOffset openUtc, out DateTimeOffset closeUtc)
    {
        openUtc = closeUtc = default;
        if (!WorkingDays.Contains(localDate.DayOfWeek) || Holidays.Contains(localDate))
        {
            return false;
        }

        openUtc = ToUtc(localDate, OpensAt);
        closeUtc = ToUtc(localDate, ClosesAt);
        return closeUtc > openUtc;
    }

    public DateOnly LocalDateOf(DateTimeOffset utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, TimeZone).DateTime);

    // DST gap: move forward to the first valid local minute. Ambiguous (fall back): ConvertTimeToUtc uses standard time.
    private DateTimeOffset ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        while (TimeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, TimeZone), TimeSpan.Zero);
    }
}
