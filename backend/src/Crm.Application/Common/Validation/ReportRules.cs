namespace Crm.Application.Common.Validation;

public static class ReportRules
{
    public const int MaxRangeDays = 366;
    public const int DailyBucketMaxDays = 31;   // longer periods use 7-day buckets starting at the period start
    public const int WeeklyBucketDays = 7;
    public const int RecentFeedbackLimit = 20;
    public static readonly DateOnly EarliestDate = new(2000, 1, 1);
}
