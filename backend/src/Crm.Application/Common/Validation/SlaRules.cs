namespace Crm.Application.Common.Validation;

public static class SlaRules
{
    public const int MaxTargetMinutes = 525_600;      // one year of business minutes
    public const int MaxWarningLeadMinutes = 10_080;
    public const int RuleNameMaxLength = 100;
    public const int MaxSortOrder = 1000;
    public const int TimeZoneIdMaxLength = 64;
    public const int HolidayNameMaxLength = 100;
    public const int MaxHolidays = 366;
    public const int MaxPreviewMinutes = 525_600;
    public const int WorkerBatchSize = 200;
    public const int WorkerMaxPagesPerRule = 10; // per (rule, kind) per pass => at most 2,000 candidates; the rest next pass
    public const int OverviewListLimit = 50;
    public const int OverviewScanLimit = 500;
    public const int RecentExecutionsLimit = 20;
    public const string AutomationActorName = "Automation";
}
