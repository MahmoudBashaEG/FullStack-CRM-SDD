using Crm.Domain.Common;

namespace Crm.Domain.Sla;

/// <summary>
/// SLA-001 S3: the one global business calendar (row Id 1): a time zone, working weekdays, one daily opening interval and holidays.
/// Changes affect tickets created afterwards only (S4).
/// </summary>
public sealed class BusinessCalendar
{
    public const int SingletonId = 1;

    private BusinessCalendar() { } // EF Core

    private readonly List<BusinessHoliday> _holidays = [];

    public int Id { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;

    /// <summary>Bit (1 &lt;&lt; (int)DayOfWeek) per working day; Sunday = bit 0.</summary>
    public int WorkingDaysMask { get; private set; }

    public TimeOnly OpensAt { get; private set; }
    public TimeOnly ClosesAt { get; private set; }
    public IReadOnlyCollection<BusinessHoliday> Holidays => _holidays;
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }

    public IReadOnlySet<DayOfWeek> WorkingDays =>
        Enum.GetValues<DayOfWeek>().Where(d => (WorkingDaysMask & (1 << (int)d)) != 0).ToHashSet();

    public static BusinessCalendar Create(
        string timeZoneId, IReadOnlyCollection<DayOfWeek> workingDays, TimeOnly opensAt, TimeOnly closesAt,
        IReadOnlyList<BusinessHoliday> holidays, Guid actorUserId, DateTimeOffset now)
    {
        var calendar = new BusinessCalendar { Id = SingletonId };
        calendar.Update(timeZoneId, workingDays, opensAt, closesAt, holidays, actorUserId, now);
        return calendar;
    }

    /// <summary>Throws DomainException when: unknown time zone, no working day, OpensAt >= ClosesAt, duplicate holiday dates.</summary>
    public void Update(
        string timeZoneId, IReadOnlyCollection<DayOfWeek> workingDays, TimeOnly opensAt, TimeOnly closesAt,
        IReadOnlyList<BusinessHoliday> holidays, Guid actorUserId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId) || !TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _))
        {
            throw new DomainException("Unknown time zone. Use an IANA id such as Africa/Cairo.");
        }

        if (workingDays.Count == 0)
        {
            throw new DomainException("Select at least one working day.");
        }

        if (opensAt >= closesAt)
        {
            throw new DomainException("Closing time must be after opening time.");
        }

        if (holidays.Select(h => h.Date).Distinct().Count() != holidays.Count)
        {
            throw new DomainException("Holiday dates must be unique.");
        }

        TimeZoneId = timeZoneId;
        WorkingDaysMask = workingDays.Distinct().Aggregate(0, (mask, day) => mask | (1 << (int)day));
        OpensAt = opensAt;
        ClosesAt = closesAt;
        _holidays.Clear();
        _holidays.AddRange(holidays.OrderBy(h => h.Date));
        UpdatedAtUtc = now;
        UpdatedByUserId = actorUserId;
    }

    public BusinessCalendarRules ToRules() =>
        new(TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId), WorkingDays, OpensAt, ClosesAt, _holidays.Select(h => h.Date).ToHashSet());
}
