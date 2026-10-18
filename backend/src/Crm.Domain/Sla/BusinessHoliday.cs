namespace Crm.Domain.Sla;

/// <summary>A closed calendar date (local date in the calendar's time zone). Owned by BusinessCalendar.</summary>
public sealed record BusinessHoliday(DateOnly Date, string Name);
