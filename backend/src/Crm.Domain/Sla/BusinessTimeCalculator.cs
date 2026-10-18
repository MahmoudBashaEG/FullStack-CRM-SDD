using Crm.Domain.Common;

namespace Crm.Domain.Sla;

/// <summary>SLA-001 §5. Pure functions; every input/output instant is UTC. Nights, closed weekdays and holidays never count.</summary>
public static class BusinessTimeCalculator
{
    public const int MaxDaysScanned = 3660;

    /// <summary>startUtc + minutes of business time. A start outside working hours begins at the next open period. minutes &gt; 0.</summary>
    public static DateTimeOffset AddBusinessMinutes(DateTimeOffset startUtc, int minutes, BusinessCalendarRules calendar)
    {
        var remaining = TimeSpan.FromMinutes(minutes);
        var date = calendar.LocalDateOf(startUtc);
        for (var i = 0; i < MaxDaysScanned; i++, date = date.AddDays(1))
        {
            if (!calendar.TryGetWindowUtc(date, out var open, out var close))
            {
                continue;
            }

            var from = startUtc > open ? startUtc : open;
            if (from >= close)
            {
                continue;
            }

            var available = close - from;
            if (available >= remaining)
            {
                return from + remaining;
            }

            remaining -= available;
        }

        throw new DomainException("The business calendar has no working time in the next ten years.");
    }

    /// <summary>Whole business minutes in [fromUtc, toUtc); 0 when toUtc &lt;= fromUtc.</summary>
    public static int BusinessMinutesBetween(DateTimeOffset fromUtc, DateTimeOffset toUtc, BusinessCalendarRules calendar)
    {
        if (toUtc <= fromUtc)
        {
            return 0;
        }

        var total = TimeSpan.Zero;
        var date = calendar.LocalDateOf(fromUtc);
        var last = calendar.LocalDateOf(toUtc);
        for (var i = 0; i < MaxDaysScanned && date <= last; i++, date = date.AddDays(1))
        {
            if (!calendar.TryGetWindowUtc(date, out var open, out var close))
            {
                continue;
            }

            var start = fromUtc > open ? fromUtc : open;
            var end = toUtc < close ? toUtc : close;
            if (end > start)
            {
                total += end - start;
            }
        }

        return (int)Math.Floor(total.TotalMinutes);
    }

    /// <summary>Positive = business minutes left until dueUtc; negative = business minutes overdue.</summary>
    public static int SignedBusinessMinutesUntil(DateTimeOffset nowUtc, DateTimeOffset dueUtc, BusinessCalendarRules calendar) =>
        nowUtc <= dueUtc ? BusinessMinutesBetween(nowUtc, dueUtc, calendar) : -BusinessMinutesBetween(dueUtc, nowUtc, calendar);
}
