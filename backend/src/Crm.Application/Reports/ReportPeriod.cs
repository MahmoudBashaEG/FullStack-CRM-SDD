using Crm.Application.Common.Validation;

namespace Crm.Application.Reports;

/// <summary>REPORT-001 shared date selection. Stored by name in query strings ("Last30Days").</summary>
public enum ReportPreset
{
    Last7Days = 0,
    Last30Days = 1,
    Last90Days = 2,
    Custom = 3,
}

/// <summary>Every report query carries the same three inputs (validated by ReportPeriodRules).</summary>
public interface IReportPeriodQuery
{
    ReportPreset Preset { get; }
    DateOnly? From { get; }
    DateOnly? To { get; }
}

/// <summary>
/// R8: UTC calendar days, From..To inclusive, SQL range [StartUtc, EndExclusiveUtc). "Last N days" ends today (UTC) inclusive.
/// BucketDays = 1 for periods of at most 31 days, else 7 (consecutive buckets starting at From; the last one may be shorter).
/// </summary>
public sealed record ReportPeriod(
    ReportPreset Preset, DateOnly From, DateOnly To, DateTimeOffset StartUtc, DateTimeOffset EndExclusiveUtc, int BucketDays)
{
    public int Days => To.DayNumber - From.DayNumber + 1;

    public static DateOnly TodayUtc(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);

    public static ReportPeriod Resolve(ReportPreset preset, DateOnly? from, DateOnly? to, DateOnly todayUtc)
    {
        var (start, end) = preset switch
        {
            ReportPreset.Last7Days => (todayUtc.AddDays(-6), todayUtc),
            ReportPreset.Last30Days => (todayUtc.AddDays(-29), todayUtc),
            ReportPreset.Last90Days => (todayUtc.AddDays(-89), todayUtc),
            _ => (from!.Value, to!.Value), // Custom: the validator requires both
        };
        var days = end.DayNumber - start.DayNumber + 1;
        return new ReportPeriod(
            preset, start, end, Utc(start), Utc(end.AddDays(1)),
            days <= ReportRules.DailyBucketMaxDays ? 1 : ReportRules.WeeklyBucketDays);
    }

    private static DateTimeOffset Utc(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
