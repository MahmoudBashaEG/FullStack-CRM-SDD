using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Automation;
using Crm.Domain.Sla;

namespace Crm.Application.Sla;

/// <summary>
/// The ONE SLA evaluation service (SLA-001 intake §11): the ticket views, list, dashboard, overview and the worker all use these
/// helpers, so a ticket shown as "Warning" is the ticket the worker alerts on.
/// </summary>
public sealed class SlaEvaluator(ISlaConfigurationRepository configuration, IAutomationRuleRepository rules, TimeProvider timeProvider)
{
    public async Task<SlaEvaluationContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        var calendar = await configuration.FindCalendarAsync(cancellationToken);
        var warningRules = await rules.ListEnabledAsync(AutomationTrigger.SlaWarningDue, cancellationToken);
        return new SlaEvaluationContext(calendar?.ToRules(), warningRules, timeProvider.GetUtcNow());
    }

    public static TicketSlaView Evaluate(TicketSlaInput input, SlaEvaluationContext context)
    {
        var s = input.Snapshot;
        var first = Target(
            SlaKind.FirstResponse, s.FirstResponseTargetMinutes, s.FirstResponseDueAtUtc, s.FirstRespondedAtUtc, s.FirstRespondedByName, input, context);
        var resolution = Target(
            SlaKind.Resolution, s.ResolutionTargetMinutes, s.ResolutionDueAtUtc, s.ResolvedAtUtc, null, input, context);

        var indicator =
            first.State == SlaTargetState.Breached || resolution.State == SlaTargetState.Breached ? SlaIndicator.Breached
            : first.State == SlaTargetState.Met && resolution.State == SlaTargetState.Met ? SlaIndicator.Met
            : first.IsWarning || resolution.IsWarning ? SlaIndicator.Warning
            : SlaIndicator.OnTrack;
        return new TicketSlaView(indicator, s.TimeZoneId, first, resolution);
    }

    /// <summary>F3: the ONE remaining-time rule, shared by the display and the worker. Current calendar, stored due instant.
    /// Positive = business minutes left, negative = overdue, null = no calendar.</summary>
    public static int? RemainingBusinessMinutes(DateTimeOffset now, DateTimeOffset due, BusinessCalendarRules? calendar) =>
        calendar is null ? null : BusinessTimeCalculator.SignedBusinessMinutesUntil(now, due, calendar);

    /// <summary>F1: a rule's own threshold — evaluated per rule, never against a minimum or maximum across rules.</summary>
    public static bool IsWarningThresholdReached(AutomationRule rule, int? remainingBusinessMinutes) =>
        remainingBusinessMinutes is { } remaining && remaining >= 0 && rule.WarningLeadMinutes is { } lead && remaining <= lead;

    // State from instants only (calendar-independent). Business minutes need the calendar; when it is missing, elapsed = 0, remaining = null.
    private static SlaTargetView Target(
        SlaKind kind, int minutes, DateTimeOffset due, DateTimeOffset? completed, string? by,
        TicketSlaInput input, SlaEvaluationContext context)
    {
        var state = completed is { } c
            ? (c <= due ? SlaTargetState.Met : SlaTargetState.Breached)
            : (context.Now > due ? SlaTargetState.Breached : SlaTargetState.Pending);

        var elapsed = context.Calendar is null
            ? 0
            : BusinessTimeCalculator.BusinessMinutesBetween(input.Snapshot.CreatedAtUtc, completed ?? context.Now, context.Calendar);
        var remaining = completed is null ? RemainingBusinessMinutes(context.Now, due, context.Calendar) : null;

        var isWarning = state == SlaTargetState.Pending
            && context.WarningRules.Any(r =>
                r.Matches(input.Category, input.Priority, input.IsAssigned, kind) && IsWarningThresholdReached(r, remaining));

        return new SlaTargetView(kind, minutes, due, completed, by, state, isWarning, remaining, elapsed);
    }
}
