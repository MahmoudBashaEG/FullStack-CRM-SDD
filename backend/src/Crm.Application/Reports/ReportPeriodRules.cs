using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Reports;

internal static class ReportPeriodRules
{
    /// <summary>
    /// The one period validation, added by every report validator. Keys: preset, from, to. from/to are ignored unless Custom.
    /// "to" may be today (UTC) + 1 so a client ahead of UTC can pick its local today.
    /// </summary>
    public static void AddReportPeriodRules<T>(this AbstractValidator<T> validator, TimeProvider timeProvider)
        where T : IReportPeriodQuery
    {
        validator.RuleFor(q => q.Preset).IsInEnum();
        validator.When(q => q.Preset == ReportPreset.Custom, () =>
        {
            validator.RuleFor(q => q.From)
                .NotNull().WithMessage("Choose a start date for a custom range.")
                .GreaterThanOrEqualTo(ReportRules.EarliestDate).WithMessage("The start date must be on or after 2000-01-01.");
            validator.RuleFor(q => q.To)
                .NotNull().WithMessage("Choose an end date for a custom range.")
                .Must(to => to is null || to.Value <= ReportPeriod.TodayUtc(timeProvider.GetUtcNow()).AddDays(1))
                .WithMessage("The end date cannot be in the future.")
                .Must((q, to) => q.From is null || to is null || q.From.Value <= to.Value)
                .WithMessage("The start date must be on or before the end date.")
                .Must((q, to) => q.From is null || to is null || to.Value.DayNumber - q.From.Value.DayNumber + 1 <= ReportRules.MaxRangeDays)
                .WithMessage($"A custom range can cover at most {ReportRules.MaxRangeDays} days.");
        });
    }
}
