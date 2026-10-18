namespace Crm.Application.Reports;

/// <summary>Pure helpers shared by every report, so the same metric is computed one way.</summary>
internal static class ReportMath
{
    /// <summary>0–100 rounded to 1 decimal; null (N/A) when the denominator is 0 — never 0 %.</summary>
    public static double? Percent(int numerator, int denominator) =>
        denominator == 0 ? null : Math.Round(100.0 * numerator / denominator, 1);

    /// <summary>Every enum member in declaration order, zeros included (RM2 "including zeros").</summary>
    public static IReadOnlyList<ReportCount<TEnum>> CountByMember<TCell, TEnum>(
        IEnumerable<TCell> cells, Func<TCell, TEnum> key, Func<TCell, int> count)
        where TEnum : struct, Enum
    {
        var totals = cells.GroupBy(key).ToDictionary(g => g.Key, g => g.Sum(count));
        return Enum.GetValues<TEnum>().Select(v => new ReportCount<TEnum>(v, totals.GetValueOrDefault(v))).ToList();
    }

    /// <summary>Bucket start/end pairs covering the whole period (R8).</summary>
    public static IReadOnlyList<(DateOnly Start, DateOnly End)> Buckets(ReportPeriod period)
    {
        var buckets = new List<(DateOnly, DateOnly)>();
        for (var start = period.From; start <= period.To; start = start.AddDays(period.BucketDays))
        {
            var end = start.AddDays(period.BucketDays - 1);
            buckets.Add((start, end > period.To ? period.To : end));
        }

        return buckets;
    }

    /// <summary>-1 when the day lies outside the period (defensive; SQL filters by the same range).</summary>
    public static int BucketIndex(ReportPeriod period, DateOnly day) =>
        day < period.From || day > period.To ? -1 : (day.DayNumber - period.From.DayNumber) / period.BucketDays;
}
